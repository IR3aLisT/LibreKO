namespace LibreKO.Domain;

public static class RebirthProgress
{
    private const double FullPercent = 100.0;
    private static readonly string[] StatShortNames = { "STR", "HP", "DEX", "INT", "MP" };

    public static bool Shown(int level, int rebirthLevel) => rebirthLevel > 0 || level >= CharacterSheet.MaxLevel;

    public static bool Capped(int rebirthLevel) => rebirthLevel >= RebirthPick.MaxRebirthLevel;

    public static float Fraction(int level, double expPercent, int rebirthLevel)
    {
        if (Capped(rebirthLevel)) return 1f;
        if (level < CharacterSheet.MaxLevel) return 0f;
        return (float)System.Math.Clamp(expPercent / FullPercent, 0.0, 1.0);
    }

    public static bool Ready(int level, double expPercent, int rebirthLevel) =>
        !Capped(rebirthLevel) && level >= CharacterSheet.MaxLevel && expPercent >= FullPercent;

    public static string Tooltip(int level, double expPercent, int rebirthLevel, System.Func<int, int> bonusAtRow)
    {
        string head = Capped(rebirthLevel)
            ? $"Rebirth {rebirthLevel} (the highest)"
            : Ready(level, expPercent, rebirthLevel)
                ? $"Rebirth {rebirthLevel} — ready for the next rebirth"
                : $"Rebirth {rebirthLevel} — {Fraction(level, expPercent, rebirthLevel) * FullPercent:0.0}% to the next rebirth";
        int points = rebirthLevel * RebirthPick.PointsPerRebirth;
        if (points == 0) return head;
        var parts = new System.Collections.Generic.List<string>();
        for (int row = 0; row < StatShortNames.Length; row++)
            if (bonusAtRow(row) is var bonus and > 0) parts.Add($"{StatShortNames[row]} +{bonus}");
        return parts.Count > 0
            ? $"{head}\nRebirth points: {points} ({string.Join(", ", parts)})"
            : $"{head}\nRebirth points: {points}";
    }
}
