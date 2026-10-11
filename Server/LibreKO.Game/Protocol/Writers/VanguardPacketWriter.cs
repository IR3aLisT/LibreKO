using LibreKO.Common.Infrastructure.Network;

namespace LibreKO.Game.Protocol.Writers;

public static class VanguardPacketWriter
{
    public const byte SubAppoint = 1;
    public const byte SubWanted = 2;
    public const byte SubClosed = 3;
    public const byte AppointInvite = 1;
    public const byte AppointResult = 2;
    public const byte WantedList = 1;
    public const byte WantedPositions = 2;
    public const byte WantedRemove = 3;
    public const byte Appointed = 1;
    public const byte AppointFailed = 2;
    public const byte AcceptRequest = 0;

    public static Packet Invite() => Head(SubAppoint, AppointInvite);

    public static Packet Result(byte result)
    {
        var packet = Head(SubAppoint, AppointResult);
        packet.WriteByte(result);
        return packet;
    }

    public static Packet List(IReadOnlyList<string> names)
    {
        var packet = Head(SubWanted, WantedList);
        packet.WriteByte((byte)names.Count);
        foreach (var name in names)
            packet.WriteString(name);
        return packet;
    }

    public static Packet Positions(IReadOnlyList<(short X, short Z, string Name)> wanted)
    {
        var packet = Head(SubWanted, WantedPositions);
        packet.WriteShort((short)wanted.Count);
        foreach (var (x, z, name) in wanted)
        {
            packet.WriteShort(x);
            packet.WriteShort(z);
            packet.WriteString(name);
        }
        return packet;
    }

    public static Packet Remove(string name)
    {
        var packet = Head(SubWanted, WantedRemove);
        packet.WriteString(name);
        return packet;
    }

    public static Packet Closed()
    {
        var packet = new Packet(GameOpcodes.GS_VANGUARD);
        packet.WriteByte(SubClosed);
        return packet;
    }

    private static Packet Head(byte sub, byte kind)
    {
        var packet = new Packet(GameOpcodes.GS_VANGUARD);
        packet.WriteByte(sub);
        packet.WriteByte(kind);
        return packet;
    }
}
