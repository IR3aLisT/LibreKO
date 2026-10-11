using LibreKO.Common.Infrastructure.Network;

namespace LibreKO.Game.Protocol.Writers;

public static class ItemSmashPacketWriter
{
    public const byte SmashSub = 13;
    public const short Succeeded = 1;
    public const short InventoryFull = 2;
    public const short BagFull = 3;
    public const short CannotDisassemble = 4;
    public const short WrongNpc = 5;

    public readonly record struct Piece(int ItemId, byte BagSlot, ushort Count);

    public static Packet Refusal(short result) => Reply(result);

    public static Packet Success(int itemId, byte bagSlot, IReadOnlyList<Piece> pieces)
    {
        var packet = Reply(Succeeded);
        packet.WriteInt(itemId);
        packet.WriteByte(bagSlot);
        packet.WriteShort((short)pieces.Count);
        foreach (var piece in pieces)
        {
            packet.WriteInt(piece.ItemId);
            packet.WriteByte(piece.BagSlot);
            packet.WriteUShort(piece.Count);
        }
        return packet;
    }

    private static Packet Reply(short result)
    {
        var packet = new Packet(GameOpcodes.GS_ITEM_UPGRADE);
        packet.WriteByte(SmashSub);
        packet.WriteShort(result);
        return packet;
    }
}
