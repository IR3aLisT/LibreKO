using System.Text.RegularExpressions;

namespace LibreKO.Domain;

public static class WhisperLinks
{
    private static readonly Regex WebLink = new(@"https?://[^\s\[\]]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool IsWebLink(string text) => WebLink.Match(text) is { Success: true } m && m.Value == text;

    public static string Linkify(string text)
    {
        var result = new System.Text.StringBuilder();
        int at = 0;
        foreach (Match m in WebLink.Matches(text))
        {
            result.Append(BbCode.Esc(text[at..m.Index]));
            result.Append($"[url={m.Value}][u]{BbCode.Esc(m.Value)}[/u][/url]");
            at = m.Index + m.Length;
        }
        result.Append(BbCode.Esc(text[at..]));
        return result.ToString();
    }
}
