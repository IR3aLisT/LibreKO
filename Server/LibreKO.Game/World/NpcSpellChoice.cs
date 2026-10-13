namespace LibreKO.Game.World;

public static class NpcSpellChoice
{
    public const int SpellRollRange = 401;
    public const int MeleeRollRange = 3000;
    private const int SecondSpellBelow = 30;
    private const int ThirdSpellBelow = 60;
    private const int MeleeSpellBelow = 1000;

    public static int Pick(int magic1, int magic2, int magic3, int roll) =>
        roll < SecondSpellBelow && magic2 != 0 ? magic2
        : roll < ThirdSpellBelow && magic3 != 0 ? magic3
        : magic1;

    public static bool CastsInMelee(int roll) => roll < MeleeSpellBelow;
}
