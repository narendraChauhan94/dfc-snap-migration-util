using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace DfcMigrator;

public record ProductPlan(string Id, string Name, bool OneOff, int ContractMonths, int? OneOffMonths, long FeePence, long JoinFeePence,
    int MinAge, string DescriptionHtml, int DfcPlanId, int CategorySort, string CategoryName, string ClubRef);

public record BranchPlan(string Id, Venue Venue, string MerchantId, string BeneficiaryId, string NotifyEmails, List<ProductPlan> Products);

/// <summary>A generated page. Web Sales and Internal pages are built from the same template page (as in the reference client).</summary>
public record PagePlan(Club Portal, string Type, string PageName, List<BranchPlan> Branches, int[] CollectionDays);

/// <summary>A config value plus the annotation comment the reference client writes when the value still needs confirming.</summary>
public record ConfigValue(string Value, string? Note = null);

/// <param name="Globals">Global settings per portal (Club.Id). The reference pages share the same values except each portal's own notes.</param>
public record SitePlan(string Folder, string CustomerName, Club WebClub, List<PagePlan> Pages, Dictionary<int, Dictionary<string, ConfigValue>> Globals,
    Dictionary<string, string> Colours, string? LogoFile);

public class MigrationPlan
{
    public SitePlan? Site { get; set; }
    public List<string> Errors { get; } = [];
    public List<string> Warnings { get; } = [];
    public List<string> Info { get; } = [];
    /// <summary>Values the data cannot supply (written into the config as NEEDS VALUE / CONFIRM notes). Not a failure, but not "complete" either.</summary>
    public List<string> ActionItems { get; } = [];
    public bool IsValid => Errors.Count == 0;
    public IEnumerable<PagePlan> Pages => Site?.Pages ?? [];
}

/// <summary>
/// Builds the SNAP V2 site from a customer folder, following the rules of the proven manual migration
/// (reference: Template\MidUlsterDistrictCouncil). Each rule names the data it comes from.
/// </summary>
public static class Planner
{
    public const string StandardThankYou = "Thank you for joining {0}. Your membership is now set up and a confirmation email is on its way to you.";
    static readonly Regex Merchant16 = new(@"^\d{16}$");
    static readonly Regex Hex6 = new("^#?[0-9A-Fa-f]{6}$");
    static readonly Regex NewUrlRx = new(@"/([^/]+)/([^/]+)\.aspx$", RegexOptions.IgnoreCase);

    public static MigrationPlan Build(CustomerFolder data, DateOnly today)
    {
        var plan = BuildPlan(data, today);
        foreach (var list in new[] { plan.Errors, plan.Warnings, plan.Info }) // every exit, including early error returns
        {
            var distinct = list.Distinct().ToList(); list.Clear(); list.AddRange(distinct);
        }
        return plan;
    }

    static MigrationPlan BuildPlan(CustomerFolder data, DateOnly today)
    {
        var plan = new MigrationPlan();
        var err = plan.Errors;
        if (!data.IsValid) { err.AddRange(data.Errors); return plan; }

        // ---- one customer only: every file must belong to the same DFC customer
        var customerIds = data.Clubs.Select(c => c.ClubId).Distinct().ToList();
        if (customerIds.Count != 1) { err.Add($"Club file contains {customerIds.Count} different customers (ClubID {string.Join(", ", customerIds)}) - one customer per folder."); return plan; }
        var portals = data.Clubs.ToDictionary(c => c.Id);
        foreach (var bad in data.Venues.Where(v => !portals.ContainsKey(v.PortalId)).Select(v => v.PortalId).Distinct())
            err.Add($"Branches file has venues for portal {bad}, which is not in the Club file (different customer?).");
        foreach (var bad in data.Plans.Where(p => !portals.ContainsKey(p.PortalId)).Select(p => p.PortalId).Distinct())
            err.Add($"Plan file has plans for portal {bad}, which is not in the Club file (different customer?).");
        foreach (var bad in data.Merchant.Where(m => m.ClubId != "" && m.ClubId != customerIds[0].ToString()).Take(3))
            err.Add($"{bad.Cell("ClubID")}: ClubID {bad.ClubId} is not this customer ({customerIds[0]}).");

        // ---- portals -> pages: the merchant sheet says which portals are migrated and their exact URL (= folder + page name)
        string? folder = null;
        var pages = new List<PagePlan>();
        foreach (var g in data.Merchant.GroupBy(m => m.ClubUrl, StringComparer.OrdinalIgnoreCase))
        {
            var first = g.First();
            var portal = data.Clubs.FirstOrDefault(c => c.ClubUrl.Equals(g.Key, StringComparison.OrdinalIgnoreCase));
            if (portal is null) { err.Add($"{first.Cell("Club_URL")}: portal '{g.Key}' is not in the Club file."); continue; }
            var type = first.UrlType.Equals("Websales", StringComparison.OrdinalIgnoreCase) ? "WebSales"
                     : first.UrlType.Equals("InClub", StringComparison.OrdinalIgnoreCase) ? "Internal" : null;
            if (type is null) { err.Add($"{first.Cell("URL Type")}: '{first.UrlType}' - expected Websales or InClub."); continue; }
            var urls = g.Select(m => m.NewUrl).Distinct().ToList();
            var m = urls.Count == 1 ? NewUrlRx.Match(urls[0]) : Match.Empty;
            if (!m.Success) { err.Add($"{first.Cell("New URL")}: portal '{g.Key}' needs one New URL like https://.../<Folder>/<Page>.aspx (found: {string.Join(" | ", urls)})."); continue; }
            if (folder is not null && !folder.Equals(m.Groups[1].Value, StringComparison.OrdinalIgnoreCase))
            { err.Add($"{first.Cell("New URL")}: folder '{m.Groups[1].Value}' differs from '{folder}' - one site folder per customer."); continue; }
            folder = m.Groups[1].Value;
            if (!Regex.IsMatch(folder, "^[A-Za-z0-9]+$") || !Regex.IsMatch(m.Groups[2].Value, "^[A-Za-z0-9_-]+$"))
            { err.Add($"{first.Cell("New URL")}: folder/page must be letters and digits ('{folder}/{m.Groups[2].Value}')."); continue; }
            if (portal.CollectionDays.Length == 0) err.Add($"Club file, portal {portal.Id}: no PayDate1/8/15/25 is 'Y' - collection days unknown.");
            pages.Add(new PagePlan(portal, type, m.Groups[2].Value, BuildBranches(g.ToList(), portal, data, today, plan), portal.CollectionDays));
        }
        foreach (var dup in pages.GroupBy(p => p.PageName, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
            err.Add($"Merchant sheet: two portals would both create {dup.Key}.aspx.");
        foreach (var c in data.Clubs.Where(c => pages.All(p => p.Portal.Id != c.Id)))
            plan.Info.Add($"Portal {c.Id} ({c.ClubUrl}) is not in the merchant sheet - not migrated.");
        var web = pages.FirstOrDefault(p => p.Type == "WebSales");
        if (pages.Count == 0 || folder is null) { err.Add("Merchant sheet: no portal to migrate."); return plan; }
        if (web is null) err.Add("Merchant sheet: no Websales portal - the site's global settings come from the web portal.");
        // Excel rounds 16-digit numbers to 15 significant digits; reformatting the column as Text afterwards keeps the rounded value.
        var ids = data.Merchant.Select(m => m.MerchantId).Where(id => Merchant16.IsMatch(id)).Distinct().ToList();
        if (ids.Count >= 3 && ids.All(id => id.EndsWith('0')))
            err.Add($"Merchant sheet: all {ids.Count} merchant IDs end in 0 - Excel rounded the 16th digit before the column became Text. Re-enter the exact IDs (from the merchant source, not this sheet) into the Text column.");
        if (err.Count > 0) return plan;

        // ---- global settings, from each page's own portal record (web and internal records hold the same values; notes differ)
        var club = web!.Portal;
        var globals = new Dictionary<int, Dictionary<string, ConfigValue>>();
        foreach (var page in pages)
        {
            var c = page.Portal;
            var g2 = new Dictionary<string, ConfigValue>
            {
                ["ClubName"] = new(c.Name), ["CompanyName"] = new(c.Name), ["PDFCompanyName"] = new(c.Name),
                ["ClubID"] = new("", "NEEDS VALUE: Harlands 5-digit club number (e.g. Marriott 30617) - not the DFC ID"),
                ["ConfirmationEmail_Branch_Addresses"] = new(EmailList(c.NotifyEmail != "" ? c.NotifyEmail : c.Email)),
                ["ProductPageBranchInfo"] = new(Balanced(c.Welcome, $"Club file (portal {c.Id}) Welcome", err)),
                ["ThankyouMessage"] = c.FinalMessage != ""
                    ? new(Balanced(c.FinalMessage, $"Club file (portal {c.Id}) FinalMessage", err))
                    : new(string.Format(StandardThankYou, c.Name), "CONFIRM wording with client - DFC FinalMessage was blank"),
                ["TermsAndConditionsDocument"] = new("", c.TermsUpload != "" ? $"NEEDS VALUE: DFC pointed at {c.TermsUpload}; no equivalent file in this folder yet" : "NEEDS VALUE: no terms document in the DFC export"),
                ["PDFCompanyAddress"] = c.Address != "" ? new(c.Address) : new("", "NEEDS VALUE: club address is blank in the DFC export"),
            };
            if (g2["ConfirmationEmail_Branch_Addresses"].Value == "") err.Add($"Club file, portal {c.Id}: NotifyEmail and Email are both empty.");
            foreach (var (key, v) in g2.Where(kv => kv.Value.Note is not null)) plan.ActionItems.Add($"{page.PageName} {key}: {v.Note}");
            globals[c.Id] = g2;
        }

        // ---- design: colours for ClubStyle.css (one stylesheet per site, from the web portal's design record)
        var design = data.Designs.FirstOrDefault(d => d.PortalId == club.Id);
        var colours = new Dictionary<string, string>();
        if (design is null) err.Add($"Design file has no record for portal {club.Id}.");
        else
        {
            foreach (var (name, value) in new[] { ("ButtonColour", design.ButtonColour), ("GreyButtonColour", design.GreyButtonColour), ("ErrorColour", design.ErrorColour),
                                                  ("InfoIconColour", design.InfoIconColour), ("ErrorIconColour", design.ErrorIconColour),
                                                  ("BackgroundColour", design.BackgroundColour), ("TextColour", design.TextColour),
                                                  ("HeaderTextColour", design.HeaderTextColour), ("InternalBGColour", design.InternalBGColour) })
                if (Hex6.IsMatch(value)) colours[name] = "#" + value.TrimStart('#').ToUpperInvariant();
                else err.Add($"Design file, portal {club.Id}: {name} '{value}' is not a 6-digit hex colour.");
            // Layout settings from the design record (reference: LogoAlign Left, BoxStyle Rounded, no glow, no border).
            colours["LogoAlign"] = design.LogoAlign.ToLowerInvariant() is "left" or "center" or "right" ? design.LogoAlign.ToLowerInvariant() : "";
            if (colours["LogoAlign"] == "") err.Add($"Design file, portal {club.Id}: LogoAlign '{design.LogoAlign}' - expected Left, Center or Right.");
            colours["BoxRadius"] = design.BoxStyle.ToLowerInvariant() switch { "rounded" => "4px", "square" => "0", _ => "" };
            if (colours["BoxRadius"] == "") err.Add($"Design file, portal {club.Id}: BoxStyle '{design.BoxStyle}' - expected Rounded or Square.");
            foreach (var (name, value) in new[] { ("Glow_thickness", design.GlowThickness), ("Border_thickness", design.BorderThickness) })
                if (value is not ("" or "0")) plan.Warnings.Add($"Design file, portal {club.Id}: {name} = {value} is not applied (the template, like the reference, has no glow/border box).");
            if (err.Count == 0)
            {
                // Derived shades, matching how the reference stylesheet was built.
                colours["ButtonTint"] = Mix(colours["ButtonColour"], 0.85);
                colours["ErrorTint"] = Mix(colours["ErrorColour"], 0.96);
                colours["GreyButtonHover"] = Shift(colours["GreyButtonColour"], -20);
            }
            foreach (var other in data.Designs.Where(d => d.PortalId != club.Id && pages.Any(p => p.Portal.Id == d.PortalId) && d with { PortalId = club.Id, Logo = design.Logo } != design))
                plan.Warnings.Add($"Design for portal {other.PortalId} differs from the web portal's; the site uses one stylesheet (web portal's colours).");
        }
        if (data.LogoFile is null && design?.Logo is { Length: > 0 } l) plan.ActionItems.Add($"Logo: DFC used '{l}' - put the customer logo image in the data folder.");

        plan.Site = new SitePlan(folder, club.Name, club, pages, globals, colours, data.LogoFile);
        return plan;
    }

    static List<BranchPlan> BuildBranches(List<MerchantRow> rows, Club portal, CustomerFolder data, DateOnly today, MigrationPlan plan)
    {
        var err = plan.Errors;
        var branches = new List<BranchPlan>();
        // Excel keeps 15 significant digits: a 16-digit merchant ID stored as a number has lost its last digit.
        foreach (var r in rows)
        {
            var venue = data.Venues.FirstOrDefault(v => v.Id.ToString() == r.VenueId && v.PortalId == portal.Id);
            if (venue is null) { err.Add($"{r.Cell("Branch_ID")}: venue {r.VenueId} is not in the Branches file for portal {portal.Id}."); continue; }
            // Site ref is an optional cross-check: the merchant JSON may omit it.
            if (r.SiteRef != "" && venue.ClubRef != r.SiteRef) { err.Add($"{r.Cell("Site Ref (Venue-wise)")}: '{r.SiteRef}' does not match venue {venue.Id} site ref {venue.ClubRef}."); continue; }
            if (!r.MerchantIsText) err.Add($"{r.Cell("Merchant HNK")}: stored as a number, so Excel may have dropped the 16th digit ({r.MerchantId}). Format the column as Text and re-enter the exact merchant ID.");
            else if (!Merchant16.IsMatch(r.MerchantId)) err.Add($"{r.Cell("Merchant HNK")}: '{r.MerchantId}' must be exactly 16 digits.");
            if (!Regex.IsMatch(r.BeneficiaryId, @"^\d+$")) err.Add($"{r.Cell("Beneficiary ID")}: '{r.BeneficiaryId}' must be a whole number.");

            var plans = data.Plans.Where(p => p.PortalId == portal.Id && p.ClubRef == venue.ClubRef).ToList();
            int promo = plans.Count(p => p.PromoCode != ""), inactive = plans.Count(p => p.Active != 1), expired = plans.Count(p => Expired(p, today));
            // Promo-code plans are left out: the migrated site runs with UsePromoCodes=False (reference: only test/legacy plans had codes).
            var eligible = plans.Where(p => p.Active == 1 && p.PromoCode == "" && !Expired(p, today)).ToList();
            if (promo + inactive + expired > 0)
                plan.Info.Add($"Portal {portal.Id} / {venue.Name}: excluded {promo} promo-code, {inactive} inactive and {expired} expired plan(s).");
            if (eligible.Count == 0) { plan.Info.Add($"Portal {portal.Id} / {venue.Name}: no eligible plans - branch not created."); continue; }

            var products = new List<ProductPlan>();
            foreach (var p in eligible)
                if (MapProduct(p, data, err) is { } prod) products.Add(prod);
            foreach (var dup in products.GroupBy(p => p.Id).Where(x => x.Count() > 1))
                err.Add($"Portal {portal.Id}: plans {string.Join(", ", dup.Select(d => d.DfcPlanId))} at site ref {venue.ClubRef} have the same name '{dup.First().Name}' - product ID would clash.");
            branches.Add(new BranchPlan(BranchId(venue.Name), venue, r.MerchantId, r.BeneficiaryId, EmailList(venue.Email), products));
            if (r.Integration != "") plan.Warnings.Add($"Portal {portal.Id} uses the '{r.Integration}' 3rd-party integration - not generated, configure it in SNAP.");
        }
        foreach (var dup in branches.GroupBy(b => b.Id).Where(g => g.Count() > 1))
            err.Add($"Portal {portal.Id}: venues {string.Join(", ", dup.Select(b => b.Venue.Name))} give the same branch ID '{dup.Key}'.");
        foreach (var dup in branches.SelectMany(b => b.Products).GroupBy(p => p.Id).Where(g => g.Count() > 1))
            err.Add($"Portal {portal.Id}: product ID '{dup.Key}' would be used by more than one branch.");
        return branches;
    }

    static ProductPlan? MapProduct(Plan p, CustomerFolder data, List<string> err)
    {
        string where = $"Plan {p.Id} '{p.Name}'";
        var cat = data.Categories.FirstOrDefault(c => c.Id == p.CategoryId);
        if (cat is null) { err.Add($"{where}: category {p.CategoryId} is not in the Categories file."); return null; }
        if (p.Fee <= 0) { err.Add($"{where}: fee {p.Fee} is not positive."); return null; }
        long fee = (long)Math.Round(p.Fee * 100, MidpointRounding.AwayFromZero);
        string price = (p.Fee).ToString("0.00", CultureInfo.InvariantCulture);
        // Category becomes the product-tab class used by JS/H_ProductGrouping.js (e.g. "Adult Memberships" -> Adult_Memberships).
        string tab = $"<span class=\"{Regex.Replace(cat.Name.Trim(), @"\s+", "_")}\"><strong>{p.Name}</strong><br />&pound;{price}";

        if (p.CardPayOneOff == 1 || p.LetterType == "One-Off")
        {
            var m = Regex.Match(p.ApiDuration, @"^(\d+)M$");
            if (!m.Success || int.Parse(m.Groups[1].Value) < 1) { err.Add($"{where}: one-off plan with unrecognised duration '{p.ApiDuration}'."); return null; }
            int months = int.Parse(m.Groups[1].Value);
            return new(ProductId(p), p.Name, true, 1, months, fee, Pence(p.JoinFee), p.MinAge, $"{tab} one-off payment for {months} months membership</span>", p.Id, cat.SortOrder, cat.Name, p.ClubRef);
        }
        if (p.ApiFrequency != "M") { err.Add($"{where}: recurring frequency '{p.ApiFrequency}' is not supported (only monthly 'M' is in the proven process)."); return null; }
        int term = Math.Max(p.Term, 1);
        var html = term > 1 ? $"{tab} per month for a minimum of {term} months</span>" : $"{tab} per month</span>";
        return new(ProductId(p), p.Name, false, term, null, fee, Pence(p.JoinFee), p.MinAge, html, p.Id, cat.SortOrder, cat.Name, p.ClubRef);
    }

    static long Pence(decimal v) => (long)Math.Round(v * 100, MidpointRounding.AwayFromZero);

    static bool Expired(Plan p, DateOnly today) =>
        p.EndDate.Length >= 10 && DateOnly.TryParse(p.EndDate[..10], CultureInfo.InvariantCulture, out var end) && end < today;

    /// <summary>Product ID = site ref + plan name, e.g. 4130_adult_all_inclusive_monthly_dd_membership.</summary>
    public static string ProductId(Plan p) => $"{p.ClubRef}_{Slug(p.Name)}";

    /// <summary>Branch ID = venue name without "Leisure Centre", e.g. "Greenvale Leisure Centre (Annual)" -> greenvale_annual.</summary>
    public static string BranchId(string venueName)
    {
        var s = Slug(Regex.Replace(venueName, @"\bLeisure Centre\b", "", RegexOptions.IgnoreCase));
        if (s.Length > 40) s = s[..(s.LastIndexOf('_', 40) is > 0 and var cut ? cut : 40)];
        return s == "" || !char.IsLetter(s[0]) ? "b_" + s : s;
    }

    public static string Slug(string s) => Regex.Replace(s.ToLowerInvariant().Replace("&", " and "), "[^a-z0-9]+", "_").Trim('_');

    static string EmailList(string s) => string.Join(",", s.Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));

    static string Balanced(string html, string where, List<string> err)
    {
        var fixedHtml = HtmlFix.Balance(html, out var problem, out _);
        if (fixedHtml is null) { err.Add($"{where}: {problem}."); return ""; }
        return fixedHtml;
    }

    static string Mix(string hex, double towardsWhite) =>
        "#" + string.Concat(Channels(hex).Select(c => ((int)Math.Round(c + (255 - c) * towardsWhite)).ToString("x2")));

    static string Shift(string hex, int delta) =>
        "#" + string.Concat(Channels(hex).Select(c => Math.Clamp(c + delta, 0, 255).ToString("x2")));

    static IEnumerable<int> Channels(string hex) => Enumerable.Range(0, 3).Select(i => Convert.ToInt32(hex.Substring(1 + i * 2, 2), 16));
}
