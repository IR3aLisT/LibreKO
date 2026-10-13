using LibreKO.Common.Infrastructure.Network;

namespace LibreKO.Game.Protocol.Writers;

public sealed class ItemTradePacketWriter
{
    public static Packet Failed(ItemTradeRefusal reason)
    {
        var packet = Sub(ItemTradeResult.Refused);
        packet.WriteByte((byte)reason);
        return packet;
    }

    public static Packet Moved() => Sub(ItemTradeResult.Moved);

    public static Packet Traded(int balance, int price, byte? loyaltySellingGroup = null)
    {
        var packet = Sub(ItemTradeResult.Traded);
        packet.WriteInt(balance);
        packet.WriteInt(price);
        if (loyaltySellingGroup.HasValue)
            packet.WriteByte(loyaltySellingGroup.Value);
        return packet;
    }

    public const byte RepurchaseOpen = 1;
    public const byte RepurchaseBuy = 2;
    public const byte RepurchaseClose = 3;
    public const byte RepurchaseRefresh = 4;
    public const byte RepurchaseSucceeded = 1;
    public const byte RepurchaseFailed = 2;

    public readonly record struct RepurchaseRow(byte Row, int ItemId, int Price, uint DeletesAt);

    public static Packet RepurchaseList(byte op, IReadOnlyList<RepurchaseRow> rows)
    {
        var packet = Sub(ItemTradeResult.Repurchase);
        packet.WriteByte(op);
        packet.WriteByte(RepurchaseSucceeded);
        packet.WriteByte((byte)rows.Count);
        foreach (var row in rows)
        {
            packet.WriteByte(row.Row);
            packet.WriteInt(row.ItemId);
            packet.WriteInt(row.Price);
            packet.WriteUInt(row.DeletesAt);
        }
        return packet;
    }

    public static Packet RepurchaseBought(byte bagPosition, int itemId, byte flag)
    {
        var packet = Sub(ItemTradeResult.Repurchase);
        packet.WriteByte(RepurchaseBuy);
        packet.WriteByte(RepurchaseSucceeded);
        packet.WriteByte(0);
        packet.WriteByte(bagPosition);
        packet.WriteInt(itemId);
        packet.WriteByte(flag);
        return packet;
    }

    public static Packet RepurchaseRefused(int itemId, short error)
    {
        var packet = Sub(ItemTradeResult.Repurchase);
        packet.WriteByte(RepurchaseBuy);
        packet.WriteByte(RepurchaseFailed);
        packet.WriteByte(0);
        packet.WriteInt(itemId);
        packet.WriteShort(error);
        return packet;
    }

    private static Packet Sub(ItemTradeResult result)
    {
        var packet = new Packet(GameOpcodes.GS_ITEM_TRADE);
        packet.WriteByte((byte)result);
        return packet;
    }
}
