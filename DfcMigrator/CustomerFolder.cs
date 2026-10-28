using System.Text;
using System.Text.Json;
using ClosedXML.Excel;

namespace DfcMigrator;

// DFC portal (Club table row). ClubId groups the portals of one customer.
public record Club(int Id, int ClubId, string Name, string ClubUrl, string Description, string Welcome, string NotifyEmail, string Email,
    string FinalMessage, string TermsUpload, string Address, int[] CollectionDays);

// DFC venue (branch candidate). PortalId = Club.Id; ClubRef = the site ref plans hang off.
public record Venue(int Id, int PortalId, string Name, string ClubRef, string[] AddressLines, string Telephone, string Email,
    string DisplayEmail, string Welcome);

public record Plan(int Id, int PortalId, string Name, decimal Fee, int Term, decimal JoinFee, string LetterType, string ClubRef,
    int Active, string PromoCode, string EndDate, string ApiFrequency, string ApiDuration, int CardPayOneOff, int CategoryId, int MinAge);

public record Category(int Id, string Name, int SortOrder);

public record Design(int PortalId, string ButtonColour, string GreyButtonColour, string ErrorColour, string InfoIconColour, string ErrorIconColour, string Logo,
    string BackgroundColour, string TextColour, string HeaderTextColour, string InternalBGColour, string LogoAlign, string BoxStyle, string GlowThickness, string BorderThickness);

/// <summary>One merchant row (one per portal x branch), from the Merchant HNK sheet or the merchant JSON. <see cref="Cell"/> = source location for messages.
/// Sheet rows carry the DFC customer in ClubId; JSON rows carry the portal in PortalId (see DATA_FORMAT.md).</summary>
public record MerchantRow(int Row, string UrlType, string ClubId, string ClubUrl, string VenueId, string SiteRef, string BranchName,
    string MerchantId, bool MerchantIsText, string BeneficiaryId, string NewUrl, string Integration, string Source = "Merchant sheet", string PortalId = "")
{
    public string Cell(string column) => $"{Source} row {Row} ({column})";
}

/// <summary>One customer in an all-customer folder, for the pick list.</summary>
public record CustomerSummary(int CustomerId, string Name, int Portals, int Venues, int Plans, bool HasDesign, int MerchantRows)
{
    public List<string> Missing => new[] { (Venues == 0, "venues"), (Plans == 0, "plans"), (!HasDesign, "design"), (MerchantRows == 0, "merchant IDs") }
        .Where(m => m.Item1).Select(m => m.Item2).ToList();
}

public class DfcDataException(string message) : Exception(message);

/// <summary>
/// The customer's migration data folder. Files are recognised by their content (the columns they contain),
/// never by name, so every customer's folder can be named differently.
/// </summary>
public class CustomerFolder
{
    public string Path { get; init; } = "";
    public List<Club> Clubs { get; } = [];
    public List<Venue> Venues { get; } = [];
    public List<Plan> Plans { get; } = [];
    public List<Category> Categories { get; } = [];
    public List<Design> Designs { get; } = [];
    public List<MerchantRow> Merchant { get; } = [];
    public string? LogoFile { get; set; }
    /// <summary>Logos\&lt;portal ID&gt;.jpg|png in an all-customer folder, keyed by portal ID.</summary>
    public Dictionary<int, string> LogosByPortal { get; } = [];
    /// <summary>File name -> recognised role, for the operator report.</summary>
    public List<(string File, string Role)> Files { get; } = [];
    /// <summary>File name -> number of records read, checked against _manifest.json.</summary>
    public Dictionary<string, int> RowCounts { get; } = new(StringComparer.OrdinalIgnoreCase);
    public bool HasManifest { get; private set; }
    public List<string> Errors { get; } = [];
    public List<string> Warnings { get; } = [];
    public bool IsValid => Errors.Count == 0;

    static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".gif"];

    // A JSON export's role is decided by columns only that export has.
    static readonly (string Role, string[] Columns)[] JsonRoles =
    [
        ("Club (portals)", ["ClubURL", "ClubKey", "Name"]),
        ("Branches (venues)", ["LinkedClubRefs", "ClubRef", "HideVenue"]),
        ("Plans", ["Fee", "LetterType", "api_duration"]),
        ("Categories", ["Category", "ParentCategoryID"]),
        ("Design", ["ButtonColour", "Logo"]),
        ("Merchant IDs (JSON)", ["MerchantHNK", "NewUrl", "VenueID"]),
    ];
    const string Manifest = "Manifest";
    // An export cut at a fixed row limit looks complete; a file of exactly N x 10,000 rows without a manifest is suspicious.
    const int SuspiciousRowBlock = 10_000;
    static readonly string[] MerchantColumns = ["URL Type", "Club_URL", "Branch_ID", "Site Ref (Venue-wise)", "Merchant HNK", "Beneficiary ID", "New URL"];

    public static CustomerFolder Load(string folder)
    {
        var cf = new CustomerFolder { Path = System.IO.Path.GetFullPath(folder) };
        if (!Directory.Exists(cf.Path)) { cf.Errors.Add($"Folder not found: {cf.Path}"); return cf; }

        string[] entries;
        try { entries = Directory.GetFiles(cf.Path); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) { cf.Errors.Add($"Cannot read the folder: {ex.Message}"); return cf; }
        foreach (var sub in Directory.GetDirectories(cf.Path))
        {
            if (!System.IO.Path.GetFileName(sub).Equals("Logos", StringComparison.OrdinalIgnoreCase)) { cf.Warnings.Add($"Sub-folder ignored: {System.IO.Path.GetFileName(sub)}"); continue; }
            // All-customer folder: Logos\<portal ID>.<ext>, one per portal.
            foreach (var img in Directory.GetFiles(sub).Where(f => ImageExtensions.Contains(System.IO.Path.GetExtension(f).ToLowerInvariant())))
                if (int.TryParse(System.IO.Path.GetFileNameWithoutExtension(img), out var portal) && !cf.LogosByPortal.TryAdd(portal, img))
                    cf.Errors.Add($"Ambiguous: more than one logo for portal {portal} in Logos.");
                else if (!int.TryParse(System.IO.Path.GetFileNameWithoutExtension(img), out _))
                    cf.Warnings.Add($"Logos\\{System.IO.Path.GetFileName(img)} ignored - name it <portal ID>{System.IO.Path.GetExtension(img)}.");
        }

        var roles = new Dictionary<string, List<string>>();
        var logos = new List<string>();
        foreach (var file in entries.Order())
        {
            var name = System.IO.Path.GetFileName(file);
            var ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
            // "~$<name>" is the lock file Office creates while a document is open - not data.
            if (name.StartsWith("~$")) { cf.Warnings.Add($"Ignored Office lock file {name} (a file in this folder is open in Excel - save it before running)."); continue; }
            string? role = null;
            try
            {
                if (ext == ".json") role = cf.ReadJson(file);
                else if (ext == ".xlsx" || (ext == "" && IsZip(file))) role = cf.ReadMerchantSheet(file);
                else if (ImageExtensions.Contains(ext)) { logos.Add(file); role = "Logo image"; }
            }
            catch (DfcDataException ex) { cf.Errors.Add(ex.Message); role = "UNREADABLE"; }
            if (role is null) { cf.Warnings.Add($"Not used (not recognised as a migration file): {name}"); role = "not used"; }
            cf.Files.Add((name, role));
            roles.TryAdd(role, []);
            roles[role].Add(name);
        }

        // Exactly one of each, except plans (several exports are merged) and the logo (optional).
        // Merchant IDs come from either the sheet or the JSON - one source only.
        foreach (var role in JsonRoles.Select(r => r.Role).Where(r => !r.StartsWith("Merchant")).Append("Merchant sheet or Merchant IDs (JSON)"))
        {
            var found = role.StartsWith("Merchant")
                ? roles.GetValueOrDefault("Merchant sheet", []).Concat(roles.GetValueOrDefault("Merchant IDs (JSON)", [])).ToList()
                : roles.GetValueOrDefault(role, []);
            if (found.Count == 0) cf.Errors.Add($"Missing: no {role} file found in the folder.");
            else if (found.Count > 1 && role != "Plans") cf.Errors.Add($"Ambiguous: {found.Count} {role} files ({string.Join(", ", found)}) - keep only one.");
        }
        if (roles.GetValueOrDefault(Manifest, []).Count > 1) cf.Errors.Add("Ambiguous: more than one manifest file - keep only one.");
        if (logos.Count == 1) cf.LogoFile = logos[0];
        else if (logos.Count > 1) cf.Errors.Add($"Ambiguous: {logos.Count} images ({string.Join(", ", logos.Select(System.IO.Path.GetFileName))}) - keep only the customer logo.");
        else if (cf.LogosByPortal.Count == 0) cf.Warnings.Add("No logo image in the folder - the Xplor Pay logo will be used.");

        foreach (var dup in cf.Plans.GroupBy(p => p.Id).Where(g => g.Count() > 1))
            cf.Errors.Add($"Plan {dup.Key} appears in more than one plan file.");
        cf.CheckCompleteness();
        return cf;
    }

    // ---- completeness: prove no export was cut off -------------------------------------------------------
    readonly List<(string File, int Rows)> manifestFiles = [];
    readonly Dictionary<string, int> manifestTotals = new(StringComparer.OrdinalIgnoreCase);

    void CheckCompleteness()
    {
        if (!HasManifest)
        {
            foreach (var (file, rows) in RowCounts.Where(f => f.Value > 0 && f.Value % SuspiciousRowBlock == 0))
                Warnings.Add($"{file} has exactly {rows:N0} records and there is no _manifest.json - the export may have been cut off at a row limit. Add a manifest to prove it is complete.");
            return;
        }
        foreach (var (file, rows) in manifestFiles)
            if (!RowCounts.TryGetValue(file, out var actual)) Errors.Add($"Manifest lists {file}, which is not in the folder.");
            else if (actual != rows) Errors.Add($"{file}: {actual:N0} records, manifest says {rows:N0} - the file is incomplete or out of date.");
        foreach (var file in RowCounts.Keys.Where(f => manifestFiles.All(m => !m.File.Equals(f, StringComparison.OrdinalIgnoreCase))))
            Errors.Add($"{file} is not listed in the manifest - re-export the manifest with every data file.");
        var actualTotals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Plans"] = Plans.Count, ["Clubs"] = Clubs.Count, ["Venues"] = Venues.Count, ["Categories"] = Categories.Count,
            ["Designs"] = Designs.Count, ["Merchant"] = Merchant.Count,
        };
        foreach (var (name, expected) in manifestTotals)
            if (!actualTotals.TryGetValue(name, out var actual)) Warnings.Add($"Manifest total '{name}' is not a known record type - ignored.");
            else if (actual != expected) Errors.Add($"{name}: {actual:N0} records across the files, manifest says {expected:N0} - data is missing or duplicated.");
    }

    // ---- all-customer folder: list customers, extract one --------------------------------------------------
    /// <summary>Customers in the folder (DFC ClubID groups a customer's portals), with what data each one has.</summary>
    public List<CustomerSummary> Customers() =>
        Clubs.GroupBy(c => c.ClubId).Select(g =>
        {
            var portals = g.Select(c => c.Id).ToHashSet();
            return new CustomerSummary(g.Key, g.First().Name, portals.Count, Venues.Count(v => portals.Contains(v.PortalId)),
                Plans.Count(p => portals.Contains(p.PortalId)), Designs.Any(d => portals.Contains(d.PortalId)), MerchantFor(g.Key, g.ToList()).Count());
        }).OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();

    IEnumerable<MerchantRow> MerchantFor(int customerId, List<Club> portals) =>
        Merchant.Where(m => m.PortalId != ""
            ? portals.Any(p => p.Id.ToString() == m.PortalId)
            : m.ClubId == customerId.ToString() || portals.Any(p => p.ClubUrl.Equals(m.ClubUrl, StringComparison.OrdinalIgnoreCase)));

    /// <summary>A copy holding only one customer's records - what the planner expects. Folder-level problems are carried over.</summary>
    public CustomerFolder ForCustomer(int customerId)
    {
        var cf = new CustomerFolder { Path = Path };
        cf.Files.AddRange(Files);
        cf.Errors.AddRange(Errors);
        cf.Warnings.AddRange(Warnings.Where(w => !w.StartsWith("No logo image")));
        var portals = Clubs.Where(c => c.ClubId == customerId).ToList();
        if (portals.Count == 0) { cf.Errors.Add($"Customer {customerId} is not in the Club file."); return cf; }
        var ids = portals.Select(c => c.Id).ToHashSet();
        cf.Clubs.AddRange(portals);
        cf.Venues.AddRange(Venues.Where(v => ids.Contains(v.PortalId)));
        cf.Plans.AddRange(Plans.Where(p => ids.Contains(p.PortalId)));
        cf.Categories.AddRange(Categories);
        cf.Designs.AddRange(Designs.Where(d => ids.Contains(d.PortalId)));
        cf.Merchant.AddRange(MerchantFor(customerId, portals));

        var who = $"Customer {customerId} ({portals[0].Name})";
        if (cf.Venues.Count == 0) cf.Errors.Add($"{who}: no venues in the Branches file.");
        if (cf.Plans.Count == 0) cf.Errors.Add($"{who}: no plans in the plan files - the plan export may be incomplete.");
        if (cf.Merchant.Count == 0) cf.Errors.Add($"{who}: no merchant/beneficiary rows.");

        // Logo: the web portal's Logos\<portal>.ext, else a single loose image in the folder.
        var webPortal = cf.Merchant.Where(m => m.UrlType.Equals("Websales", StringComparison.OrdinalIgnoreCase))
            .Select(m => portals.FirstOrDefault(p => p.Id.ToString() == m.PortalId || p.ClubUrl.Equals(m.ClubUrl, StringComparison.OrdinalIgnoreCase)))
            .FirstOrDefault(p => p is not null);
        cf.LogoFile = webPortal is not null && LogosByPortal.TryGetValue(webPortal.Id, out var logo) ? logo
            : portals.Select(p => LogosByPortal.GetValueOrDefault(p.Id)).FirstOrDefault(l => l is not null) ?? LogoFile;
        if (cf.LogoFile is null) cf.Warnings.Add("No logo image for this customer - the Xplor Pay logo will be used.");
        return cf;
    }

    static bool IsZip(string file)
    {
        using var fs = File.OpenRead(file);
        return fs.ReadByte() == 'P' && fs.ReadByte() == 'K';
    }

    string? ReadJson(string file)
    {
        var name = System.IO.Path.GetFileName(file);
        JsonDocument doc;
        try { doc = JsonDocument.Parse(File.ReadAllBytes(file).AsMemory().TrimUtf8Bom()); }
        catch (JsonException ex) { throw new DfcDataException($"{name}: not valid JSON ({ex.Message})."); }
        using (doc)
        {
            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("Files", out var files)) return ReadManifest(name, doc.RootElement, files);
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0) return null;
            var rows = doc.RootElement.EnumerateArray().Select((e, i) => new Row(name, i, e)).ToList();
            var first = rows[0];
            var role = JsonRoles.FirstOrDefault(r => r.Columns.All(first.Has)).Role;
            if (role is not null) RowCounts[name] = rows.Count;
            switch (role)
            {
                case "Club (portals)":
                    Clubs.AddRange(rows.Select(r => new Club(r.Int("ID"), r.Int("ClubID"), r.Str("Name"), r.Str("ClubURL"), r.Str("Description"),
                        r.Str("Welcome"), r.Str("NotifyEmail"), r.Str("Email"), r.Str("FinalMessage"), r.Str("TermsUpload"),
                        string.Join(", ", new[] { "Address1", "Address2", "Address3", "Town", "County", "Postcode" }.Select(r.Str).Where(s => s != "")),
                        new[] { 1, 8, 15, 25 }.Where(d => r.Str($"PayDate{d}") == "Y").ToArray())));
                    break;
                case "Branches (venues)":
                    Venues.AddRange(rows.Select(r => new Venue(r.Int("ID"), r.Int("ClubID"), r.Str("Name"), r.Str("ClubRef"),
                        new[] { "Address1", "Address2", "Address3", "Town", "County", "Postcode" }.Select(r.Str).Where(s => s != "").ToArray(),
                        r.Str("Telephone"), r.Str("Email"), r.Str("DisplayEmail"), r.Str("Welcome"))));
                    break;
                case "Plans":
                    Plans.AddRange(rows.Select(r => new Plan(r.Int("ID"), r.Int("ClubID"), r.Str("Name"), r.Dec("Fee"), r.Int("Term"), r.Dec("JoinFee"),
                        r.Str("LetterType"), r.Str("ClubRef"), r.Int("Active"), r.Str("PromoCode"), r.Str("EndDate"), r.Str("api_frequency"),
                        r.Str("api_duration"), r.Int("CardPayOneOff"), r.Int("CategoryID"), r.Int("MinAge"))));
                    break;
                case "Categories":
                    Categories.AddRange(rows.Select(r => new Category(r.Int("id"), r.Str("Category"), r.Int("CategorySortOrder"))));
                    break;
                case "Design":
                    Designs.AddRange(rows.Select(r => new Design(r.Int("ClubID"), r.Str("ButtonColour"), r.Str("GreyButtonColour"), r.Str("ErrorColour"),
                        r.Str("InfoIconColour"), r.Str("ErrorIconColour"), r.Str("Logo"), r.Str("BackgroundColour"), r.Str("TextColour"),
                        r.Str("HeaderTextColour"), r.Str("InternalBGColour"), r.Str("LogoAlign"), r.Str("BoxStyle"), r.Str("Glow_thickness"), r.Str("Border_thickness"))));
                    break;
                case "Merchant IDs (JSON)":
                    // IDs must be JSON strings: a number loses the 16th digit of a merchant ID (MerchantIsText = false -> planner error).
                    Merchant.AddRange(rows.Select(r => new MerchantRow(r.Index + 1, r.Str("UrlType"), "", r.Str("ClubUrl"), r.Str("VenueID"), r.Opt("SiteRef"), "",
                        r.Str("MerchantHNK"), r.IsString("MerchantHNK"), r.Str("BeneficiaryID"), r.Str("NewUrl"), r.Opt("ExtService"), name, r.Str("ClubID"))));
                    break;
            }
            return role;
        }
    }

    string ReadManifest(string name, JsonElement root, JsonElement files)
    {
        HasManifest = true;
        if (files.ValueKind != JsonValueKind.Array) throw new DfcDataException($"{name}: 'Files' must be a list of {{ \"File\", \"Rows\" }}.");
        foreach (var f in files.EnumerateArray())
        {
            if (!f.TryGetProperty("File", out var file) || !f.TryGetProperty("Rows", out var rows) || !rows.TryGetInt32(out var n))
                throw new DfcDataException($"{name}: every 'Files' entry needs \"File\" and a whole-number \"Rows\".");
            manifestFiles.Add((file.GetString() ?? "", n));
        }
        if (root.TryGetProperty("Totals", out var totals) && totals.ValueKind == JsonValueKind.Object)
            foreach (var t in totals.EnumerateObject())
                manifestTotals[t.Name] = t.Value.TryGetInt32(out var n) ? n : throw new DfcDataException($"{name}: total '{t.Name}' is not a whole number.");
        return Manifest;
    }

    string? ReadMerchantSheet(string file)
    {
        var name = System.IO.Path.GetFileName(file);
        XLWorkbook wb;
        try
        {
            // Read-only, shared: the sheet may be open in Excel.
            using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var ms = new MemoryStream();
            fs.CopyTo(ms);
            ms.Position = 0;
            wb = new XLWorkbook(ms);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { throw new DfcDataException($"{name}: cannot open as an Excel workbook ({ex.Message})."); }
        using (wb)
        {
            var ws = wb.Worksheets.First();
            var h = ws.Row(1).CellsUsed().ToDictionary(c => c.GetString().Trim(), c => c.Address.ColumnNumber, StringComparer.OrdinalIgnoreCase);
            if (!MerchantColumns.All(h.ContainsKey)) return null;
            string V(IXLRow row, string col) => h.TryGetValue(col, out var c) ? Text(row.Cell(c)) : "";
            foreach (var r in ws.RowsUsed().Skip(1))
            {
                var mid = r.Cell(h["Merchant HNK"]);
                Merchant.Add(new MerchantRow(r.RowNumber(), V(r, "URL Type"), V(r, "ClubID"), V(r, "Club_URL"), V(r, "Branch_ID"),
                    V(r, "Site Ref (Venue-wise)"), V(r, "Branch Name(Venues)"), Text(mid), mid.Value.IsText, V(r, "Beneficiary ID"),
                    V(r, "New URL"), V(r, "Ext Service - 3rd Party Integration")));
            }
            return "Merchant sheet";
        }
    }

    static string Text(IXLCell c) => c.Value.Type switch
    {
        XLDataType.Blank => "",
        XLDataType.Number => c.Value.GetNumber().ToString("0.############", System.Globalization.CultureInfo.InvariantCulture),
        _ => c.Value.ToString(System.Globalization.CultureInfo.InvariantCulture).Trim(),
    };

    record Row(string File, int Index, JsonElement E)
    {
        public bool Has(string k) => E.ValueKind == JsonValueKind.Object && E.TryGetProperty(k, out _);
        public string Opt(string k) => Has(k) ? Str(k) : "";
        public bool IsString(string k) => Has(k) && E.GetProperty(k).ValueKind == JsonValueKind.String;

        JsonElement Get(string k) => E.ValueKind == JsonValueKind.Object && E.TryGetProperty(k, out var v)
            ? v : throw new DfcDataException($"{File}: row {Index + 1} has no '{k}' field.");

        public string Str(string k) => Get(k) switch
        {
            { ValueKind: JsonValueKind.Null } => "",
            { ValueKind: JsonValueKind.String } v => FixMojibake(v.GetString()!.Trim()),
            var v => v.GetRawText(),
        };

        public int Int(string k)
        {
            var s = Str(k);
            return s == "" ? 0 : int.TryParse(s, out var n) ? n : throw new DfcDataException($"{File}: row {Index + 1} field '{k}' is not a whole number ('{s}').");
        }

        public decimal Dec(string k)
        {
            var s = Str(k);
            return s == "" ? 0 : decimal.TryParse(s, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var n)
                ? n : throw new DfcDataException($"{File}: row {Index + 1} field '{k}' is not a number ('{s}').");
        }
    }

    // DFC text was double-encoded on export ("Â£" instead of "£"). Reverse it only when the round trip is clean.
    public static string FixMojibake(string s)
    {
        if ((!s.Contains('Â') && !s.Contains('â')) || s.Any(c => c > 0xFF)) return s;
        var fixedText = Encoding.UTF8.GetString(Encoding.Latin1.GetBytes(s));
        return fixedText.Contains('�') ? s : fixedText;
    }
}

static class MemoryExt
{
    public static ReadOnlyMemory<byte> TrimUtf8Bom(this Memory<byte> m) =>
        m.Length >= 3 && m.Span[0] == 0xEF && m.Span[1] == 0xBB && m.Span[2] == 0xBF ? m[3..] : m;
}
