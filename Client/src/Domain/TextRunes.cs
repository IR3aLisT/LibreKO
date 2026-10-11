using System.Text;

namespace LibreKO.Domain;

public static class TextRunes
{
    public static int Count(string text)
    {
        int count = 0;
        foreach (var _ in text.EnumerateRunes()) count++;
        return count;
    }

    public static string Truncate(string text, int maxRunes) => text[..Utf16Index(text, maxRunes)];

    public static int Utf16Index(string text, int column)
    {
        int index = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (column-- <= 0) break;
            index += rune.Utf16SequenceLength;
        }
        return index;
    }

    public static int Column(string text, int utf16Index) => Count(text[..System.Math.Clamp(utf16Index, 0, text.Length)]);

    public static string TruncateUtf8(string text, int maxBytes)
    {
        int bytes = 0;
        int end = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (bytes + rune.Utf8SequenceLength > maxBytes) return text[..end];
            bytes += rune.Utf8SequenceLength;
            end += rune.Utf16SequenceLength;
        }
        return text;
    }
}
