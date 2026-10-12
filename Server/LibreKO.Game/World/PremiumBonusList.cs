using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Domain.Services;
using LibreKO.Game.Protocol;

namespace LibreKO.Game.World;

public enum PremiumBonusKind : byte
{
    Experience = 1,
    Coins = 2,
    Drops = 3,
    RepairDiscount = 4,
    SellPrice = 5,
    DeathExpLoss = 6,
    FreeReset = 7,
    FreeRebirthReset = 8,
    PriceHistory = 9,
}

public readonly record struct PremiumBonus(PremiumBonusKind Kind, short Value);

public static class PremiumBonusList
{
    public static int ExperiencePercent(IReadOnlyList<PremiumItemExpData> table, byte premiumType, int level)
    {
        if (premiumType == 0)
            return 0;

        foreach (var entry in table)
        {
            if (entry.Type == premiumType && level >= entry.MinLevel && level <= entry.MaxLevel)
                return entry.Percent;
        }

        return 0;
    }

    public static IReadOnlyList<PremiumBonus> For(UserSession session, IGameDataService gameData)
    {
        var premiumType = session.PremiumType;
        if (premiumType == 0)
            return [];

        var bonuses = new List<PremiumBonus>();
        Add(bonuses, PremiumBonusKind.Experience, ExperiencePercent(gameData.PremiumItemExpTable, premiumType, session.Level));
        Add(bonuses, PremiumBonusKind.Coins, gameData.GetPremiumProperty(premiumType, PremiumPropertyType.Noah));
        Add(bonuses, PremiumBonusKind.Drops, gameData.GetPremiumProperty(premiumType, PremiumPropertyType.Drop));
        Add(bonuses, PremiumBonusKind.RepairDiscount, gameData.GetPremiumProperty(premiumType, PremiumPropertyType.RepairDiscount));
        Add(bonuses, PremiumBonusKind.SellPrice, NpcSellRules.PremiumBonusPercent);
        if (gameData.PremiumItemTable?.TryGetValue(premiumType, out var premium) == true)
            Add(bonuses, PremiumBonusKind.DeathExpLoss, (int)Math.Round(premium.ExpRestorePercent));
        if (CharacterDevelopmentPacketCoordinator.PremiumResetsFree(premiumType))
            bonuses.Add(new PremiumBonus(PremiumBonusKind.FreeReset, 0));
        if (CharacterDevelopmentPacketCoordinator.PremiumRebirthResetsFree(premiumType))
            bonuses.Add(new PremiumBonus(PremiumBonusKind.FreeRebirthReset, 0));
        bonuses.Add(new PremiumBonus(PremiumBonusKind.PriceHistory, 0));
        return bonuses;
    }

    private static void Add(List<PremiumBonus> bonuses, PremiumBonusKind kind, int percent)
    {
        if (percent > 0)
            bonuses.Add(new PremiumBonus(kind, (short)Math.Min(percent, short.MaxValue)));
    }
}
