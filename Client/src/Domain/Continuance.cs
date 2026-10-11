using System.Collections.Generic;

namespace LibreKO.Domain;

public enum ContinuanceCheck
{
    Send,
    Cannot,
    Already,
    Skip,
}

public static class Continuance
{
    public const int Moral = 240;
    public const int ItemSkillFloor = 500000;
    public const int BuffType = 4;
    public const int UsingText = 4402;
    public const int NoItemText = 4005;
    public const int CannotText = 7623;
    public const int UsedText = 7624;
    public const int AlreadyText = 7625;

    private static readonly HashSet<int> ExtendableMorals = new() { 1, 2, 3, 4, 6, 11, 14, 31 };

    public static bool IsContinuanceItem(int moral) => moral == Moral;

    public static ContinuanceCheck Check(int skillId, int type1, int type2, int moral, bool extended)
    {
        if (type1 != BuffType && type2 != BuffType && !ExtendableMorals.Contains(moral)) return ContinuanceCheck.Cannot;
        if (skillId > ItemSkillFloor) return ContinuanceCheck.Skip;
        return extended ? ContinuanceCheck.Already : ContinuanceCheck.Send;
    }
}
