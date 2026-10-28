using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using ClosedXML.Excel;
using Xunit;

namespace DfcMigrator.Tests;

[Collection("Console")]
public sealed class MigrationTests : IDisposable
{
    static readonly DateOnly Today = new(2026, 10, 19);
    readonly string dir = Directory.CreateTempSubdirectory("dfcmig-").FullName;
    public void Dispose() => Directory.Delete(dir, recursive: true);

    static string Root()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (Directory.Exists(Path.Combine(d.FullName, "Template", "GenericClubFastDD"))) return d.FullName;
        throw new DirectoryNotFoundException("DFCMigration folder not found above the test output folder.");
    }
    static string TemplateDir() => Path.Combine(Root(), "Template", "GenericClubFastDD");

    // ================================================================ regression against the proven manual migration

    // Rebuilds Mid Ulster from the real customer folder and compares with the hand-built reference client.
    // The sheet's merchant IDs are numbers (16th digit lost), so - exactly as an operator would - a copy of the folder
    // gets the merchant column re-entered as text with the exact IDs. The original data is never modified.
    [Fact]
    public void MidUlster_IsRebuiltExactlyLikeTheReferenceClient()
    {
        string data = Path.Combine(Root(), "MidUlsterDistrictCouncil"), reference = Path.Combine(Root(), "Template", "MidUlsterDistrictCouncil", "MidUlsterDistrictCouncil");
        if (!Directory.Exists(data) || !Directory.Exists(reference)) return; // reference material not present on this machine

        var copy = Path.Combine(dir, "data");
        Directory.CreateDirectory(copy);
        foreach (var f in Directory.GetFiles(data).Where(f => !Path.GetFileName(f).StartsWith("~$"))) File.Copy(f, Path.Combine(copy, Path.GetFileName(f)));
        File.Copy(Path.Combine(reference, "Images", "mid ulster.jpg"), Path.Combine(copy, "mid ulster.jpg"));
        var exact = ExactMerchantIds(reference, Path.Combine(copy, "MidUlster - Branches.json"));
        var sheet = Directory.GetFiles(copy, "*Merchant*").Single(f => !Path.GetFileName(f).StartsWith("~$"));
        using (var wb = new XLWorkbook(sheet))
        {
            var ws = wb.Worksheets.First();
            foreach (var row in ws.RowsUsed().Skip(1)) row.Cell(14).SetValue(exact[row.Cell(10).GetValue<string>()]);
            wb.Save();
        }

        var folder = CustomerFolder.Load(copy);
        Assert.True(folder.IsValid, string.Join("\n", folder.Errors));
        var plan = Planner.Build(folder, Today);
        Assert.True(plan.IsValid, string.Join("\n", plan.Errors));
        var result = SiteGenerator.Generate(plan.Site!, TemplateDir());
        Assert.True(result.IsValid, string.Join("\n", result.Errors));
        var (target, _) = SiteWriter.Write(result, Path.Combine(dir, "out"), plan.Site!.Folder, TemplateDir(), false, Path.Combine(dir, "backups"));

        Assert.All(Verifier.VerifyFiles(plan, Path.Combine(dir, "out")), c => Assert.True(c.Pass, $"{c.Item}: {c.Expected} vs {c.Actual}"));
        var diffs = Verifier.CompareWithReference(target, reference);
        Assert.Empty(diffs.Where(d => !d.Cosmetic).Select(d => $"{d.File} | {d.Item} | {d.Reference} | {d.Generated}"));
        // Known cosmetic differences: hand-picked hover shade, hand-typed "Leisure Services", two hand-picked branch IDs.
        Assert.True(diffs.Count <= 9, string.Join("\n", diffs.Select(d => $"{d.File} | {d.Item}")));
        Assert.Equal(4 * 2, plan.ActionItems.Count); // ClubID, thank-you, terms, address - per page, as annotated in the reference
    }

    static Dictionary<string, string> ExactMerchantIds(string reference, string branchesJson)
    {
        var venues = JsonDocument.Parse(File.ReadAllText(branchesJson)).RootElement.EnumerateArray()
            .Select(v => (Name: v.GetProperty("Name").GetString()!, Ref: v.GetProperty("ClubRef").GetString()!)).ToList();
        var map = new Dictionary<string, string>();
        foreach (var cfg in Directory.GetFiles(reference, "*.config"))
            foreach (var b in XDocument.Load(cfg).Descendants("Branch"))
                map[venues.First(v => v.Name == (string?)b.Element("Description")).Ref] = (string)b.Element("MerchantID")!;
        return map;
    }

    // ================================================================ synthetic customer folders

    sealed class Fixture
    {
        public List<object> Clubs = [], Venues = [], Plans = [], Categories = [], Designs = [];
        public List<object[]> Merchant = [];
    }

    static Fixture Customer()
    {
        var f = new Fixture();
        f.Clubs.Add(Club(10, "TestLeisure", "Test Leisure"));
        f.Clubs.Add(Club(11, "TestLeisure-int", "Test Leisure (Internal)"));
        f.Venues.Add(Venue(100, 10, "Northgate Leisure Centre", "501", "Northgate Leisure Centre"));
        f.Venues.Add(Venue(101, 10, "Northgate Leisure Centre (Annual)", "502", "Northgate Leisure Centre"));
        f.Venues.Add(Venue(102, 10, "Southbank Leisure Centre", "503", "Southbank Leisure Centre"));
        f.Venues.Add(Venue(110, 11, "Northgate Leisure Centre", "501", "Northgate Leisure Centre"));
        f.Categories.Add(new { id = 1, ClubID = 10, Category = "Adult Memberships", ParentCategoryID = 0, CategorySortOrder = 0 });
        f.Categories.Add(new { id = 2, ClubID = 10, Category = "Staff Memberships", ParentCategoryID = 0, CategorySortOrder = 0 });
        f.Plans.Add(Plan(1, 10, "501", "Adult Monthly DD", 30m, category: 1));
        f.Plans.Add(Plan(2, 10, "501", "Adult 12 Month DD", 25m, term: 12, category: 1));
        f.Plans.Add(Plan(3, 10, "502", "Adult 3 Month Membership", 90m, oneOff: "03M", category: 1));
        f.Plans.Add(Plan(4, 10, "502", "Promo Plan", 1m, promo: "TEST", category: 1));
        f.Plans.Add(Plan(5, 11, "501", "Staff Monthly DD", 20m, category: 2));
        f.Designs.Add(Design(10));
        f.Merchant.Add(MerchantRow("Websales", "TestLeisure", 100, "501", "1111000000000015", "7000001", "TestSite/TestSite"));
        f.Merchant.Add(MerchantRow("Websales", "TestLeisure", 101, "502", "1111000000000023", "7000002", "TestSite/TestSite"));
        f.Merchant.Add(MerchantRow("Websales", "TestLeisure", 102, "503", "1111000000000031", "7000003", "TestSite/TestSite")); // no plans -> not created
        f.Merchant.Add(MerchantRow("InClub", "TestLeisure-int", 110, "501", "1111000000000015", "7000001", "TestSite/TestSiteInternal"));
        return f;
    }

    static object Design(int portal, string button = "E12019", string grey = "535658", string background = "FFFFFF", string text = "000000",
        string headerText = "FFFFFF", string internalBg = "FFFFFF", string logoAlign = "Left", string box = "Rounded", string glow = "0") => new
    {
        ClubID = portal, ButtonColour = button, GreyButtonColour = grey, ErrorColour = "FF0000", InfoIconColour = "0074D9", ErrorIconColour = "FF4136",
        Logo = "x.jpg", BackgroundColour = background, TextColour = text, HeaderTextColour = headerText, InternalBGColour = internalBg,
        LogoAlign = logoAlign, BoxStyle = box, Glow_thickness = glow, Border_thickness = "0",
    };

    static object Club(int id, string url, string desc, int customer = 900, string name = "Test Leisure") => new
    {
        ID = id, ClubKey = "k", ClubID = customer, Name = name, Description = desc, ClubURL = url, Welcome = "<p>Welcome to Test Leisure</p>",
        NotifyEmail = "info@example.invalid", Email = "info@example.invalid", FinalMessage = "", TermsUpload = $"0{id}/Terms.docx",
        Address1 = "", Address2 = "", Address3 = "", Town = "", County = "", Postcode = "", PayDate1 = "Y", PayDate8 = "N", PayDate15 = "Y", PayDate25 = "N",
        ps_merchant_pwd = "never-read",
    };

    static object Venue(int id, int portal, string name, string clubRef, string address1) => new
    {
        ID = id, ClubID = portal, Name = name, ClubRef = clubRef, LinkedClubRefs = clubRef, HideVenue = 0, Address1 = address1, Address2 = "1 High Street",
        Address3 = "", Town = "Testtown", County = "", Postcode = "TT1 1TT", Telephone = "01234 567890", Email = "a@example.invalid, b@example.invalid",
        DisplayEmail = "", Welcome = "",
    };

    static object Plan(int id, int portal, string clubRef, string name, decimal fee, int term = 1, string oneOff = "", string promo = "", int category = 1) => new
    {
        ID = id, ClubID = portal, Name = name, Fee = fee, Term = term, JoinFee = 0m, LetterType = oneOff == "" ? "Monthly" : "One-Off", ClubRef = clubRef,
        Active = 1, PromoCode = promo, EndDate = "0000-00-00 00:00:00", api_frequency = oneOff == "" ? "M" : "A", api_duration = oneOff == "" ? "CO" : oneOff,
        CardPayOneOff = oneOff == "" ? 0 : 1, CategoryID = category, MinAge = 18,
    };

    static object[] MerchantRow(string type, string url, int venue, string siteRef, string mid, string beneficiary, string page) =>
        [type, url, venue, siteRef, mid, beneficiary, $"https://www.snapdda.co.uk/{page}.aspx"];

    /// <summary>Writes the fixture with deliberately non-standard file names - roles must come from content.</summary>
    string Write(Fixture f, bool merchantAsText = true, string? logo = "brand logo.png", bool merchantJson = false)
    {
        var d = Path.Combine(dir, "customer-" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(d);
        void J(string name, object rows) => File.WriteAllText(Path.Combine(d, name), JsonSerializer.Serialize(rows));
        J("a1.json", f.Clubs); J("venues export.json", f.Venues); J("p.json", f.Plans); J("cats.json", f.Categories); J("look.json", f.Designs);
        if (merchantJson)
        {
            // DATA_FORMAT.md section 1: ClubID = portal ID (looked up from the venue), IDs as strings.
            var portalOfVenue = JsonSerializer.SerializeToElement(f.Venues).EnumerateArray().ToDictionary(v => v.GetProperty("ID").GetInt32(), v => v.GetProperty("ClubID").GetInt32());
            J("merchant.json", f.Merchant.Select(m => new Dictionary<string, object>
            {
                ["ClubID"] = portalOfVenue[(int)m[2]].ToString(), ["VenueID"] = m[2].ToString()!, ["UrlType"] = m[0], ["ClubUrl"] = m[1], ["SiteRef"] = m[3],
                ["MerchantHNK"] = merchantAsText ? m[4] : decimal.Parse((string)m[4]), ["BeneficiaryID"] = m[5], ["NewUrl"] = m[6],
            }).ToList());
            if (logo is not null) File.WriteAllBytes(Path.Combine(d, logo), [0x89, 0x50, 0x4E, 0x47]);
            return d;
        }
        using (var wb = new XLWorkbook())
        {
            var ws = wb.AddWorksheet("Sheet1");
            string[] h = ["URL Type", "ClubID", "Club_URL", "Branch_ID", "Site Ref (Venue-wise)", "Merchant HNK", "Beneficiary ID", "New URL", "Ext Service - 3rd Party Integration"];
            for (int i = 0; i < h.Length; i++) ws.Cell(1, i + 1).Value = h[i];
            int r = 2;
            foreach (var m in f.Merchant)
            {
                ws.Cell(r, 1).Value = (string)m[0]; ws.Cell(r, 2).Value = 900; ws.Cell(r, 3).Value = (string)m[1]; ws.Cell(r, 4).Value = (int)m[2];
                ws.Cell(r, 5).Value = (string)m[3];
                if (merchantAsText) ws.Cell(r, 6).SetValue((string)m[4]); else ws.Cell(r, 6).Value = double.Parse((string)m[4]);
                ws.Cell(r, 7).Value = (string)m[5]; ws.Cell(r, 8).Value = (string)m[6];
                r++;
            }
            wb.SaveAs(Path.Combine(d, "merchant details.xlsx"));
        }
        if (logo is not null) File.WriteAllBytes(Path.Combine(d, logo), [0x89, 0x50, 0x4E, 0x47]);
        return d;
    }

    static XElement Config(GenerationResult r, string rel) => XDocument.Parse(System.Text.Encoding.UTF8.GetString(r.Files.Single(f => f.RelPath == rel).Content)).Root!;
    static string Text(GenerationResult r, string rel) => System.Text.Encoding.UTF8.GetString(r.Files.Single(f => f.RelPath == rel).Content);

    (MigrationPlan Plan, GenerationResult Result) Run(string folder)
    {
        var data = CustomerFolder.Load(folder);
        Assert.True(data.IsValid, string.Join("\n", data.Errors));
        var plan = Planner.Build(data, Today);
        Assert.True(plan.IsValid, string.Join("\n", plan.Errors));
        var r = SiteGenerator.Generate(plan.Site!, TemplateDir());
        Assert.True(r.IsValid, string.Join("\n", r.Errors));
        return (plan, r);
    }

    // ---------------------------------------------------------------- folder detection

    [Fact]
    public void Files_AreRecognisedByContent_NotByName()
    {
        var data = CustomerFolder.Load(Write(Customer()));
        Assert.True(data.IsValid, string.Join("\n", data.Errors));
        Assert.Equal(["Branches (venues)", "Categories", "Club (portals)", "Design", "Logo image", "Merchant sheet", "Plans"], data.Files.Select(f => f.Role).Order());
        Assert.EndsWith("brand logo.png", data.LogoFile);
    }

    [Fact]
    public void MissingDuplicateUnreadableAndUnexpectedFiles_AreReported()
    {
        var d = Write(Customer());
        File.Delete(Path.Combine(d, "cats.json"));
        File.Copy(Path.Combine(d, "a1.json"), Path.Combine(d, "copy of club.json"));
        File.WriteAllText(Path.Combine(d, "broken.json"), "[{");
        File.WriteAllText(Path.Combine(d, "notes.txt"), "x");
        File.WriteAllBytes(Path.Combine(d, "second logo.jpg"), [1]);
        var data = CustomerFolder.Load(d);
        Assert.Contains(data.Errors, e => e == "Missing: no Categories file found in the folder.");
        Assert.Contains(data.Errors, e => e.StartsWith("Ambiguous: 2 Club (portals) files"));
        Assert.Contains(data.Errors, e => e.StartsWith("broken.json: not valid JSON"));
        Assert.Contains(data.Errors, e => e.StartsWith("Ambiguous: 2 images"));
        Assert.Contains(data.Warnings, w => w.Contains("notes.txt"));
        Assert.Contains("Folder not found", CustomerFolder.Load(Path.Combine(dir, "nope")).Errors[0]);
    }

    [Fact]
    public void MerchantIdsStoredAsNumbers_AreRefused_BecauseExcelDropsThe16thDigit()
    {
        var plan = Planner.Build(CustomerFolder.Load(Write(Customer(), merchantAsText: false)), Today);
        Assert.Contains(plan.Errors, e => e.StartsWith("Merchant sheet row 2 (Merchant HNK): stored as a number"));
    }

    [Fact]
    public void DataFromAnotherCustomer_IsRefused()
    {
        var f = Customer();
        f.Plans.Add(Plan(99, 4242, "501", "Someone else's plan", 10m));
        Assert.Contains(Planner.Build(CustomerFolder.Load(Write(f)), Today).Errors, e => e.Contains("portal 4242, which is not in the Club file"));
    }

    // ---------------------------------------------------------------- rules from the reference client

    [Fact]
    public void Branches_FollowTheMerchantSheet_AndNeedPlans()
    {
        var (plan, _) = Run(Write(Customer()));
        var web = plan.Site!.Pages.Single(p => p.Type == "WebSales");
        Assert.Equal(["northgate", "northgate_annual"], web.Branches.Select(b => b.Id));
        Assert.Contains(plan.Info, i => i.Contains("Southbank Leisure Centre: no eligible plans"));
        Assert.Equal("TestSiteInternal", plan.Site.Pages.Single(p => p.Type == "Internal").PageName);
    }

    [Fact]
    public void Products_FollowTheReferenceRules()
    {
        var (_, r) = Run(Write(Customer()));
        var p = Config(r, "TestSite.config").Descendants("Product").ToDictionary(x => (string)x.Attribute("ID")!);
        Assert.Equal(["501_adult_12_month_dd", "501_adult_monthly_dd", "502_adult_3_month_membership"], p.Keys.Order()); // promo plan left out
        var monthly = p["501_adult_monthly_dd"];
        Assert.Equal("", monthly.Element("PaymentMethods")!.Value);            // default DD, as in the reference
        Assert.Equal("1 15", string.Join(" ", monthly.Element("CollectionScheduleDaysOfMonth")!.Elements().Select(e => e.Value)));
        Assert.Equal("<span class=\"Adult_Memberships\"><strong>Adult Monthly DD</strong><br />&pound;30.00 per month</span>", (string?)monthly.Element("ProductDescriptionLongHTML"));
        Assert.Equal("<span class=\"Adult_Memberships\"><strong>Adult 12 Month DD</strong><br />&pound;25.00 per month for a minimum of 12 months</span>", (string?)p["501_adult_12_month_dd"].Element("ProductDescriptionLongHTML"));
        var oneOff = p["502_adult_3_month_membership"];
        Assert.Equal(("OneOff", "3", "Standard", "True"), (oneOff.Element("PaymentMethods")!.Value.Trim(), (string?)oneOff.Element("OneOffMembershipDuration"), (string?)oneOff.Element("PaymentCalculationBasis"), (string?)oneOff.Element("NonRenewable")));
        Assert.Equal("<span class=\"Adult_Memberships\"><strong>Adult 3 Month Membership</strong><br />&pound;90.00 one-off payment for 3 months membership</span>", (string?)oneOff.Element("ProductDescriptionLongHTML"));
    }

    [Fact]
    public void GlobalsBranchesPanelsAndStyle_FollowTheReferenceRules()
    {
        var (_, r) = Run(Write(Customer()));
        var cfg = Config(r, "TestSite.config");
        var g = cfg.Element("GlobalSettings")!;
        Assert.Equal("/Clients/TestSite/TestSite.aspx", (string?)g.Element("LogMessagePrefix"));
        Assert.Equal("Thank you for joining Test Leisure. Your membership is now set up and a confirmation email is on its way to you.", (string?)g.Element("ThankyouMessage"));
        Assert.Contains("CONFIRM wording", g.Element("ThankyouMessage")!.Nodes().OfType<XComment>().Single().Value);
        Assert.Contains("010/Terms.docx", g.Element("TermsAndConditionsDocument")!.Nodes().OfType<XComment>().Single().Value);
        Assert.Contains("011/Terms.docx", Config(r, "TestSiteInternal.config").Element("GlobalSettings")!.Element("TermsAndConditionsDocument")!.Nodes().OfType<XComment>().Single().Value);
        Assert.Equal("406492", (string?)g.Element("OriginatorNumber")); // DFC originator from the template

        var b = cfg.Element("Branches")!.Elements("Branch").First();
        Assert.Equal(("1111000000000015", "7000001", "a@example.invalid,b@example.invalid"),
            ((string?)b.Element("MerchantID"), (string?)b.Element("DDMS_Beneficiary"), (string?)b.Element("ConfirmationEmail_Branch_EmailAddresses")));

        var aspx = Text(r, "TestSite.aspx");
        Assert.Equal(aspx, Text(r, "TestSiteInternal.aspx")); // Internal page = web page
        Assert.Contains("<p><strong>Northgate Leisure Centre</strong><br />1 High Street<br />Testtown<br />TT1 1TT</p>", aspx); // name not repeated as address line 1
        Assert.Contains("src=\"Images/brand%20logo.png\"", aspx);
        var css = Text(r, "ClubStyle.css");
        Assert.Contains("#E12019", css);
        Assert.Contains("#3f4244", css); // grey hover = grey - 20 per channel (reference rule)
        Assert.DoesNotContain("{{", css);
        Assert.Contains(r.Files, f => f.RelPath == "Emails/TestSiteConfirmationEmail_html.txt");
    }

    // The stylesheet is built from the template + the customer's Design.json - not copied from any client.
    [Fact]
    public void Stylesheet_IsDrivenByDesignJson()
    {
        var f = Customer();
        f.Designs.Clear();
        f.Designs.Add(Design(10, button: "1A73E8", grey: "202124", background: "F8F9FA", text: "111111", headerText: "FEFEFE", internalBg: "EEEEEE",
            logoAlign: "Center", box: "Square", glow: "2"));
        var data = CustomerFolder.Load(Write(f));
        var plan = Planner.Build(data, Today);
        Assert.True(plan.IsValid, string.Join("\n", plan.Errors));
        Assert.Contains(plan.Warnings, w => w.Contains("Glow_thickness = 2 is not applied"));
        var css = Text(SiteGenerator.Generate(plan.Site!, TemplateDir()), "ClubStyle.css");
        foreach (var expected in new[] { "#1A73E8", "#202124", "#F8F9FA", "#111111", "#FEFEFE", "#EEEEEE", "text-align:center", "#0c0d10" /* grey hover = grey - 20 */ })
            Assert.Contains(expected, css);
        Assert.DoesNotContain("border-radius:4px", css.Replace(" ", ""));            // Square boxes
        foreach (var midUlster in new[] { "#E12019", "#535658", "#f4dedd" }) Assert.DoesNotContain(midUlster, css, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("{{", css);
    }

    [Fact]
    public void UnsupportedDesignValues_StopGeneration()
    {
        var f = Customer();
        f.Designs.Clear();
        f.Designs.Add(Design(10, logoAlign: "Middle", box: "Bevelled", background: "white"));
        var errors = Planner.Build(CustomerFolder.Load(Write(f)), Today).Errors;
        Assert.Contains(errors, e => e.Contains("LogoAlign 'Middle'"));
        Assert.Contains(errors, e => e.Contains("BoxStyle 'Bevelled'"));
        Assert.Contains(errors, e => e.Contains("BackgroundColour 'white' is not a 6-digit hex colour"));
    }

    // Seen on the real Mid Ulster sheet: column re-formatted as Text after Excel had already rounded 16 digits to 15.
    [Fact]
    public void MerchantIdsRoundedByExcel_StopGeneration()
    {
        var f = Customer();
        f.Merchant.Clear();
        f.Merchant.Add(MerchantRow("Websales", "TestLeisure", 100, "501", "1111000000000010", "7000001", "TestSite/TestSite"));
        f.Merchant.Add(MerchantRow("Websales", "TestLeisure", 101, "502", "1111000000000020", "7000002", "TestSite/TestSite"));
        f.Merchant.Add(MerchantRow("Websales", "TestLeisure", 102, "503", "1111000000000030", "7000003", "TestSite/TestSite"));
        var errors = Planner.Build(CustomerFolder.Load(Write(f)), Today).Errors;
        Assert.Contains(errors, e => e.Contains("all 3 merchant IDs end in 0"));
    }

    // ---- all-customer folder, merchant JSON, manifest (DATA_FORMAT.md) ---------------------------------------

    [Fact]
    public void MerchantJson_GivesTheSameBranchesAsTheSheet()
    {
        static string Branches(MigrationPlan p) => string.Join(";", p.Site!.Pages.SelectMany(pg => pg.Branches.Select(b => $"{pg.PageName}/{b.Id}/{b.MerchantId}/{b.BeneficiaryId}/{b.Products.Count}")));
        var sheet = Planner.Build(CustomerFolder.Load(Write(Customer())), Today);
        var json = Planner.Build(CustomerFolder.Load(Write(Customer(), merchantJson: true)), Today);
        Assert.True(json.IsValid, string.Join("\n", json.Errors));
        Assert.Equal(Branches(sheet), Branches(json));
    }

    [Fact]
    public void MerchantJson_NumericIdsAreRefused()
    {
        var errors = Planner.Build(CustomerFolder.Load(Write(Customer(), merchantAsText: false, merchantJson: true)), Today).Errors;
        Assert.Contains(errors, e => e.Contains("merchant.json row 1 (Merchant HNK): stored as a number"));
    }

    static Fixture TwoCustomers()
    {
        var f = Customer();
        f.Clubs.Add(Club(20, "OtherGym", "Other Gym", customer: 901, name: "Other Gym Ltd"));
        f.Venues.Add(Venue(200, 20, "Other Gym Central", "701", "Other Gym Central"));
        f.Designs.Add(Design(20, button: "112233"));
        f.Merchant.Add(MerchantRow("Websales", "OtherGym", 200, "701", "1111000000000099", "7000099", "OtherGym/OtherGym")); // no plans for customer 901
        return f;
    }

    [Fact]
    public void AllCustomerFolder_ListsCustomers_AndExtractsOne()
    {
        var all = CustomerFolder.Load(Write(TwoCustomers(), merchantJson: true));
        Assert.True(all.IsValid, string.Join("\n", all.Errors));
        Assert.Equal(["Other Gym Ltd:1/1/0/1 missing plans", "Test Leisure:2/4/5/4 complete"],
            all.Customers().Select(c => $"{c.Name}:{c.Portals}/{c.Venues}/{c.Plans}/{c.MerchantRows} {(c.Missing.Count == 0 ? "complete" : "missing " + string.Join(",", c.Missing))}"));
        Assert.Contains(Planner.Build(all, Today).Errors, e => e.Contains("2 different customers")); // the whole folder is never planned as one

        var one = all.ForCustomer(900);
        var plan = Planner.Build(one, Today);
        Assert.True(plan.IsValid, string.Join("\n", plan.Errors));
        Assert.DoesNotContain(one.Venues, v => v.PortalId == 20);
        Assert.DoesNotContain(plan.Site!.Pages.SelectMany(p => p.Branches), b => b.MerchantId == "1111000000000099");

        Assert.Contains(all.ForCustomer(901).Errors, e => e == "Customer 901 (Other Gym Ltd): no plans in the plan files - the plan export may be incomplete.");
        Assert.Contains(all.ForCustomer(555).Errors, e => e == "Customer 555 is not in the Club file.");
    }

    [Fact]
    public void AllCustomerFolder_UsesTheWebPortalsLogoFromTheLogosFolder()
    {
        var d = Write(TwoCustomers(), merchantJson: true, logo: null);
        Directory.CreateDirectory(Path.Combine(d, "Logos"));
        File.WriteAllBytes(Path.Combine(d, "Logos", "10.png"), [0x89, 0x50]);
        File.WriteAllBytes(Path.Combine(d, "Logos", "20.jpg"), [0xFF, 0xD8]);
        var all = CustomerFolder.Load(d);
        Assert.Equal("10.png", Path.GetFileName(all.ForCustomer(900).LogoFile));
        Assert.Equal("20.jpg", Path.GetFileName(all.ForCustomer(901).LogoFile));
    }

    [Fact]
    public void Manifest_ProvesTheExportIsComplete()
    {
        var d = Write(Customer(), merchantJson: true);
        void Manifest(int plans, bool listMerchant = true)
        {
            var files = new List<object> { new { File = "a1.json", Rows = 2 }, new { File = "venues export.json", Rows = 4 }, new { File = "p.json", Rows = 5 },
                new { File = "cats.json", Rows = 2 }, new { File = "look.json", Rows = 1 } };
            if (listMerchant) files.Add(new { File = "merchant.json", Rows = 4 });
            File.WriteAllText(Path.Combine(d, "_manifest.json"), JsonSerializer.Serialize(new { ExportedAt = "2026-10-27T15:33:00Z", Files = files, Totals = new { Plans = plans, Clubs = 2 } }));
        }

        Manifest(plans: 5);
        var ok = CustomerFolder.Load(d);
        Assert.True(ok.IsValid, string.Join("\n", ok.Errors));
        Assert.Contains(ok.Files, f => f.Role == "Manifest");

        Manifest(plans: 9);
        Assert.Contains(CustomerFolder.Load(d).Errors, e => e == "Plans: 5 records across the files, manifest says 9 - data is missing or duplicated.");
        Manifest(plans: 5, listMerchant: false);
        Assert.Contains(CustomerFolder.Load(d).Errors, e => e == "merchant.json is not listed in the manifest - re-export the manifest with every data file.");
    }

    [Fact]
    public void WithoutManifest_ARoundRowCountIsFlaggedAsPossiblyCutOff()
    {
        var f = Customer();
        for (int i = 0; i < 10_000 - 5; i++) f.Plans.Add(Plan(1000 + i, 99, "999", $"Other {i}", 1m));
        Assert.Contains(CustomerFolder.Load(Write(f, merchantJson: true)).Warnings, w => w.StartsWith("p.json has exactly 10,000 records and there is no _manifest.json"));
    }

    // ---- Claude Desktop plugin: what the agent may see, the tool operations, the credential guard -----------------

    [Fact]
    public void Redact_MasksMerchantIdsAndRemovesEmails()
    {
        Assert.Equal("MID ************1205 for [email removed], phone 028 7963 2796",
            Operations.Redact("MID 1111000000011205 for leisure@midulstercouncil.org, phone 028 7963 2796"));
    }

    [Fact]
    public void Check_IsReady_AndNeverShowsFullMerchantIdsOrEmails()
    {
        var check = Operations.Check(Write(Customer()), 0, TemplateDir());
        Assert.Equal("READY", check.Status);
        Assert.Equal(900, check.CustomerId);
        var json = System.Text.Json.JsonSerializer.Serialize(check, Operations.Json);
        Assert.DoesNotContain("1111000000000015", json);
        Assert.DoesNotContain("@example.invalid", json);
        Assert.Contains("************0015", json);
    }

    [Fact]
    public async Task Create_WritesOnce_AndReplacesOnlyWhenAsked()
    {
        var data = Write(Customer());
        var output = Path.Combine(dir, "clients");
        Directory.CreateDirectory(output);
        async Task<CreateResult> Create(bool replace) =>
            await Operations.Create(data, 0, output, replace, TemplateDir(), Path.Combine(dir, "logs"), Path.Combine(dir, "backups"), null, null);

        var first = await Create(false);
        Assert.Equal("GENERATED - ACTION ITEMS OPEN", first.Status);
        Assert.Equal(0, first.ChecksFailed);
        Assert.True(File.Exists(first.Report));
        Assert.StartsWith("PORTAL EXISTS", (await Create(false)).Status);
        var replaced = await Create(true);
        Assert.Equal("GENERATED - ACTION ITEMS OPEN", replaced.Status);
        Assert.NotNull(replaced.Backup);
    }

    [Fact]
    public void Guard_BlocksClaudesToolsFromDataFolders_FailsClosedOnBadInput()
    {
        var data = Write(Customer()); // the fixture's Club rows carry ps_merchant_pwd, like real DFC exports
        var other = Path.Combine(dir, "notes");
        Directory.CreateDirectory(other);
        string Decide(string tool, object input)
        {
            var sw = new StringWriter();
            Guard.Run(System.Text.Json.JsonSerializer.Serialize(new { tool_name = tool, cwd = dir, tool_input = input }), sw);
            return sw.ToString().Contains("\"deny\"") ? "deny" : "allow";
        }
        Assert.Equal("deny", Decide("Read", new { file_path = Path.Combine(data, "a1.json") }));
        Assert.Equal("deny", Decide("Bash", new { command = $"cat \"{Path.GetFileName(data)}/p.json\"" }));   // relative to cwd
        Assert.Equal("deny", Decide("Grep", new { pattern = "pwd", path = dir }));                             // parent of a data folder
        Assert.Equal("allow", Decide("Read", new { file_path = Path.Combine(other, "x.txt") }));
        Assert.Equal("allow", Decide("Read", new { file_path = Path.Combine(dir, "DATA_FORMAT.md") }));        // a file next to a data folder
        var ask = new StringWriter();
        Guard.Run(System.Text.Json.JsonSerializer.Serialize(new { tool_name = "mcp__plugin_dfc-migration_dfc__create_portal", tool_input = new { folder = data } }), ask);
        Assert.Contains("\"ask\"", ask.ToString());
        Assert.Equal("allow", Decide("Bash", new { command = "git status" }));
        var bad = new StringWriter();
        Guard.Run("﻿not json", bad);
        Assert.Contains("\"deny\"", bad.ToString());
    }

    // Early error returns used to skip de-duplication (interactive mode printed the same warning 15 times).
    [Fact]
    public void MessagesAreDistinct_EvenWhenPlanningStopsOnAnError()
    {
        var f = Customer();
        f.Merchant.Clear();
        f.Merchant.Add(MerchantRow("Websales", "TestLeisure", 100, "501", "1111000000000010", "7000001", "TestSite/TestSite"));
        f.Merchant.Add(MerchantRow("Websales", "TestLeisure", 101, "502", "1111000000000020", "7000002", "TestSite/TestSite"));
        f.Merchant.Add(MerchantRow("Websales", "TestLeisure", 102, "503", "1111000000000030", "7000003", "TestSite/TestSite"));
        var plan = Planner.Build(CustomerFolder.Load(Write(f)), Today);
        Assert.NotEmpty(plan.Errors);
        foreach (var list in new[] { plan.Errors, plan.Warnings, plan.Info }) Assert.Equal(list.Distinct().Count(), list.Count);
    }

    [Theory]
    [InlineData("Greenvale Leisure Centre (Annual)", "greenvale_annual")]
    [InlineData("Cookstown Leisure Centre (Swim Academy)", "cookstown_swim_academy")]
    [InlineData("Mid Ulster Sports Arena", "mid_ulster_sports_arena")]
    public void BranchIds(string venue, string id) => Assert.Equal(id, Planner.BranchId(venue));

    [Fact]
    public void UnsupportedOrAmbiguousPlans_StopGeneration()
    {
        var f = Customer();
        f.Plans.Add(Plan(6, 10, "501", "Adult Monthly DD", 30m));               // same name -> same product ID
        f.Plans.Add(Plan(7, 10, "502", "Weird One-Off", 10m, oneOff: "CO"));     // no duration
        var errors = Planner.Build(CustomerFolder.Load(Write(f)), Today).Errors;
        Assert.Contains(errors, e => e.Contains("have the same name 'Adult Monthly DD'"));
        Assert.Contains(errors, e => e.Contains("unrecognised duration 'CO'"));
    }

    // ---------------------------------------------------------------- writing safely

    [Fact]
    public void ExistingPortal_IsRefused_ReplaceBacksUpOutsideTheSite_RerunIsIdentical_TemplateUntouched()
    {
        var folder = Write(Customer());
        var templateHash = HashTree(TemplateDir());
        var output = Path.Combine(dir, "out");
        var (_, first) = Run(folder);
        var (target, _) = SiteWriter.Write(first, output, "TestSite", TemplateDir(), false, Path.Combine(dir, "backups"));
        var firstHash = HashTree(target);
        Assert.Throws<IOException>(() => SiteWriter.Write(first, output, "TestSite", TemplateDir(), false, Path.Combine(dir, "backups")));
        var (_, second) = Run(folder);
        var (_, backup) = SiteWriter.Write(second, output, "TestSite", TemplateDir(), true, Path.Combine(dir, "backups"));
        Assert.StartsWith(Path.Combine(dir, "backups"), backup);
        Assert.Equal(firstHash, HashTree(backup!));
        Assert.Equal(firstHash, HashTree(target));
        Assert.Equal(["TestSite"], Directory.GetDirectories(output).Select(Path.GetFileName));
        Assert.Equal(templateHash, HashTree(TemplateDir()));
    }

    [Fact]
    public async Task SmokeTest_ChecksStatusAndCustomerName()
    {
        var (plan, _) = Run(Write(Customer()));
        var ok = await Verifier.SmokeTest(plan, "http://snap.test", new Fake(System.Net.HttpStatusCode.OK, "<title>Xplorpay :: Test Leisure</title>"));
        Assert.All(ok, c => Assert.True(c.Pass, c.Item));
        var bad = await Verifier.SmokeTest(plan, "http://snap.test", new Fake(System.Net.HttpStatusCode.InternalServerError, "<title>Error</title>"));
        Assert.Equal(4, bad.Count(c => !c.Pass));
    }

    sealed class Fake(System.Net.HttpStatusCode code, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(body) });
    }

    // ---------------------------------------------------------------- HTML in DFC text fields

    [Theory]
    [InlineData("<b>Open late", "<b>Open late</b>", true)]
    [InlineData("<b>Adult</b><br>With a pro-rata payment", "<b>Adult</b><br>With a pro-rata payment", false)]
    [InlineData("Adult</b> monthly", "Adult monthly", true)]
    public void HtmlFix_BalancesTags(string input, string expected, bool changed)
    {
        Assert.Equal(expected, HtmlFix.Balance(input, out var problem, out var didChange));
        Assert.Null(problem);
        Assert.Equal(changed, didChange);
    }

    [Theory]
    [InlineData("<b>x</b><script>alert(1)</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    public void HtmlFix_RejectsUnsafeMarkup(string input) => Assert.Null(HtmlFix.Balance(input, out _, out _));

    static string HashTree(string root) => Convert.ToHexString(SHA256.HashData(
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Order()
            .SelectMany(f => System.Text.Encoding.UTF8.GetBytes(Path.GetRelativePath(root, f)).Concat(File.ReadAllBytes(f))).ToArray()));
}
