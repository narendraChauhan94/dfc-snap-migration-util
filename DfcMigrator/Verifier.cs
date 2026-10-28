using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace DfcMigrator;

public record Check(string Area, string Item, string Expected, string Actual)
{
    public bool Pass => Expected == Actual;
}

/// <summary>Generated site vs reference client. Cosmetic = no functional effect (e.g. internal branch ID spelling).</summary>
public record Difference(string File, string Item, string Reference, string Generated, bool Cosmetic);

public static class Verifier
{
    static readonly Regex Secret = new("key|password|secret|token", RegexOptions.IgnoreCase);

    // ------------------------------------------------------------------ generated files vs the plan (read back from disk)

    public static List<Check> VerifyFiles(MigrationPlan plan, string outputRoot)
    {
        var site = plan.Site!;
        var checks = new List<Check>();
        string dir = Path.Combine(outputRoot, site.Folder);
        checks.Add(new("Site", site.Folder, "folder exists", Directory.Exists(dir) ? "folder exists" : "missing"));
        if (!Directory.Exists(dir)) return checks;
        var files = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(dir, f).Replace('\\', '/')).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var page in site.Pages)
        {
            string p = page.PageName, cfgPath = Path.Combine(dir, p + ".config"), aspxPath = Path.Combine(dir, p + ".aspx");
            checks.Add(new("Page", $"{p}.aspx + .config", "present", File.Exists(cfgPath) && File.Exists(aspxPath) ? "present" : "missing"));
            if (!File.Exists(cfgPath) || !File.Exists(aspxPath)) continue;
            XElement root;
            try { root = XDocument.Load(cfgPath).Root!; } catch (Exception ex) { checks.Add(new("Config", p, "valid XML", ex.Message)); continue; }
            var g = root.Element("GlobalSettings")!;
            checks.Add(new("Config", $"{p} LogMessagePrefix", $"/Clients/{site.Folder}/{p}.aspx", (string?)g.Element("LogMessagePrefix") ?? "<missing>"));
            foreach (var (key, v) in site.Globals[page.Portal.Id]) checks.Add(new("Config", $"{p} {key}", Regex.Replace(v.Value, @"\s+", " ").Trim(), Text(g.Element(key))));

            var branches = root.Element("Branches")!.Elements("Branch").ToDictionary(b => (string)b.Attribute("ID")!);
            checks.Add(new("Branches", $"{p} branch IDs", string.Join(",", page.Branches.Select(b => b.Id).Order(StringComparer.Ordinal)), string.Join(",", branches.Keys)));
            foreach (var b in page.Branches.Where(b => branches.ContainsKey(b.Id)))
            {
                var e = branches[b.Id];
                checks.Add(new("Branches", $"{p}/{b.Id}", $"{b.MerchantId}|{b.BeneficiaryId}|{b.Venue.Name}|{string.Join(",", b.Products.Select(x => x.Id).Order(StringComparer.Ordinal))}",
                    $"{Text(e.Element("MerchantID"))}|{Text(e.Element("DDMS_Beneficiary"))}|{Text(e.Element("Description"))}|{string.Join(",", e.Element("ProductIDs")!.Elements().Select(i => i.Value))}"));
            }
            var products = root.Element("Products")!.Elements("Product").ToDictionary(x => (string)x.Attribute("ID")!);
            var expected = page.Branches.SelectMany(b => b.Products).ToList();
            checks.Add(new("Products", $"{p} product count", expected.Count.ToString(), products.Count.ToString()));
            foreach (var x in expected)
            {
                if (!products.TryGetValue(x.Id, out var e)) { checks.Add(new("Products", $"{p}/{x.Id}", "present", "missing")); continue; }
                string E(string n) => Text(e.Element(n));
                checks.Add(new("Products", $"{p}/{x.Id}",
                    $"{x.FeePence}|{x.ContractMonths}|{(x.OneOff ? "OneOff" : "")}|{x.OneOffMonths}|{x.MinAge}|{string.Join(" ", page.CollectionDays)}",
                    $"{E("MonthlyMembershipFeePence")}|{E("ContractLengthMonths")}|{E("PaymentMethods")}|{(x.OneOff ? E("OneOffMembershipDuration") : "")}|{E("YoungAgeLimitYears")}|{E("CollectionScheduleDaysOfMonth")}"));
            }

            var aspx = File.ReadAllText(aspxPath);
            var views = Regex.Matches(aspx, "<asp:View ID=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToHashSet();
            checks.Add(new("Page", $"{p} branch panels", "all present", page.Branches.All(b => views.Contains(b.Id)) ? "all present" : "missing " + string.Join(",", page.Branches.Where(b => !views.Contains(b.Id)).Select(b => b.Id))));
            var logo = Uri.UnescapeDataString(Regex.Match(aspx, "<img alt=\"<%= Globals.ClubName %>\".*?src=\"([^\"]+)\"").Groups[1].Value);
            checks.Add(new("Page", $"{p} header logo", "file present", files.Contains(logo) ? "file present" : $"missing ({logo})"));
            foreach (var (name, text) in new[] { ($"{p}.aspx", aspx), ($"{p}.config", File.ReadAllText(cfgPath)) })
            {
                var live = SiteGenerator.LiveContent(text, name);
                var markers = new[] { "<!-- REPLACE -->", "BRANCH VIEWS" }.Where(m => text.Contains(m, StringComparison.OrdinalIgnoreCase))
                    .Concat(new[] { "GenericClub", "Generic Club", "{{" }.Where(m => live.Contains(m, StringComparison.OrdinalIgnoreCase))).ToList();
                checks.Add(new("Markers", name, "none", markers.Count == 0 ? "none" : string.Join(", ", markers)));
            }
        }
        var css = Path.Combine(dir, "ClubStyle.css");
        checks.Add(new("Style", "ClubStyle.css colours filled", "yes", File.Exists(css) && !File.ReadAllText(css).Contains("{{") ? "yes" : "no"));
        return checks;
    }

    static string Text(XElement? e) => e is null ? "<missing>" : Regex.Replace(string.Concat(e.Nodes().OfType<XText>().Select(t => t.Value)) +
        string.Concat(e.Elements().Select(c => " " + c.Value)), @"\s+", " ").Trim();

    // ------------------------------------------------------------------ generated site vs a reference client folder

    public static List<Difference> CompareWithReference(string generatedDir, string referenceDir)
    {
        var diffs = new List<Difference>();
        var gen = Files(generatedDir);
        var refs = Files(referenceDir);
        foreach (var f in refs.Keys.Except(gen.Keys, StringComparer.OrdinalIgnoreCase)) diffs.Add(new(f, "file", "present", "missing", false));
        foreach (var f in gen.Keys.Except(refs.Keys, StringComparer.OrdinalIgnoreCase)) diffs.Add(new(f, "file", "absent", "extra", false));

        foreach (var f in refs.Keys.Intersect(gen.Keys, StringComparer.OrdinalIgnoreCase).Order())
        {
            string a = refs[f], b = gen[f];
            if (f.EndsWith(".config", StringComparison.OrdinalIgnoreCase)) diffs.AddRange(CompareConfig(f, a, b));
            else if (f.EndsWith(".aspx", StringComparison.OrdinalIgnoreCase)) diffs.AddRange(CompareAspx(f, a, b));
            else if (f.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
                diffs.AddRange(CompareLines(f, SiteGenerator.LiveContent(File.ReadAllText(a), f), SiteGenerator.LiveContent(File.ReadAllText(b), f)));
            else if (!File.ReadAllBytes(a).SequenceEqual(File.ReadAllBytes(b))) diffs.Add(new(f, "content", "", "differs", false));
        }
        return diffs;
    }

    static Dictionary<string, string> Files(string dir) =>
        Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).ToDictionary(f => Path.GetRelativePath(dir, f).Replace('\\', '/'), f => f, StringComparer.OrdinalIgnoreCase);

    static IEnumerable<Difference> CompareConfig(string file, string refPath, string genPath)
    {
        XElement r, g;
        try { r = XDocument.Load(refPath).Root!; g = XDocument.Load(genPath).Root!; }
        catch (Exception ex) { return [new(file, "XML", "", ex.Message, false)]; }
        var d = new List<Difference>();
        string Show(string tag, string v) => Secret.IsMatch(tag) ? "***" : Short(v);

        // Global settings: values and the NEEDS VALUE / CONFIRM notes.
        var rg = r.Element("GlobalSettings")!.Elements().ToDictionary(e => e.Name.LocalName);
        var gg = g.Element("GlobalSettings")!.Elements().ToDictionary(e => e.Name.LocalName);
        foreach (var tag in rg.Keys.Union(gg.Keys))
        {
            string a = rg.TryGetValue(tag, out var x) ? Text(x) : "<absent>", b = gg.TryGetValue(tag, out var y) ? Text(y) : "<absent>";
            if (a != b) d.Add(new(file, $"GlobalSettings/{tag}", Show(tag, a), Show(tag, b), false));
            string na = Notes(x), nb = Notes(y);
            if (na != nb) d.Add(new(file, $"GlobalSettings/{tag} note", Short(na), Short(nb), true));
        }

        // Products by ID, every field.
        var rp = r.Element("Products")!.Elements("Product").ToDictionary(e => (string)e.Attribute("ID")!);
        var gp = g.Element("Products")!.Elements("Product").ToDictionary(e => (string)e.Attribute("ID")!);
        foreach (var id in rp.Keys.Except(gp.Keys)) d.Add(new(file, $"Product {id}", "present", "missing", false));
        foreach (var id in gp.Keys.Except(rp.Keys)) d.Add(new(file, $"Product {id}", "absent", "extra", false));
        foreach (var id in rp.Keys.Intersect(gp.Keys))
            foreach (var tag in rp[id].Elements().Select(e => e.Name.LocalName).Union(gp[id].Elements().Select(e => e.Name.LocalName)))
            {
                string a = Text(rp[id].Element(tag)), b = Text(gp[id].Element(tag));
                if (a != b) d.Add(new(file, $"Product {id}/{tag}", Short(a), Short(b), false));
            }
        if (rp.Count == gp.Count && rp.Keys.SequenceEqual(gp.Keys) == false && rp.Keys.Order().SequenceEqual(gp.Keys.Order()))
            d.Add(new(file, "Product order", string.Join(",", rp.Keys.Take(3)) + "...", string.Join(",", gp.Keys.Take(3)) + "...", true));

        // Branches matched by Description (their IDs are internal and were hand-picked in the reference).
        var rb = r.Element("Branches")!.Elements("Branch").ToDictionary(e => Text(e.Element("Description")));
        var gb = g.Element("Branches")!.Elements("Branch").ToDictionary(e => Text(e.Element("Description")));
        foreach (var n in rb.Keys.Except(gb.Keys)) d.Add(new(file, $"Branch '{n}'", "present", "missing", false));
        foreach (var n in gb.Keys.Except(rb.Keys)) d.Add(new(file, $"Branch '{n}'", "absent", "extra", false));
        foreach (var n in rb.Keys.Intersect(gb.Keys))
        {
            if ((string?)rb[n].Attribute("ID") != (string?)gb[n].Attribute("ID"))
                d.Add(new(file, $"Branch '{n}' ID", (string)rb[n].Attribute("ID")!, (string)gb[n].Attribute("ID")!, true));
            foreach (var tag in rb[n].Elements().Select(e => e.Name.LocalName).Union(gb[n].Elements().Select(e => e.Name.LocalName)))
            {
                string a = Text(rb[n].Element(tag)), b = Text(gb[n].Element(tag));
                if (tag == "ConfirmationEmail_Branch_EmailAddresses") { a = Count(a); b = Count(b); } // compared, not printed (personal data)
                if (a != b) d.Add(new(file, $"Branch '{n}'/{tag}", Short(a), Short(b), false));
            }
        }
        return d;
    }

    static string Count(string emails) => emails == "" ? "none" : $"{emails.Split(',').Length} address(es) [{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(emails)))[..8]}]";

    static string Notes(XElement? e) => e is null ? "" : string.Join(" | ", e.Nodes().OfType<XComment>().Select(c => c.Value.Trim()));

    static IEnumerable<Difference> CompareAspx(string file, string refPath, string genPath)
    {
        string a = File.ReadAllText(refPath), b = File.ReadAllText(genPath);
        var d = new List<Difference>();
        // Branch panels compared by the venue name they show; the rest of the page compared as text.
        var ra = Views(a); var ga = Views(b);
        foreach (var n in ra.Keys.Except(ga.Keys)) d.Add(new(file, $"Branch panel '{n}'", "present", "missing", false));
        foreach (var n in ga.Keys.Except(ra.Keys)) d.Add(new(file, $"Branch panel '{n}'", "absent", "extra", false));
        foreach (var n in ra.Keys.Intersect(ga.Keys))
        {
            if (ra[n].Id != ga[n].Id) d.Add(new(file, $"Branch panel '{n}' ID", ra[n].Id, ga[n].Id, true));
            if (ra[n].Body != ga[n].Body) d.Add(new(file, $"Branch panel '{n}'", Short(ra[n].Body), Short(ga[n].Body), n == "GenericBranchInfo"));
        }
        d.AddRange(CompareLines(file + " (outside branch panels)", SiteGenerator.LiveContent(StripViews(a), file), SiteGenerator.LiveContent(StripViews(b), file)));
        return d;
    }

    record ViewInfo(string Id, string Body);

    static Dictionary<string, ViewInfo> Views(string aspx)
    {
        var map = new Dictionary<string, ViewInfo>();
        foreach (Match m in Regex.Matches(aspx, "<asp:View ID=\"([^\"]+)\"[^>]*>(.*?)</asp:View>", RegexOptions.Singleline))
        {
            var body = Regex.Replace(m.Groups[2].Value, @"\s+", " ").Trim();
            var name = m.Groups[1].Value == "GenericBranchInfo" ? "GenericBranchInfo" : WebUtility.HtmlDecode(Regex.Match(body, "<strong>(.*?)</strong>").Groups[1].Value);
            map.TryAdd(name, new(m.Groups[1].Value, body));
        }
        return map;
    }

    static string StripViews(string aspx) => Regex.Replace(aspx, "<asp:MultiView ID=\"BranchInfoPanel\".*?</asp:MultiView>", "<BranchInfoPanel/>", RegexOptions.Singleline);

    static IEnumerable<Difference> CompareLines(string file, string a, string b)
    {
        // Text files: whitespace-insensitive line comparison.
        // The same colour written differently (#fff / #FFFFFF) is the same value.
        static string Canon(string l) => Regex.Replace(l, @"#([0-9a-fA-F]{6}|[0-9a-fA-F]{3})\b", m =>
            "#" + (m.Groups[1].Value.Length == 3 ? string.Concat(m.Groups[1].Value.Select(c => $"{c}{c}")) : m.Groups[1].Value).ToLowerInvariant());
        string[] Lines(string s) => s.Replace("\r\n", "\n").Split('\n').Select(l => Canon(Regex.Replace(l, @"\s+", " ").Trim())).Where(l => l != "").ToArray();
        var la = Lines(a);
        var lb = Lines(b);
        var onlyRef = la.Except(lb).ToList();
        var onlyGen = lb.Except(la).ToList();
        // A line that differs only in a colour value is a visual shade difference, not data.
        static string NoColour(string l) => Regex.Replace(l, @"#[0-9a-fA-F]{6}\b", "#colour");
        for (int i = 0; i < Math.Max(onlyRef.Count, onlyGen.Count); i++)
        {
            string x = i < onlyRef.Count ? onlyRef[i] : "", y = i < onlyGen.Count ? onlyGen[i] : "";
            yield return new(file, "line", Short(x), Short(y), x != "" && y != "" && NoColour(x) == NoColour(y));
        }
    }

    static string Short(string s) => s.Length > 140 ? s[..137] + "..." : s;

    // ------------------------------------------------------------------ live site smoke test

    /// <summary>Each page answers 200 and shows the customer name in its &lt;title&gt; (proves SNAP loaded the generated config).</summary>
    public static async Task<List<Check>> SmokeTest(MigrationPlan plan, string baseUrl, HttpMessageHandler? handler = null)
    {
        using var http = new HttpClient(handler ?? new HttpClientHandler(), disposeHandler: handler is null) { Timeout = TimeSpan.FromSeconds(30) };
        var checks = new List<Check>();
        foreach (var page in plan.Pages)
        {
            var url = $"{baseUrl.TrimEnd('/')}/Clients/{plan.Site!.Folder}/{page.PageName}.aspx";
            try
            {
                using var resp = await http.GetAsync(url);
                var body = await resp.Content.ReadAsStringAsync();
                checks.Add(new("HTTP", $"{url} status", "200", ((int)resp.StatusCode).ToString()));
                var title = Regex.Match(body, "<title>(.*?)</title>", RegexOptions.Singleline | RegexOptions.IgnoreCase).Groups[1].Value;
                checks.Add(new("HTTP", $"{url} title shows customer", "yes", WebUtility.HtmlDecode(title).Contains(plan.Site.CustomerName) ? "yes" : $"no ('{title.Trim()}')"));
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { checks.Add(new("HTTP", $"{url} status", "200", ex.Message)); }
        }
        return checks;
    }

    // ------------------------------------------------------------------ report

    public static string Report(MigrationPlan plan, CustomerFolder data, List<Check> checks, List<Difference>? reference, string? referenceDir)
    {
        var s = plan.Site!;
        var sb = new StringBuilder();
        int failed = checks.Count(c => !c.Pass);
        int refData = reference?.Count(d => !d.Cosmetic) ?? 0;
        string status = failed > 0 || refData > 0 ? "FAILED" : plan.ActionItems.Count > 0 ? "GENERATED - ACTION ITEMS OPEN" : "COMPLETE";
        sb.AppendLine($"# Migration report - {s.CustomerName}").AppendLine();
        sb.AppendLine($"- **Status: {status}**");
        sb.AppendLine($"- Data folder: `{data.Path}`");
        sb.AppendLine($"- Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"- Site: `/Clients/{s.Folder}/` - {string.Join(", ", s.Pages.Select(p => $"{p.PageName}.aspx [{p.Type}] ({p.Branches.Count} branches, {p.Branches.Sum(b => b.Products.Count)} products)"))}");
        sb.AppendLine($"- Verification: {checks.Count - failed}/{checks.Count} checks passed");
        if (reference is not null) sb.AppendLine($"- Compared with reference `{referenceDir}`: {refData} data difference(s), {reference.Count - refData} cosmetic");
        sb.AppendLine().AppendLine("## Files used").AppendLine();
        foreach (var (f, role) in data.Files) sb.AppendLine($"- {role}: `{f}`");
        void List(string title, IEnumerable<string> items)
        {
            var l = items.ToList();
            if (l.Count == 0) return;
            sb.AppendLine().AppendLine($"## {title}").AppendLine();
            foreach (var x in l) sb.AppendLine($"- {x}");
        }
        List("Action items (values the data cannot supply)", plan.ActionItems);
        List("Warnings", plan.Warnings.Concat(data.Warnings));
        List("Information", plan.Info);
        if (failed > 0)
        {
            sb.AppendLine().AppendLine("## Failed checks").AppendLine().AppendLine("| Area | Item | Expected | Actual |").AppendLine("|---|---|---|---|");
            foreach (var c in checks.Where(c => !c.Pass)) sb.AppendLine($"| {c.Area} | {Cell(c.Item)} | {Cell(c.Expected)} | {Cell(c.Actual)} |");
        }
        if (reference is { Count: > 0 })
        {
            sb.AppendLine().AppendLine("## Differences from the reference client").AppendLine().AppendLine("| Kind | File | Item | Reference | Generated |").AppendLine("|---|---|---|---|---|");
            foreach (var d in reference.OrderBy(d => d.Cosmetic)) sb.AppendLine($"| {(d.Cosmetic ? "cosmetic" : "**DATA**")} | {d.File} | {Cell(d.Item)} | {Cell(d.Reference)} | {Cell(d.Generated)} |");
        }
        sb.AppendLine().AppendLine("## All checks").AppendLine().AppendLine("| Result | Area | Item | Expected | Actual |").AppendLine("|---|---|---|---|---|");
        foreach (var c in checks) sb.AppendLine($"| {(c.Pass ? "PASS" : "**FAIL**")} | {c.Area} | {Cell(c.Item)} | {Cell(c.Expected)} | {Cell(c.Actual)} |");
        return sb.ToString();
    }

    static string Cell(string s) => WebUtility.HtmlEncode(s).Replace("|", "\\|").Replace("\n", " ");
}
