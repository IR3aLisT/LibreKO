namespace LibreKO.Domain;

public static class CooldownGroups
{
    public const int None = 0;
    public const int Shells = 2;
    public const int SiegeWeapons = 3;
    public const int RefusalText = 16822;
    public const double RefusalRounding = 1.0;

    public static bool Shares(int group) => group is Shells or SiegeWeapons;

    public static int SecondsShown(double remaining) => (int)(remaining + RefusalRounding);
}
