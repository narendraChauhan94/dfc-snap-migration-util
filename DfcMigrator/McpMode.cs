using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DfcMigrator;

/// <summary>
/// "dfcmigrate mcp": the tool server of the Claude Desktop plugin, over stdio. stdout carries the protocol, so nothing else may print to it.
/// The agent chooses the data folder and customer; where the portal is written (Clients folder) and the live-test URL come only from
/// dfcmigrate.settings.json, so the agent can't write anywhere else.
/// </summary>
static class McpMode
{
    public static async Task<int> Run(Workspace ws)
    {
        DfcTools.Ws = ws;
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace); // stderr only
        builder.Services.AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly();
        await builder.Build().RunAsync();
        return 0;
    }
}

[McpServerToolType]
public static class DfcTools
{
    internal static Workspace Ws = null!;

    static string Out(object o) => JsonSerializer.Serialize(o, Operations.Json);

    static string ClientsFolder() =>
        Ws.Settings.GetValueOrDefault("clientsFolder") is { Length: > 0 } c && Directory.Exists(c) ? c
            : throw new InvalidOperationException($"No SNAP Clients folder is configured. Set \"clientsFolder\" in {Path.Combine(Ws.Home, "dfcmigrate.settings.json")} (or run dfcmigrate.exe once interactively).");

    [McpServerTool(Name = "list_customers", ReadOnly = true)]
    [Description("Lists the customers in a DFC customer data folder (one or all customers), with which data each one has. Returns at most 25 matches - pass `search` (part of the name, or an exact DFC ClubID) to narrow it. Read-only.")]
    public static string ListCustomers(
        [Description("Full path of the customer data folder the user gave")] string folder,
        [Description("Optional: part of the customer name, or a DFC ClubID")] string? search = null) =>
        Out(Operations.ListCustomers(folder, search));

    [McpServerTool(Name = "check_customer", ReadOnly = true)]
    [Description("Dry run for one customer: validates the data, plans the portal (pages, branches, product counts) and lists errors, warnings, info and action items. Writes nothing. Also returns where create_portal would write and whether a portal already exists there.")]
    public static string CheckCustomer(
        [Description("Full path of the customer data folder")] string folder,
        [Description("DFC ClubID of the customer (from list_customers). 0 = the only customer in a one-customer folder")] int customerId)
    {
        var check = Operations.Check(folder, customerId, Ws.TemplateDir);
        string? target = null;
        try { target = check.SiteFolder is null ? null : Path.Combine(ClientsFolder(), check.SiteFolder); }
        catch (InvalidOperationException ex) { return Out(new { check, setupProblem = ex.Message }); }
        return Out(new { check, writesTo = target, portalExists = target is not null && Directory.Exists(target) });
    }

    [McpServerTool(Name = "create_portal", Destructive = true)]
    [Description("Creates the customer's SNAP V2 portal in the configured SNAP Clients folder, then re-reads and checks every file, optionally compares with a hand-built reference client and tests the running site. Refuses if the data has errors. Needs the user's approval. If a portal already exists it is only replaced when replace=true (the old copy is moved to Backups).")]
    public static async Task<string> CreatePortal(
        [Description("Full path of the customer data folder")] string folder,
        [Description("DFC ClubID of the customer. 0 = the only customer in a one-customer folder")] int customerId,
        [Description("Replace an existing portal (only when the user explicitly agreed)")] bool replace = false,
        [Description("Optional: folder of a hand-built client of the same customer to compare with")] string? referenceFolder = null)
    {
        if (referenceFolder is { Length: > 0 } && !Directory.Exists(referenceFolder)) return Out(new { status = "REFERENCE FOLDER NOT FOUND", referenceFolder });
        var r = await Operations.Create(folder, customerId, ClientsFolder(), replace, Ws.TemplateDir, Ws.LogsDir, Ws.BackupsDir,
            string.IsNullOrWhiteSpace(referenceFolder) ? null : referenceFolder, Ws.Settings.GetValueOrDefault("baseUrl"));
        return Out(r);
    }
}
