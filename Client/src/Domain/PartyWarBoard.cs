using System.Collections.Generic;
using System.Linq;

namespace LibreKO.Domain;

public readonly record struct WarBoardShare(int MemberId, int Share);

public readonly record struct WarBoardReply(byte Target, byte Mode, IReadOnlyList<WarBoardShare> Shares);

public static class PartyWarBoard
{
    public const byte TargetRows = 1;
    public const byte TargetBoard = 2;
    public const byte ModeOff = 0;
    public const byte ModeDamage = 1;
    public const byte ModeHealing = 2;
    public const int ShareScale = 1000;
    public const int MaxRows = 8;
    public const double RowToggleSeconds = 1.0;
    public const double BoardTabSeconds = 5.0;
    public const int TextAvailableIn = 16820;
    private const int PriestFamily = 4;
    private const int TenthsPerPercent = 10;

    public static byte NextRowMode(byte mode) => mode switch
    {
        ModeOff => ModeDamage,
        ModeDamage => ModeHealing,
        _ => ModeOff,
    };

    public static float Fill(int share) => System.Math.Clamp(share / (float)ShareScale, 0f, 1f);

    public static string Percent(int share) =>
        share <= 0 ? "0.0%" : $"{share / TenthsPerPercent}.{share % TenthsPerPercent}%";

    public static string RowName(int place, string name) => $"{place}. {name}";

    public static IReadOnlyList<WarBoardShare> Ranked(IEnumerable<WarBoardShare> shares) =>
        shares.OrderByDescending(s => s.Share).Take(MaxRows).ToList();

    public static byte DefaultBoardMode(int classCode) =>
        CharacterClassCatalog.Family(classCode) == PriestFamily ? ModeHealing : ModeDamage;

    public static double SecondsLeft(double lastSent, double now, double limit) =>
        System.Math.Max(0, lastSent + limit - now);
}
