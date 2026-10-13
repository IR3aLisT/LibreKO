namespace LibreKO.Domain;

public static class StrikeEffect
{
    public const int None = 0;
    private const int GlowTargetOffset = 2;

    private static readonly int[] OpeningSwingBlood = { 10002, 10048 };
    private static readonly int[] FollowSwingBlood = { 10049, 10050 };
    private static readonly int[] OtherStrikeBlood = { 10051, 10052, 10053, 10054 };

    public static int FromGlow(int glowFx) => glowFx > 0 ? glowFx + GlowTargetOffset : None;

    public static int Blood(bool npcAttacker, int rightKind, int strikeAnim, int stanceBase, System.Func<int, int> pick)
    {
        bool opening = npcAttacker
            || strikeAnim == stanceBase + 1
            || (rightKind == WeaponAnimation.Staff && strikeAnim == WeaponAnimation.AttackStaff);
        int[] set = opening ? OpeningSwingBlood
            : strikeAnim == stanceBase + 2 ? FollowSwingBlood
            : OtherStrikeBlood;
        return set[pick(set.Length)];
    }
}
