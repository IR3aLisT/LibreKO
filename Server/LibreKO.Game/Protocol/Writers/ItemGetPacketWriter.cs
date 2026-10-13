using LibreKO.Common.Infrastructure.Network;

namespace LibreKO.Game.Protocol.Writers;

public sealed class ItemGetPacketWriter
{
    public const byte ResultError = 0;
    public const byte ResultSuccess = 1;
    public const byte ResultPartyCoins = 2;
    public const byte ResultPartyMember = 3;
    public const int FailureRetry = -7;
    public const int FailureNoRoom = -11;

    public const byte PositionGold = byte.MaxValue;

    public byte Result { get; set; }
    public int? Failure { get; set; }
    public int BundleId { get; set; }
    public byte Position { get; set; }
    public int ItemId { get; set; }
    public ushort Count { get; set; }
    public int Money { get; set; }
    public ushort BundleSlot { get; set; }
    public string LooterName { get; set; } = "";

    public static Packet Failed(int bundleId) =>
        new ItemGetPacketWriter() { Result = ResultError, BundleId = bundleId, Failure = FailureRetry }.Build();
    public static Packet NoRoom(int bundleId) =>
        new ItemGetPacketWriter() { Result = ResultError, BundleId = bundleId, Failure = FailureNoRoom }.Build();
    public static Packet Looted(
        int bundleId, byte position, int itemId, ushort count, int money, ushort bundleSlot) => new ItemGetPacketWriter()
        {
            Result = ResultSuccess,
            BundleId = bundleId,
            Position = position,
            ItemId = itemId,
            Count = count,
            Money = money,
            BundleSlot = bundleSlot,
        }.Build();
    public static Packet LootedGold(
        int bundleId, int itemId, ushort count, int money, ushort bundleSlot) => Looted(bundleId, PositionGold, itemId, count, money, bundleSlot);
    public static Packet PartyCoins(int bundleId, int itemId, int money) => new ItemGetPacketWriter()
        {
            Result = ResultPartyCoins,
            BundleId = bundleId,
            Position = PositionGold,
            ItemId = itemId,
            Money = money,
        }.Build();
    public static Packet PartyMemberLooted(int bundleId, int itemId, string looterName, ushort bundleSlot) =>
        new ItemGetPacketWriter()
        {
            Result = ResultPartyMember,
            BundleId = bundleId,
            ItemId = itemId,
            LooterName = looterName,
            BundleSlot = bundleSlot,
        }.Build();
    private Packet Build()
    {
        var packet = new Packet(GameOpcodes.GS_ITEM_GET);
        packet.WriteByte(Result);

        packet.WriteInt(BundleId);
        if (Result == ResultPartyMember)
        {
            packet.WriteInt(ItemId);
            packet.WriteString(LooterName);
            packet.WriteUShort(BundleSlot);
            return packet;
        }
        if (Result != ResultSuccess && Result != ResultPartyCoins)
        {
            packet.WriteInt(Failure ?? FailureRetry);
            return packet;
        }

        packet.WriteByte(Position);
        packet.WriteInt(ItemId);
        if (Result == ResultPartyCoins)
        {
            packet.WriteInt(Money);
            return packet;
        }

        packet.WriteUShort(Count);
        packet.WriteInt(Money);
        packet.WriteUShort(BundleSlot);
        return packet;
    }
}
