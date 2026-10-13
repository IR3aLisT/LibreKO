using LibreKO.Common.Infrastructure.Network;

namespace LibreKO.Game.Protocol.Writers;

public static class MapEventPacketWriter
{
    public const byte SubOwners = 0;
    public const byte SubCaptured = 1;
    public const byte SubPoints = 2;
    public const byte SubResult = 5;
    public const byte CapturedOne = 1;

    public static Packet Owners(IReadOnlyList<byte> owners)
    {
        var packet = new Packet(GameOpcodes.GS_MAP_EVENT);
        packet.WriteByte(SubOwners);
        packet.WriteByte((byte)owners.Count);
        foreach (var owner in owners)
            packet.WriteByte(owner);
        return packet;
    }

    public static Packet Captured(byte index, byte owner)
    {
        var packet = new Packet(GameOpcodes.GS_MAP_EVENT);
        packet.WriteByte(SubCaptured);
        packet.WriteByte(CapturedOne);
        packet.WriteByte(index);
        packet.WriteByte(owner);
        return packet;
    }

    public static Packet Points(ushort karus, ushort elmorad)
    {
        var packet = new Packet(GameOpcodes.GS_MAP_EVENT);
        packet.WriteByte(SubPoints);
        packet.WriteUShort(karus);
        packet.WriteUShort(elmorad);
        return packet;
    }

    public static Packet Result(byte winner)
    {
        var packet = new Packet(GameOpcodes.GS_MAP_EVENT);
        packet.WriteByte(SubResult);
        packet.WriteByte(winner);
        return packet;
    }
}
