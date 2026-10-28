using System.Text.Json;

namespace DfcMigrator;

/// <summary>Operator prompts for the interactive menu. End of input (closed window / Ctrl+Z) always means "quit", never "yes".</summary>
public static class Prompt
{
    public static string Ask(string question, string? defaultValue = null)
    {
        Console.Write(defaultValue is null ? $"  {question}: " : $"  {question} [{defaultValue}]: ");
        var s = Console.ReadLine() ?? throw new QuitException();
        s = s.Trim().Trim('﻿').Trim('"').Trim(); // drag-and-drop pastes quoted paths; piped/pasted input can start with a BOM
        return s == "" ? defaultValue ?? "" : s;
    }

    public static bool YesNo(string question, bool defaultYes)
    {
        var a = Ask($"{question} {(defaultYes ? "[Y/n]" : "[y/N]")}").ToLowerInvariant();
        return a == "" ? defaultYes : a is "y" or "yes";
    }

    // Operators often answer a "[default]" question with "yes": treat that as accepting the default, never as a value.
    static bool IsAccept(string s) => s.ToLowerInvariant() is "y" or "yes" or "ok";

    static string AskOrBack(string question, string? defaultValue)
    {
        var a = Ask($"{question} (ENTER = accept, Q = back to menu)", defaultValue);
        if (a.Equals("q", StringComparison.OrdinalIgnoreCase)) throw new BackToMenuException();
        return IsAccept(a) ? defaultValue ?? "" : a;
    }

    /// <summary>Existing folder (the customer's data folder): re-asks until it exists and can be read.</summary>
    public static string AskExistingFolder(string question, string? defaultPath)
    {
        while (true)
        {
            var a = AskOrBack(question, defaultPath);
            if (a == "") { Say("Please enter a folder path.", ConsoleColor.Yellow); continue; }
            try
            {
                var full = Path.GetFullPath(a);
                if (!Directory.Exists(full)) { Say($"Folder not found: {full}", ConsoleColor.Yellow); continue; }
                Directory.GetFiles(full); // accessible?
                return full;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or UnauthorizedAccessException or IOException)
            { Say($"Cannot use '{a}': {ex.Message}", ConsoleColor.Yellow); }
        }
    }

    /// <summary>Optional existing folder: blank / no / skip = none.</summary>
    public static string AskOptionalFolder(string question, string defaultPath)
    {
        while (true)
        {
            var a = Ask(question, defaultPath == "" ? null : defaultPath);
            if (IsAccept(a)) a = defaultPath;
            if (a == "" || a.Equals("skip", StringComparison.OrdinalIgnoreCase) || a.Equals("n", StringComparison.OrdinalIgnoreCase) || a.Equals("no", StringComparison.OrdinalIgnoreCase)) return "";
            if (Directory.Exists(a)) return Path.GetFullPath(a);
            Say($"Folder not found: {a} (leave blank to skip)", ConsoleColor.Yellow);
        }
    }

    /// <summary>Folder (e.g. the SNAP Clients folder): must be a full path; a missing folder is only created on request.</summary>
    public static string AskFolder(string question, string defaultPath)
    {
        while (true)
        {
            var a = AskOrBack(question, defaultPath);
            if (!Path.IsPathRooted(a)) { Say($"'{a}' is not a full folder path (e.g. C:\\...\\SnapDDA - Website\\Clients).", ConsoleColor.Yellow); continue; }
            try
            {
                var full = Path.GetFullPath(a);
                if (Directory.Exists(full) || YesNo($"{full} does not exist. Create it?", false)) return full;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            { Say($"'{a}' is not a valid folder path.", ConsoleColor.Yellow); }
        }
    }

    /// <summary>Optional site URL: blank = skip; otherwise must be an absolute http(s) URL.</summary>
    public static string AskUrl(string question, string defaultUrl)
    {
        while (true)
        {
            var a = Ask($"{question} (blank = skip)", defaultUrl == "" ? null : defaultUrl);
            if (IsAccept(a)) a = defaultUrl;
            if (a == "" || a.Equals("skip", StringComparison.OrdinalIgnoreCase) || a.Equals("n", StringComparison.OrdinalIgnoreCase) || a.Equals("no", StringComparison.OrdinalIgnoreCase)) return "";
            if (Uri.TryCreate(a, UriKind.Absolute, out var u) && u.Scheme is "http" or "https") return a;
            Say($"'{a}' is not a URL - use e.g. http://localhost:4200, or leave blank.", ConsoleColor.Yellow);
        }
    }

    public static void Header(string title)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n --- {title} ---");
        Console.ResetColor();
    }

    public static void Say(string text, ConsoleColor color = ConsoleColor.Gray)
    {
        Console.ForegroundColor = color;
        Console.WriteLine("  " + text);
        Console.ResetColor();
    }
}

public class QuitException : Exception;

/// <summary>Operator typed Q at a prompt: abandon the current option and show the menu again (nothing written).</summary>
public class BackToMenuException : Exception;

/// <summary>Folders the tool works from, found relative to the exe (the DFCMigration folder), plus remembered answers.</summary>
public class Workspace
{
    public string Home { get; }
    public string TemplateDir => Path.Combine(Home, "Template", "GenericClubFastDD");
    public string LogsDir => Path.Combine(Home, "Logs");
    public string BackupsDir => Path.Combine(Home, "Backups");
    public string DefaultOutput => Path.Combine(Home, "Output");
    string SettingsFile => Path.Combine(Home, "dfcmigrate.settings.json");

    public Workspace()
    {
        // The DFCMigration folder is the first parent of the exe (or the current folder) that contains the template.
        Home = new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() }
            .SelectMany(Ancestors)
            .FirstOrDefault(d => Directory.Exists(Path.Combine(d, "Template", "GenericClubFastDD")))
            ?? Directory.GetCurrentDirectory();
    }

    static IEnumerable<string> Ancestors(string dir)
    {
        for (var d = new DirectoryInfo(dir); d is not null; d = d.Parent) yield return d.FullName;
    }

    // Remembered answers (e.g. the SNAP Clients folder) so operators don't retype them. No secrets are stored.
    public Dictionary<string, string> Settings
    {
        get
        {
            try { return File.Exists(SettingsFile) ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(SettingsFile)) ?? [] : []; }
            catch (JsonException) { return []; }
        }
    }

    public void Remember(string key, string value)
    {
        var s = Settings;
        s[key] = value;
        File.WriteAllText(SettingsFile, JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true }));
    }
}
