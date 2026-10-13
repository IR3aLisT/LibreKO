namespace LibreKO.Domain;

public static class RankSymbol
{
    public const int NoTier = -1;
    private const string Folder = "res://assets/ui/rank_symbol";

    public static int TierOf(int personalRank) => personalRank switch
    {
        1 => 0,
        >= 2 and <= 4 => 1,
        >= 5 and <= 10 => 2,
        >= 11 and <= 40 => 3,
        >= 41 and <= 100 => 4,
        >= 101 and <= 200 => 5,
        _ => NoTier,
    };

    public static string? TexturePath(int personalRank) =>
        TierOf(personalRank) is var tier and >= 0 ? $"{Folder}/tier_{tier}.png" : null;

    public static string? PlatePath(int knightsRank, int personalRank)
    {
        if (knightsRank != 0 && knightsRank < personalRank && TierOf(knightsRank) is var knights and >= 0)
            return $"{Folder}/knights_{knights}.png";
        return TierOf(personalRank) is var personal and >= 0 ? $"{Folder}/personal_{personal}.png" : null;
    }
}
