using System;

namespace LibreKO.Domain;

[Flags]
public enum GenieOther : byte
{
    None = 0,
    HpPotions = 1,
    MpPotions = 2,
    Arrows = 4,
    PartyBroken = 8,
    LastSurvivor = 16,
    WeaponBroken = 32,
    AutoSpirit = 64,
}

public static class GenieTownReturn
{
    public const int DefaultCount = 10;
    public const int MinCount = 1;
    public const int MaxCount = 999;
    public const int AutoSpiritAtPoints = 1;

    public readonly record struct Rule(GenieOther Option, int LabelText, string Label, int MessageText, string Message);

    public static readonly Rule[] Rules =
    {
        new(GenieOther.HpPotions, 31034, "When the HP recovery item falls below the set value, return to the village",
            31000, "Moving to town due to enough HP recovery amount. Once you are in town, Genie will be disabled."),
        new(GenieOther.MpPotions, 31035, "When the MP recovery item falls below the set value, return to the village",
            31001, "Moving to town due to enough MP recovery amount. Once you are in town, Genie will be disabled."),
        new(GenieOther.Arrows, 31036, "When there's not enough arrows you will be returned to town. (only for Rogues)",
            31002, "Moving to town due to insufficient arrow amount. Once you are in town, Genie will be disabled."),
        new(GenieOther.PartyBroken, 31037, "When the party breaks up you will be returned to town.",
            31003, "Party has been canceled. Moving to town. Once you are in town, Genie will be disabled."),
        new(GenieOther.LastSurvivor, 31038, "When you are the only survivor of the party, you'll be moved to town.",
            31004, "You are the only one who survived in the party. Moving to town. Once you are in town, Genie will be disabled."),
        new(GenieOther.WeaponBroken, 31039, "When the weapon durability is 0 you'll be moved to town.",
            31005, "Your weapon durability is 0. Moving to town. Genie will be disabled once you are in town."),
    };

    public const int AutoSpiritText = 31041;
    public const string AutoSpiritLabel = "When Knight Genie point is 1, automatically use the Genie Spirit. (When Genie Spirit is present)";

    public const int LowHpText = 31015;
    public const string LowHpMessage = "? Caution ? HP is less than 50%, does not meet the required amount to be moved to town.";

    public static bool CanMoveToTown(int hp, int maxHp) => maxHp > 0 && hp * 2 >= maxHp;

    public static int StoredOrDefault(int stored) => stored is >= MinCount and <= MaxCount ? stored : DefaultCount;

    public static bool ShouldReturn(bool enabled, int stock, int threshold) => enabled && stock < threshold;

    public readonly record struct State(
        int HpStock, int HpThreshold, int MpStock, int MpThreshold,
        bool NeedsArrows, int Arrows,
        bool WasInParty, bool InParty, bool PartyOthersAllDead,
        bool WeaponBroken);

    public static GenieOther Triggered(GenieOther enabled, State s)
    {
        if (ShouldReturn((enabled & GenieOther.HpPotions) != 0, s.HpStock, s.HpThreshold)) return GenieOther.HpPotions;
        if (ShouldReturn((enabled & GenieOther.MpPotions) != 0, s.MpStock, s.MpThreshold)) return GenieOther.MpPotions;
        if ((enabled & GenieOther.Arrows) != 0 && s.NeedsArrows && s.Arrows <= 0) return GenieOther.Arrows;
        if ((enabled & GenieOther.PartyBroken) != 0 && s.WasInParty && !s.InParty) return GenieOther.PartyBroken;
        if ((enabled & GenieOther.LastSurvivor) != 0 && s.InParty && s.PartyOthersAllDead) return GenieOther.LastSurvivor;
        if ((enabled & GenieOther.WeaponBroken) != 0 && s.WeaponBroken) return GenieOther.WeaponBroken;
        return GenieOther.None;
    }

    public static bool WantsSpirit(GenieOther enabled, int points, bool hasSpirit) =>
        (enabled & GenieOther.AutoSpirit) != 0 && points == AutoSpiritAtPoints && hasSpirit;
}
