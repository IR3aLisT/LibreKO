using LibreKO.Common.Infrastructure.Network;

namespace LibreKO.Game.Protocol.Writers;

public sealed class RegenePacketWriter
{
    public const byte RespawnNormal = 1;
    public const byte ResurrectOffer = 4;
    public const byte RespawnRefused = 10;

    public const ushort RefusedStandby = 4;
    public const ushort RefusedMissingItem = 10;

    public short X { get; init; }
    public short Z { get; init; }
    public short Y { get; init; }
    public byte RespawnType { get; init; } = RespawnNormal;

    public static Packet At(short x, short z, short y = 0) => new RegenePacketWriter() { X = x, Z = z, Y = y }.Build();

    public static Packet Offer(short x, short z, short y, int casterId, int skillId)
    {
        var packet = new RegenePacketWriter() { X = x, Z = z, Y = y, RespawnType = ResurrectOffer }.Build();
        packet.WriteInt(casterId);
        packet.WriteInt(skillId);
        return packet;
    }

    public static Packet MissingItem(short x, short z, short y, int itemId, ushort amount)
    {
        var packet = new RegenePacketWriter() { X = x, Z = z, Y = y, RespawnType = RespawnRefused }.Build();
        packet.WriteUShort(RefusedMissingItem);
        packet.WriteInt(itemId);
        packet.WriteUShort(amount);
        return packet;
    }

    private Packet Build()
    {
        var packet = new Packet(GameOpcodes.GS_REGENE);
        packet.WriteShort(X);
        packet.WriteShort(Z);
        packet.WriteShort(Y);
        packet.WriteByte(RespawnType);
        return packet;
    }
}
