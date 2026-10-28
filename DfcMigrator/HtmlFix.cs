using System.Text;
using System.Text.RegularExpressions;

namespace DfcMigrator;

// DFC product descriptions are free HTML and often leave tags open ("<b>Adult 3 Month ..." with no </b>).
// SNAP renders them inside radio-button labels; an open tag swallows the following products and
// JS/H_ProductGrouping.js then drops them. Balance the markup, and refuse anything script-like (rendered on a public page).
public static partial class HtmlFix
{
    static readonly HashSet<string> Void = new(StringComparer.OrdinalIgnoreCase) { "br", "img", "hr", "input", "wbr", "meta", "link", "col", "source" };

    [GeneratedRegex(@"<\s*(/?)\s*([a-zA-Z][a-zA-Z0-9]*)\b[^<>]*?(/?)\s*>")]
    private static partial Regex Tag();

    [GeneratedRegex(@"<\s*/?\s*(script|iframe|object|embed|style|form)\b|\bon[a-z]+\s*=|javascript\s*:|<!--", RegexOptions.IgnoreCase)]
    private static partial Regex Unsafe();

    /// <returns>Balanced HTML, or null with <paramref name="problem"/> set when the markup must be fixed by hand.</returns>
    public static string? Balance(string html, out string? problem, out bool changed)
    {
        problem = null;
        changed = false;
        if (Unsafe().IsMatch(html)) { problem = "contains script, event handler, javascript: or comment markup - remove it"; return null; }

        var sb = new StringBuilder();
        var open = new Stack<string>();
        int last = 0;
        foreach (Match m in Tag().Matches(html))
        {
            sb.Append(html, last, m.Index - last);
            last = m.Index + m.Length;
            string name = m.Groups[2].Value.ToLowerInvariant();
            bool closing = m.Groups[1].Value == "/", selfClosing = m.Groups[3].Value == "/";

            if (!closing) { sb.Append(m.Value); if (!selfClosing && !Void.Contains(name)) open.Push(name); continue; }
            if (Void.Contains(name)) { changed = true; continue; }        // </br> etc.
            if (!open.Contains(name)) { changed = true; continue; }       // stray closing tag: drop it
            while (open.Peek() != name) { sb.Append($"</{open.Pop()}>"); changed = true; } // close tags left open inside it
            open.Pop();
            sb.Append(m.Value);
        }
        sb.Append(html, last, html.Length - last);
        if (Tag().Replace(sb.ToString(), "").IndexOfAny(['<', '>']) >= 0)
        { problem = "contains a '<' or '>' that is not part of a valid tag - use &lt; / &gt;"; return null; }
        while (open.Count > 0) { sb.Append($"</{open.Pop()}>"); changed = true; }
        return sb.ToString();
    }
}
