using System.Text.Json;
using System.Text.RegularExpressions;

namespace DfcMigrator;

/// <summary>
/// "dfcmigrate guard": PreToolUse hook of the Claude Desktop plugin. DFC exports hold live credentials (ps_merchant_pwd, voyager_api_key ...)
/// and staff emails for every customer, so Claude's own Read/Grep/Glob/Bash/PowerShell tools must not open them - the plugin's tools
/// read the data and return only redacted summaries. Reads the hook JSON from stdin; prints a deny decision, or nothing to let the call run.
/// </summary>
public static class Guard
{
    // Fields only DFC customer data has. A folder with a top-level .json containing one of them is a DFC data folder.
    static readonly string[] Markers = ["\"ps_merchant_pwd\"", "\"voyager_api_key\"", "\"MerchantHNK\""];
    const int SniffBytes = 64 * 1024;

    public static int Run(string hookJson, TextWriter stdout)
    {
        string? blocked;
        try
        {
            // Input piped through PowerShell can start with a byte-order mark.
            using var doc = JsonDocument.Parse(hookJson.TrimStart('﻿', ' ', '\r', '\n', '\t'));
            var root = doc.RootElement;
            // create_portal writes into the SNAP Clients folder: always ask the user, even in auto mode.
            if (root.TryGetProperty("tool_name", out var tn) && tn.GetString()?.EndsWith("__create_portal") == true)
            {
                stdout.WriteLine(JsonSerializer.Serialize(new { hookSpecificOutput = new { hookEventName = "PreToolUse", permissionDecision = "ask",
                    permissionDecisionReason = "Create the SNAP portal? This writes files into the SNAP Clients folder (an existing portal is only replaced if replace=true; the old copy goes to Backups)." } }));
                return 0;
            }
            var cwd = root.TryGetProperty("cwd", out var c) ? c.GetString() ?? "" : "";
            blocked = root.TryGetProperty("tool_input", out var input) ? Candidates(input).Select(p => Resolve(p, cwd)).FirstOrDefault(p => p is not null && Protected(p)) : null;
        }
        catch (JsonException ex)
        {
            // Fails closed: unreadable hook input must not quietly let a call through to the credentials.
            return Deny(stdout, $"The dfc-migration guard could not read the hook input ({ex.Message}).");
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            blocked = null; // a malformed path in the input can't point at a real data folder
            Console.Error.WriteLine($"dfcmigrate guard: {ex.Message}");
        }
        return blocked is null ? 0 : Deny(stdout, $"{blocked} is (or is next to) a DFC customer data folder, which holds live credentials and staff emails. " +
            "Use the dfc-migration tools (list_customers, check_customer, create_portal) - they return redacted summaries.");
    }

    static int Deny(TextWriter stdout, string reason)
    {
        stdout.WriteLine(JsonSerializer.Serialize(new { hookSpecificOutput = new { hookEventName = "PreToolUse", permissionDecision = "deny", permissionDecisionReason = reason } }));
        return 0;
    }

    /// <summary>Path-like strings in the tool input: file_path / path, the fixed part of a Glob pattern, and tokens of a shell command.</summary>
    static IEnumerable<string> Candidates(JsonElement input)
    {
        if (input.ValueKind != JsonValueKind.Object) yield break;
        foreach (var key in new[] { "file_path", "path", "notebook_path" })
            if (input.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String) yield return v.GetString()!;
        if (input.TryGetProperty("pattern", out var pat) && pat.ValueKind == JsonValueKind.String && pat.GetString()!.IndexOfAny(['/', '\\']) >= 0)
            yield return FixedPart(pat.GetString()!);
        if (input.TryGetProperty("command", out var cmd) && cmd.ValueKind == JsonValueKind.String)
            // Quoted strings, then bare words that look like paths.
            foreach (Match m in Regex.Matches(cmd.GetString()!, "\"([^\"]+)\"|'([^']+)'|([^\\s\"';|&()<>]+)"))
            {
                var token = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value;
                if (token.IndexOfAny(['/', '\\']) >= 0 || Regex.IsMatch(token, "^[A-Za-z]:")) yield return FixedPart(token);
            }
    }

    static string FixedPart(string pattern)
    {
        var wild = pattern.IndexOfAny(['*', '?', '[', '{']);
        return wild < 0 ? pattern : pattern[..wild];
    }

    static string? Resolve(string path, string cwd)
    {
        if (path.Trim() == "") return null;
        var p = path.Trim().Replace('/', Path.DirectorySeparatorChar);
        // Git Bash style /c/Users/... -> C:\Users\...
        if (Regex.IsMatch(p, @"^\\[a-zA-Z]\\")) p = $"{char.ToUpperInvariant(p[1])}:{p[2..]}";
        return Path.GetFullPath(Path.IsPathRooted(p) || cwd == "" ? p : Path.Combine(cwd, p));
    }

    /// <summary>True if the path is a DFC data folder or inside one, or is a folder (searched/listed) that directly contains one.</summary>
    // ponytail: checks one level down only - a search started two or more levels above a data folder isn't caught.
    public static bool Protected(string path)
    {
        var isFolder = Directory.Exists(path);
        var dir = isFolder ? path : Path.GetDirectoryName(path);
        if (dir is null) return false;
        for (var d = new DirectoryInfo(dir); d is not null; d = d.Parent)
            if (IsDataFolder(d.FullName)) return true;
        return isFolder && SafeDirs(dir).Any(IsDataFolder);
    }

    static IEnumerable<string> SafeDirs(string dir)
    {
        try { return Directory.GetDirectories(dir); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return []; }
    }

    static bool IsDataFolder(string dir)
    {
        string[] files;
        try { files = Directory.Exists(dir) ? Directory.GetFiles(dir, "*.json") : []; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
        foreach (var f in files)
        {
            try
            {
                using var fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                var buf = new byte[SniffBytes];
                var text = System.Text.Encoding.UTF8.GetString(buf, 0, fs.Read(buf, 0, buf.Length));
                if (Markers.Any(text.Contains)) return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return false;
    }
}
