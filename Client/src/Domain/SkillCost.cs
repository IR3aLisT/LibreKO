namespace LibreKO.Domain;

public static class SkillCost
{
    public const int TextNotEnoughMp = 3004;
    public const int TextNotEnoughSp = 4008;
    public const int TextNotEnoughHp = 4006;
    public const int TextNotEnoughVp = 4013;
    public const int HpCostCap = 10000;
    private const int MageFamily = 3;
    private const int PriestFamily = 4;

    public static int ManaShortText(int classCode) =>
        CharacterClassCatalog.Family(classCode) is MageFamily or PriestFamily ? TextNotEnoughMp : TextNotEnoughSp;

    public static bool HpShort(int hp, int hpCost) => hpCost < HpCostCap && hp < hpCost;
}
