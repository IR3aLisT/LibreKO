namespace LibreKO.Domain;

public static class HotbarAssign
{
    public const int NoSecondType = 255;
    public const int Shouting = 130013;
    public const int Assault = 130014;

    public static bool CanDrag(int type1, int type2) => type1 switch
    {
        MagicType.None => type2 == NoSecondType,
        MagicType.Special => type2 != NoSecondType,
        _ => true,
    };

    public static bool CanAdd(int skillId, int type1) =>
        type1 != MagicType.None || skillId is Shouting or Assault;
}
