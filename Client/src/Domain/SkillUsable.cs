using System.Collections.Generic;

namespace LibreKO.Domain;

public static class SkillUsable
{
    public const int AnyWeapon = 0;
    public const int NeedsNoWeapon = 9;
    public const int AnyHands = 254;
    public const int JamadarKind = 140;
    public const int JamadarClass = 1;
    public const int KindsPerClass = 10;

    private static readonly HashSet<int> AngerExplosions = new()
    {
        105255, 106255, 107265, 108265, 109269, 110269, 111266, 112266, 114255, 115255,
        205255, 206255, 207265, 208265, 209269, 210269, 211266, 212266, 214255, 215255,
    };

    public static int WeaponClass(int kind) => kind == JamadarKind ? JamadarClass : kind / KindsPerClass;

    public static bool GearAllows(int needWeapon, int rightKind, int leftKind)
    {
        if (needWeapon is NeedsNoWeapon or AnyHands) return true;
        if (!WeaponAnimation.IsWeapon(rightKind) && !WeaponAnimation.IsWeapon(leftKind)) return false;
        return needWeapon == AnyWeapon || needWeapon == WeaponClass(rightKind) || needWeapon == WeaponClass(leftKind);
    }

    public static bool IsAngerExplosion(int skillId) => AngerExplosions.Contains(skillId);

    public static bool AngerAllows(int skillId, bool gaugeFull) => !IsAngerExplosion(skillId) || gaugeFull;

    public static bool BuffAllows(int needBuffSkill, System.Func<int, bool> active) =>
        needBuffSkill == 0 || active(needBuffSkill);
}
