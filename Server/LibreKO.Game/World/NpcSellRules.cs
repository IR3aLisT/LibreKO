namespace LibreKO.Game.World;

public static class NpcSellRules
{
    public const int NormalDivisor = 6;
    public const int PremiumDivisor = 4;
    public const int PremiumBonusPercent = NormalDivisor * 100 / PremiumDivisor - 100;

    public static int Unit(int buyPrice, bool fullSale, bool premium)
    {
        var price = fullSale ? buyPrice : buyPrice / (premium ? PremiumDivisor : NormalDivisor);
        return price < 1 ? 0 : price;
    }

    public static bool SellsAtPremium(UserSession session, bool clanPremium) => session.PremiumType != 0 || clanPremium;
}
