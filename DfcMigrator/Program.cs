using System.Text.Json;
using DfcMigrator;

const string Usage = """
DFC -> SNAP V2 customer migration

  dfcmigrate                      interactive: asks for the customer's data folder first
  dfcmigrate <data folder>        interactive, starting with that folder (or drag the folder onto the exe)
  dfcmigrate --data <folder> --list-customers
  dfcmigrate --data <folder> --output <SNAP Clients folder> [--customer <DFC ClubID>] [--dry-run] [--replace] [--yes]
             [--reference <reference client folder>] [--base-url <running SNAP site>] [--template <dir>] [--logs <dir>]

--customer   which customer to migrate when the folder holds several (see --list-customers)
--json       print one JSON result instead of console text (merchant IDs masked, no emails); writing needs --yes
--search     with --list-customers --json: part of the name or a ClubID

  dfcmigrate mcp                  tool server for the Claude Desktop plugin (stdio)
  dfcmigrate guard                plugin hook: blocks Claude's file/shell tools from reading DFC data folders
--dry-run    check the data and list every file that would be written; writes nothing
--reference  compare the generated site with a known-good client folder (e.g. Template\MidUlsterDistrictCouncil\MidUlsterDistrictCouncil)
--base-url   also check the deployed pages over HTTP (e.g. http://localhost:4200)
""";

Console.OutputEncoding = System.Text.Encoding.UTF8;
var ws = new Workspace();

try
{
    if (args is ["mcp"]) return await McpMode.Run(ws);
    if (args is ["guard"]) return Guard.Run(Console.In.ReadToEnd(), Console.Out);
    if (args.Length == 0 || (args.Length == 1 && !args[0].StartsWith("--")))
        return await Interactive(args.Length == 1 ? args[0] : null);
    var (opts, flags) = ParseArgs(args);
    if (!opts.TryGetValue("data", out var dataDir)) { Console.Error.WriteLine(Usage); return 2; }
    if (flags.Contains("json")) return await JsonMode(dataDir, opts, flags);
    var all = LoadFolder(dataDir);
    if (flags.Contains("list-customers")) { ListCustomers(all.Customers()); return all.IsValid ? 0 : 1; }
    if (!opts.TryGetValue("output", out var outDir)) { Console.Error.WriteLine(Usage); return 2; }
    var customers = all.Customers();
    int customerId;
    if (opts.TryGetValue("customer", out var c)) { if (!int.TryParse(c, out customerId)) throw new ArgumentException($"--customer '{c}' must be a DFC ClubID number."); }
    else if (customers.Count == 1) customerId = customers[0].CustomerId;
    else { ListCustomers(customers); Console.Error.WriteLine($"ERROR: the folder holds {customers.Count} customers - choose one with --customer <ClubID>."); return 2; }
    var data = Extract(all, customerId);
    if (!data.IsValid) return 1; // problems already listed
    var plan = BuildPlan(data);
    if (!plan.IsValid) return 1;
    return await Generate(data, plan, outDir, opts.GetValueOrDefault("template") ?? ws.TemplateDir, opts.GetValueOrDefault("logs") ?? ws.LogsDir,
        dryRun: flags.Contains("dry-run"), replace: flags.Contains("replace"), confirm: !flags.Contains("yes"),
        reference: opts.GetValueOrDefault("reference"), baseUrl: opts.GetValueOrDefault("base-url"));
}
catch (Exception ex) when (ex is DfcDataException or IOException or UnauthorizedAccessException or ArgumentException)
{
    Console.Error.WriteLine("ERROR: " + ex.Message);
    return 1;
}

// =====================================================================================================
// Interactive run: data folder first, then output folder, preview, confirm, generate, validate.
// =====================================================================================================
async Task<int> Interactive(string? folder)
{
    Console.Title = "DFC to SNAP V2 Migration Tool";
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("""

      ==============================================================
         DFC  ->  SNAP V2  CUSTOMER MIGRATION TOOL
         Customer data folder  ->  SNAP V2 portal (common template)
      ==============================================================
    """);
    Console.ResetColor();
    Prompt.Say($"Template : {ws.TemplateDir}", ConsoleColor.DarkGray);
    try
    {
        while (true)
        {
            // STEP 1 - the customer's migration data folder (always the first question).
            Prompt.Header("STEP 1  CUSTOMER DATA FOLDER");
            folder = folder is not null && Directory.Exists(folder) ? Path.GetFullPath(folder)
                : Prompt.AskExistingFolder("Customer migration data folder (type or drag the folder here)", ws.Settings.GetValueOrDefault("lastDataFolder"));
            ws.Remember("lastDataFolder", folder);
            var all = LoadFolder(folder);
            var customers = all.Customers();
            CustomerFolder data = all;
            if (customers.Count > 0)
            {
                // STEP 1b - an all-customer folder: pick who to migrate (a one-customer folder is picked automatically).
                var picked = customers.Count == 1 ? customers[0] : PickCustomer(customers);
                ws.Remember("lastCustomer", picked.CustomerId.ToString());
                data = Extract(all, picked.CustomerId);
            }
            var plan = data.IsValid ? BuildPlan(data) : null;
            if (plan is null || !plan.IsValid)
            {
                var choice = Prompt.Ask("Fix the data and press R to re-check, F to choose another folder, or Q to quit", "R").ToUpperInvariant();
                if (choice == "Q") return 1;
                if (choice == "F") folder = null;
                continue;
            }

            // STEP 2 - where the portal is created.
            Prompt.Header("STEP 2  SNAP CLIENTS FOLDER");
            var output = Prompt.AskFolder("SNAP Clients folder to create the portal in", ws.Settings.GetValueOrDefault("clientsFolder", ws.DefaultOutput));
            ws.Remember("clientsFolder", output);
            var reference = Prompt.AskOptionalFolder("Reference client folder to compare with (blank = skip)", ws.Settings.GetValueOrDefault("referenceFolder", ""));
            if (reference != "") ws.Remember("referenceFolder", reference);
            var url = Prompt.AskUrl("Running SNAP site to test after deploying", ws.Settings.GetValueOrDefault("baseUrl", ""));
            // The page checks need the site up: catch "connection refused" now, not as failed checks after writing.
            while (url != "" && !await SiteIsUp(url))
            {
                Prompt.Say($"Nothing is answering at {url}.", ConsoleColor.Yellow);
                var a = Prompt.Ask("Start the SNAP site and press R to retry, S to skip the page checks, or Q to quit", "R").ToUpperInvariant();
                if (a == "Q") throw new QuitException();
                if (a == "S") url = "";
            }
            if (url != "") ws.Remember("baseUrl", url);

            // STEP 3 - preview, confirm, generate, validate.
            Prompt.Header("STEP 3  GENERATE");
            bool replace = false;
            var target = Path.Combine(output, plan.Site!.Folder);
            if (Directory.Exists(target))
            {
                Prompt.Say($"Portal folder already exists: {target}", ConsoleColor.Yellow);
                if (!Prompt.YesNo($"Replace it? The current copy is moved to {ws.BackupsDir}", false)) { Prompt.Say("Nothing was changed."); return 1; }
                replace = true;
            }
            return await Generate(data, plan, output, ws.TemplateDir, ws.LogsDir, dryRun: false, replace, confirm: true,
                reference == "" ? null : reference, url == "" ? null : url);
        }
    }
    catch (Exception ex) when (ex is QuitException or BackToMenuException) { Prompt.Say("Stopped - nothing was written."); return 1; }
    finally
    {
        Console.Write("\n  Press ENTER to close... ");
        Console.ReadLine();
    }
}

// =====================================================================================================
// Shared steps (interactive and command line run exactly the same code)
// =====================================================================================================
CustomerFolder LoadFolder(string folder)
{
    var data = CustomerFolder.Load(folder);
    Prompt.Say($"Data folder: {data.Path}", ConsoleColor.White);
    foreach (var (file, role) in data.Files)
        Prompt.Say($"  {role,-20} {file}", role is "not used" or "UNREADABLE" ? ConsoleColor.Yellow : ConsoleColor.Gray);
    Section("PROBLEMS IN THE DATA FOLDER", data.Errors, ConsoleColor.Red);
    Section("NOTES", data.Warnings, ConsoleColor.Yellow);
    return data;
}

CustomerFolder Extract(CustomerFolder all, int customerId)
{
    var data = all.ForCustomer(customerId);
    var s = all.Customers().FirstOrDefault(c => c.CustomerId == customerId);
    if (s is not null) Prompt.Say($"\nCustomer {s.CustomerId}: {s.Name} - {s.Portals} portal(s), {s.Venues} venues, {s.Plans} plans, {s.MerchantRows} merchant rows", ConsoleColor.White);
    Section("PROBLEMS FOR THIS CUSTOMER", data.Errors.Except(all.Errors).ToList(), ConsoleColor.Red);
    return data;
}

void ListCustomers(List<CustomerSummary> customers)
{
    Prompt.Header($"CUSTOMERS IN THE FOLDER ({customers.Count}, {customers.Count(c => c.Missing.Count == 0)} with complete data)");
    Prompt.Say($"  {"#",4}  {"ClubID",-7} {"Name",-40} {"Portals",7} {"Venues",6} {"Plans",6} {"Merch.",6}  Data", ConsoleColor.DarkGray);
    for (int i = 0; i < customers.Count; i++)
    {
        var c = customers[i];
        var name = c.Name.Length > 40 ? c.Name[..39] + "~" : c.Name;
        Prompt.Say($"  {i + 1,4}  {c.CustomerId,-7} {name,-40} {c.Portals,7} {c.Venues,6} {c.Plans,6} {c.MerchantRows,6}  {(c.Missing.Count == 0 ? "complete" : "missing " + string.Join(", ", c.Missing))}",
            c.Missing.Count == 0 ? ConsoleColor.Green : ConsoleColor.Gray);
    }
}

CustomerSummary PickCustomer(List<CustomerSummary> customers)
{
    var shown = customers;
    ListCustomers(shown);
    while (true)
    {
        var answer = Prompt.Ask("Customer to migrate: list number, DFC ClubID, or part of the name to search (* = full list)", ws.Settings.GetValueOrDefault("lastCustomer"));
        if (answer == "*") { ListCustomers(shown = customers); continue; }
        if (int.TryParse(answer, out var n))
        {
            var byId = customers.FirstOrDefault(c => c.CustomerId == n);
            if (byId is not null) return byId;
            if (n >= 1 && n <= shown.Count) return shown[n - 1];
        }
        var matches = customers.Where(c => c.Name.Contains(answer, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count == 1) return matches[0];
        if (matches.Count == 0) { Prompt.Say($"  No customer matches '{answer}'.", ConsoleColor.Yellow); continue; }
        ListCustomers(shown = matches);
    }
}

MigrationPlan BuildPlan(CustomerFolder data)
{
    var plan = Planner.Build(data, DateOnly.FromDateTime(DateTime.Today));
    if (plan.Site is { } s)
    {
        Prompt.Say("");
        Prompt.Say($"Customer : {s.CustomerName} (DFC ClubID {s.WebClub.ClubId})", ConsoleColor.White);
        Prompt.Say($"Site     : /Clients/{s.Folder}/", ConsoleColor.White);
        foreach (var p in s.Pages)
        {
            Prompt.Say($"Page     : {p.PageName}.aspx [{p.Type}] <- DFC portal {p.Portal.Id}: {p.Branches.Count} branches, {p.Branches.Sum(b => b.Products.Count)} products", ConsoleColor.White);
            foreach (var b in p.Branches) Prompt.Say($"             {b.Id,-34} {b.Products.Count,3} products   {b.Venue.Name}");
        }
        Prompt.Say($"Logo     : {(s.LogoFile is null ? "(none - Xplor Pay logo)" : Path.GetFileName(s.LogoFile))}", ConsoleColor.White);
    }
    Section("INFORMATION", plan.Info, ConsoleColor.Gray);
    Section("WARNINGS", plan.Warnings, ConsoleColor.Yellow);
    Section("ACTION ITEMS (written into the config as NEEDS VALUE / CONFIRM notes)", plan.ActionItems, ConsoleColor.Yellow);
    Section("ERRORS - nothing will be generated until these are fixed", plan.Errors, ConsoleColor.Red);
    return plan;
}

async Task<int> Generate(CustomerFolder data, MigrationPlan plan, string output, string template, string logs, bool dryRun, bool replace, bool confirm, string? reference, string? baseUrl)
{
    var site = plan.Site!;
    var result = SiteGenerator.Generate(site, Path.GetFullPath(template));
    Section("FILES", result.Files.OrderBy(f => f.RelPath).Select(f => $"{f.Action,-17} {f.RelPath}{(f.Source != f.RelPath ? "   <- " + f.Source : "")}").ToList(), ConsoleColor.Gray);
    Section("NOT COPIED FROM THE TEMPLATE", result.Skipped, ConsoleColor.Gray);
    Section("WARNINGS", result.Warnings, ConsoleColor.Yellow);
    if (!result.IsValid) { Section("ERRORS - nothing was written", result.Errors, ConsoleColor.Red); return 1; }
    if (dryRun) { Prompt.Say("Dry run OK - nothing was written.", ConsoleColor.Green); return 0; }

    var target = Path.Combine(Path.GetFullPath(output), site.Folder);
    if (Directory.Exists(target) && !replace) throw new IOException($"Portal folder already exists: {target}. Re-run with --replace (the old copy is moved to {ws.BackupsDir}).");
    if (confirm)
    {
        Console.Write($"\n  Create {result.Files.Count} file(s) in {target}? Type 'yes' to continue: ");
        if (!string.Equals(Console.ReadLine()?.Trim(), "yes", StringComparison.OrdinalIgnoreCase)) { Prompt.Say("Cancelled - nothing was written."); return 1; }
    }
    // Write, then validate: files read back from disk, optional live check, optional reference comparison, report.
    var r = await Operations.WriteAndVerify(null, data, plan, result, output, replace, template, logs, ws.BackupsDir, reference, baseUrl);
    if (r.Backup is not null) Prompt.Say($"Previous portal moved to {r.Backup}");
    Prompt.Say($"WRITTEN: {result.Files.Count} file(s) to {r.Written}", ConsoleColor.Green);
    foreach (var f in r.FailedChecks) Prompt.Say($"FAIL {f}", ConsoleColor.Red);
    if (r.ReferenceDataDiffs is { } dd) Prompt.Say($"Reference comparison: {dd} data difference(s), {r.ReferenceCosmeticDiffs} cosmetic - details in the report.", dd == 0 ? ConsoleColor.Green : ConsoleColor.Red);
    Prompt.Say($"{r.Status}: {r.ChecksTotal - r.ChecksFailed}/{r.ChecksTotal} checks passed. Report: {r.Report}",
        r.Status == "COMPLETE" ? ConsoleColor.Green : r.Status == "FAILED" ? ConsoleColor.Red : ConsoleColor.Yellow);
    return r.Status == "FAILED" ? 1 : 0;
}

// --json: one machine-readable result (the same objects the plugin's tools return). Writing requires --yes.
async Task<int> JsonMode(string dataDir, Dictionary<string, string> opts, HashSet<string> flags)
{
    object result;
    var customerId = opts.TryGetValue("customer", out var c) ? int.TryParse(c, out var n) ? n : throw new ArgumentException($"--customer '{c}' must be a DFC ClubID number.") : 0;
    var template = opts.GetValueOrDefault("template") ?? ws.TemplateDir;
    if (flags.Contains("list-customers")) result = Operations.ListCustomers(dataDir, opts.GetValueOrDefault("search"));
    else if (flags.Contains("dry-run") || !opts.ContainsKey("output")) result = Operations.Check(dataDir, customerId, template);
    else if (!flags.Contains("yes")) throw new ArgumentException("--json writes without asking, so it needs --yes (or use --dry-run).");
    else result = await Operations.Create(dataDir, customerId, opts["output"], flags.Contains("replace"), template, opts.GetValueOrDefault("logs") ?? ws.LogsDir,
        ws.BackupsDir, opts.GetValueOrDefault("reference"), opts.GetValueOrDefault("base-url"));
    Console.WriteLine(JsonSerializer.Serialize(result, Operations.Json));
    return result switch { CheckResult r => r.Status == "READY" ? 0 : 1, CreateResult r => r.Status is "COMPLETE" or "GENERATED - ACTION ITEMS OPEN" ? 0 : 1, _ => 0 };
}

// Any HTTP answer (even 404) means the site is running; only a failed connection or timeout means it isn't.
static async Task<bool> SiteIsUp(string url)
{
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    try { using var _ = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead); return true; }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return false; }
}

void Section(string title, List<string> lines, ConsoleColor color)
{
    if (lines.Count == 0) return;
    Prompt.Header($"{title} ({lines.Count})");
    foreach (var l in lines) Prompt.Say(l, color);
}

static (Dictionary<string, string> Opts, HashSet<string> Flags) ParseArgs(string[] a)
{
    var opts = new Dictionary<string, string>();
    var flags = new HashSet<string>();
    for (int i = 0; i < a.Length; i++)
    {
        if (!a[i].StartsWith("--")) throw new ArgumentException($"Unexpected argument '{a[i]}'.\n\n{Usage}");
        var k = a[i][2..];
        if (i + 1 < a.Length && !a[i + 1].StartsWith("--")) opts[k] = a[++i];
        else flags.Add(k);
    }
    return (opts, flags);
}
