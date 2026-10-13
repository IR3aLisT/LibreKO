using System;

namespace LibreKO.Domain;

public static class SeekingParty
{
    public const byte NotSeeking = 1;
    public const byte SeekingBand = 2;
    public const byte Seeking = 3;
    public const int BandText = 6123;
    public const int SeekingText = 8402;
    private const int LevelGap = 8;
    private const double LevelFactor = 1.5;
    private const int TopLevel = 83;
    private const int BottomLevel = 1;

    public static (int Min, int Max) Band(int level)
    {
        int low = Math.Min(level - LevelGap, (int)(level / LevelFactor));
        if (low < 0) low = BottomLevel;
        int high = Math.Min(TopLevel, Math.Max(level + LevelGap, (int)(level * LevelFactor)));
        return (low, high);
    }

    public static string? Line(byte state, int level, Func<int, string, string> text) => state switch
    {
        SeekingBand => TextTemplate.Fill(text(BandText, "Seeking Party : Level %d ~ %d"), Band(level).Min, Band(level).Max),
        Seeking => text(SeekingText, "Seeking_Party"),
        _ => null,
    };
}
