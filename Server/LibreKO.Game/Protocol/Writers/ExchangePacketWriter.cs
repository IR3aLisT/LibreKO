using LibreKO.Common.Infrastructure.Network;

namespace LibreKO.Game.Protocol.Writers;

public sealed class ExchangePacketWriter
{
    public const byte Failed = 0;
    public const byte Succeeded = 1;

    private const int NoItemExtension = 0;

    public readonly record struct TransferredItem(
        byte Position, int ItemId, ushort Count, short Durability,
        int UniqueId = 0, CharacterSealPacketWriter.RingSummary? Ring = null);

    public static Packet Sub(byte sub)
    {
        var packet = new Packet(GameOpcodes.GS_EXCHANGE);
        packet.WriteByte(sub);
        return packet;
    }

    public static Packet Result(byte sub, byte result)
    {
        var packet = Sub(sub);
        packet.WriteByte(result);
        return packet;
    }

    public static Packet Partner(byte sub, int characterId)
    {
        var packet = Sub(sub);
        packet.WriteInt(characterId);
        return packet;
    }

    public static Packet ItemOffered(byte sub, int itemId, int count, short durability,
        int uniqueId = 0, CharacterSealPacketWriter.RingSummary? ring = null)
    {
        var packet = Sub(sub);
        packet.WriteInt(itemId);
        packet.WriteInt(count);
        packet.WriteShort(durability);
        WriteExtension(packet, uniqueId, ring);
        return packet;
    }

    public static Packet Completed(
        byte sub, int money, IReadOnlyCollection<TransferredItem> items)
    {
        var packet = Result(sub, Succeeded);
        packet.WriteInt(money);
        packet.WriteUShort((ushort)items.Count);

        foreach (var item in items)
        {
            packet.WriteByte(item.Position);
            packet.WriteInt(item.ItemId);
            packet.WriteUShort(item.Count);
            packet.WriteShort(item.Durability);
            packet.WriteByte(0);
            WriteExtension(packet, item.UniqueId, item.Ring);
        }

        return packet;
    }

    private static void WriteExtension(Packet packet, int uniqueId, CharacterSealPacketWriter.RingSummary? ring)
    {
        if (uniqueId == 0 || ring is not { } summary)
        {
            packet.WriteInt(NoItemExtension);
            return;
        }
        packet.WriteInt(uniqueId);
        CharacterSealPacketWriter.WriteSummary(packet, summary);
    }
}
