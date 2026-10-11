namespace LibreKO.Game.World;

public static class NpcTariff
{
    private const int Percent = 100;
    private const int TreasuryShare = 80;
    private const int TerritoryShare = 20;

    public static int Tax(long total, int tariff) => tariff <= 0 || total <= 0 ? 0 : (int)(total * tariff / Percent);

    public static long WithTax(long total, int tariff) => total + Tax(total, tariff);

    public static (int Treasury, int Territory) KingSplit(int tax) =>
        (tax * TreasuryShare / Percent, tax * TerritoryShare / Percent);
}
