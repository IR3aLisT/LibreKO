using LibreKO.Common.Infrastructure.Network;

namespace LibreKO.Game.Protocol.Writers;

public enum ClanBattleResult : byte
{
    Challenged = 1,
    Offline = 2,
    Dead = 3,
    OtherNation = 4,
    NotLeader = 5,
    Declined = 6,
    NotClanMember = 7,
    WrongZone = 8,
    AlreadyAtWar = 9,
    NotAtWar = 16,
}

public enum ClanBattleEnd : byte
{
    TheySurrendered = 1,
    Cleared = 2,
    YouSurrendered = 3,
}

public static class ClanBattlePacketWriter
{
    public const byte ChallengeSub = 0x13;
    public const byte BegunSub = 0x16;
    public const byte EndSub = 0x17;
    private const ushort Unused = 0;

    public static Packet Result(ClanBattleResult result)
    {
        var packet = Sub(ChallengeSub);
        packet.WriteByte((byte)result);
        return packet;
    }

    public static Packet Challenge(int challengerId, string challengerName)
    {
        var packet = Result(ClanBattleResult.Challenged);
        packet.WriteInt(challengerId);
        packet.WriteUShort(Unused);
        packet.WriteString(challengerName);
        return packet;
    }

    public static Packet Begun(short enemyClanId)
    {
        var packet = Sub(BegunSub);
        packet.WriteShort(enemyClanId);
        return packet;
    }

    public static Packet End(ClanBattleEnd end, short otherClanId = 0, string otherClanName = "")
    {
        var packet = Sub(EndSub);
        packet.WriteByte((byte)end);
        if (end == ClanBattleEnd.Cleared)
            return packet;
        packet.WriteShort(otherClanId);
        packet.WriteUShort(Unused);
        packet.WriteString(otherClanName);
        return packet;
    }

    private static Packet Sub(byte sub)
    {
        var packet = new Packet(GameOpcodes.GS_KNIGHTS_PROCESS);
        packet.WriteByte(sub);
        return packet;
    }
}
