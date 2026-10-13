using LibreKO.Common.Enums;

namespace LibreKO.Game.World;

public static class FullMoonRules
{
    public const byte NormalBoard = 0;
    public const byte FullMoonBoard = 2;
    public const int VoucherItem = 914057000;
    public const byte EntrySub = 58;
    public const byte TowerSub = 59;
    public const byte StageSub = 60;

    public const byte InstanceFailed = 1;
    public const byte NotAllInMoradon = 2;
    public const byte MemberDead = 4;
    public const byte NoParty = 5;
    public const byte NotFullMoonParty = 6;
    public const byte MemberWithoutVoucher = 7;

    public const byte AnyState = 0;
    public const byte SoloState = 2;
    public const byte PartyState = 3;
    public const byte AnyZone = 0;
    public const byte AnyLevel = 0;
    public const int LevelBand = 10;
    public const int LastBand = 9;

    public static bool InMoradon(byte zone) => zone is >= (byte)ZoneId.Moradon and <= (byte)ZoneId.Moradon4;

    public static bool HasVoucher(UserSession session) =>
        session.Inventory.Any(slot => slot.ItemId == VoucherItem && slot.Count > 0);

    public static byte EntryRefusal(UserSession? leader, IReadOnlyList<UserSession> members, byte boardType)
    {
        if (leader == null || members.Count == 0)
            return NoParty;
        if (boardType != FullMoonBoard)
            return NotFullMoonParty;
        if (members.Any(m => !InMoradon(m.ZoneId)))
            return NotAllInMoradon;
        if (members.Any(m => m.Hp <= 0))
            return MemberDead;
        if (members.Any(m => !HasVoucher(m)))
            return MemberWithoutVoucher;
        return InstanceFailed;
    }

    public static bool PassesFilter(byte state, short zone, byte level, bool isParty, short candidateZone, byte candidateLevel)
    {
        if (state == SoloState && isParty) return false;
        if (state == PartyState && !isParty) return false;
        if (zone != AnyZone && zone != candidateZone) return false;
        if (level == AnyLevel) return true;
        int band = Math.Min((candidateLevel - 1) / LevelBand + 1, LastBand);
        return band == level;
    }
}
