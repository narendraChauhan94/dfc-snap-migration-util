using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace DfcMigrator;

// Results returned by --json and the MCP tools. Everything here can reach the AI agent, so every string
// goes through Redact(): merchant IDs/long numbers keep their last 4 digits and email addresses are removed.
public record CustomerList(string Folder, int Total, int Complete, int Matched, List<CustomerSummary> Customers, List<string> FolderErrors, List<string> FolderNotes);
public record BranchSummary(string Id, string Venue, string MerchantId, int Products);
public record PageSummary(string Page, string Type, int Portal, List<BranchSummary> Branches);
public record CheckResult(string Status, int CustomerId, string Customer, string? SiteFolder, List<PageSummary> Pages, string Logo, int FilesToWrite,
    List<string> Errors, List<string> Warnings, List<string> Info, List<string> ActionItems);
public record CreateResult(string Status, CheckResult? Check, string? Written, string? Backup, int ChecksTotal, int ChecksFailed, List<string> FailedChecks,
    int? ReferenceDataDiffs, int? ReferenceCosmeticDiffs, List<string> DataDiffs, string? Report);

/// <summary>
/// The migration steps without console output - shared by the command line (--json) and the MCP server (Claude Desktop plugin).
/// </summary>
public static partial class Operations
{
    public static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    const int MaxListed = 25; // keeps the agent's context small; the operator narrows the list by searching

    /// <summary>Customers in a data folder; query = part of the name or an exact DFC ClubID (blank = all, first 25).</summary>
    public static CustomerList ListCustomers(string folder, string? query = null)
    {
        var all = CustomerFolder.Load(folder);
        var customers = all.Customers();
        var q = (query ?? "").Trim();
        var matched = q == "" ? customers
            : customers.Where(c => c.CustomerId.ToString() == q || c.Name.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        return new CustomerList(all.Path, customers.Count, customers.Count(c => c.Missing.Count == 0), matched.Count,
            matched.Take(MaxListed).Select(c => c with { Name = Redact(c.Name) }).ToList(), Redact(all.Errors), Redact(all.Warnings));
    }

    /// <summary>Everything a dry run knows about one customer. Writes nothing.</summary>
    public static CheckResult Check(string folder, int customerId, string templateDir) => Prepare(folder, customerId, templateDir).Result;

    static (CheckResult Result, CustomerFolder? Data, MigrationPlan? Plan, GenerationResult? Generated) Prepare(string folder, int customerId, string templateDir)
    {
        var all = CustomerFolder.Load(folder);
        var customers = all.Customers();
        // A one-customer folder may be checked without knowing its ClubID.
        if (customerId == 0 && customers.Count == 1) customerId = customers[0].CustomerId;
        var data = all.ForCustomer(customerId);
        var name = customers.FirstOrDefault(c => c.CustomerId == customerId)?.Name ?? "";
        if (!data.IsValid) return (Result("DATA ERRORS", customerId, name, null, 0, data.Errors, data.Warnings, [], []), data, null, null);

        var plan = Planner.Build(data, DateOnly.FromDateTime(DateTime.Today));
        var warnings = data.Warnings.Concat(plan.Warnings).ToList();
        if (!plan.IsValid) return (Result("DATA ERRORS", customerId, name, null, 0, plan.Errors, warnings, plan.Info, plan.ActionItems), data, plan, null);

        var generated = SiteGenerator.Generate(plan.Site!, Path.GetFullPath(templateDir));
        warnings.AddRange(generated.Warnings);
        var status = !generated.IsValid ? "TEMPLATE ERRORS" : "READY";
        return (Result(status, customerId, name, plan.Site, generated.Files.Count, generated.Errors, warnings, plan.Info, plan.ActionItems), data, plan, generated);

        CheckResult Result(string s, int id, string n, SitePlan? site, int files, List<string> errors, List<string> warn, List<string> info, List<string> actions) => new(
            s, id, Redact(n), site?.Folder,
            site?.Pages.Select(p => new PageSummary(p.PageName, p.Type, p.Portal.Id,
                p.Branches.Select(b => new BranchSummary(b.Id, Redact(b.Venue.Name), Redact(b.MerchantId), b.Products.Count)).ToList())).ToList() ?? [],
            site is null ? "" : site.LogoFile is null ? "none - Xplor Pay logo" : Path.GetFileName(site.LogoFile),
            files, Redact(errors), Redact(warn), Redact(info), Redact(actions));
    }

    /// <summary>Generates the portal, writes it (old copy to Backups when replacing), then verifies it. Refuses if the data has errors.</summary>
    public static async Task<CreateResult> Create(string folder, int customerId, string outputRoot, bool replace, string templateDir, string logsDir, string backupsDir,
        string? reference, string? baseUrl)
    {
        var (check, data, plan, generated) = Prepare(folder, customerId, templateDir);
        if (check.Status != "READY") return Empty(check.Status);
        var target = Path.Combine(Path.GetFullPath(outputRoot), plan!.Site!.Folder);
        if (Directory.Exists(target) && !replace) return Empty("PORTAL EXISTS - approve a replace to continue");
        return await WriteAndVerify(check, data!, plan, generated!, outputRoot, replace, templateDir, logsDir, backupsDir, reference, baseUrl);

        CreateResult Empty(string status) => new(status, check, null, null, 0, 0, [], null, null, [], null);
    }

    /// <summary>Write + read-back checks + optional live check and reference comparison + report. Shared with the interactive tool.</summary>
    public static async Task<CreateResult> WriteAndVerify(CheckResult? check, CustomerFolder data, MigrationPlan plan, GenerationResult generated, string outputRoot,
        bool replace, string templateDir, string logsDir, string backupsDir, string? reference, string? baseUrl)
    {
        var site = plan.Site!;
        var (written, backup) = SiteWriter.Write(generated, outputRoot, site.Folder, templateDir, replace, backupsDir);
        var checks = Verifier.VerifyFiles(plan, outputRoot);
        if (!string.IsNullOrWhiteSpace(baseUrl)) checks.AddRange(await Verifier.SmokeTest(plan, baseUrl));
        var diffs = string.IsNullOrWhiteSpace(reference) ? null : Verifier.CompareWithReference(written, reference);
        Directory.CreateDirectory(logsDir);
        var reportPath = Path.Combine(logsDir, $"{site.Folder}-{DateTime.Now:yyyyMMdd-HHmmss}-report.md");
        File.WriteAllText(reportPath, Verifier.Report(plan, data, checks, diffs, reference)); // no personal data or secrets in the report

        var failed = checks.Where(c => !c.Pass).ToList();
        var dataDiffs = diffs?.Where(d => !d.Cosmetic).ToList() ?? [];
        var status = failed.Count > 0 || dataDiffs.Count > 0 ? "FAILED" : plan.ActionItems.Count > 0 ? "GENERATED - ACTION ITEMS OPEN" : "COMPLETE";
        return new CreateResult(status, check, written, backup, checks.Count, failed.Count,
            Redact(failed.Take(20).Select(c => $"{c.Area} {c.Item}: expected '{c.Expected}', actual '{c.Actual}'").ToList()),
            diffs is null ? null : dataDiffs.Count, diffs is null ? null : diffs.Count - dataDiffs.Count,
            Redact(dataDiffs.Take(20).Select(d => $"{d.File} | {d.Item}").ToList()), reportPath);
    }

    // ---- redaction: what the agent may see ---------------------------------------------------------------
    [GeneratedRegex(@"\d{9,}")] private static partial Regex LongNumber();
    [GeneratedRegex(@"[A-Za-z0-9._%+'-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}")] private static partial Regex Email();

    /// <summary>Masks merchant IDs and other long numbers to their last 4 digits and removes email addresses.</summary>
    public static string Redact(string s) =>
        Email().Replace(LongNumber().Replace(s, m => new string('*', m.Length - 4) + m.Value[^4..]), "[email removed]");

    static List<string> Redact(IEnumerable<string> lines) => lines.Select(Redact).ToList();
}
