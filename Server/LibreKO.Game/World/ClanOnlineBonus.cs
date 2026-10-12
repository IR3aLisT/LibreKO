namespace LibreKO.Game.World;

public static class ClanOnlineBonus
{
    public const int Threshold = 5;
    private const int ExpBase = 15;
    private const int ExpCap = 65;
    private const int NpStep = 10;
    private const int NpCap = 6;

    public static int ExpPercent(int online) => online < Threshold ? 0 : Math.Min(ExpCap, online + ExpBase);

    public static int Np(int online) => online < Threshold ? 0 : Math.Min(NpCap, online / NpStep + 1);
}
