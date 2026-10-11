using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol;

namespace LibreKO.Game.Protocol.Writers;

public sealed class WarehousePacketWriter
{
    public const byte Failed = 0;
    public const byte Succeeded = 1;
    public const byte DuplicateItem = 2;

    public static Packet Result(WarehouseSubOpcode sub, byte result)
    {
        var packet = Sub((byte)sub);
        packet.WriteByte(result);
        return packet;
    }

    public static Packet Result(WarehouseSubOpcode sub, bool succeeded) =>
        Result(sub, succeeded ? Succeeded : Failed);

    public static Packet Contents(WarehouseSubOpcode sub, int money, IReadOnlyList<ItemSlot> slots)
    {
        var packet = Result(sub, Succeeded);
        packet.WriteInt(money);

        foreach (var slot in slots)
        {
            packet.WriteInt(slot.ItemId);
            packet.WriteShort(slot.Durability);
            packet.WriteUShort(slot.Count);
            packet.WriteByte(slot.Flag);
            packet.WriteULong(0);
        }

        return packet;
    }

    public static Packet VipResult(VipWarehouseSubOpcode sub, VipWarehouseResult result)
    {
        var packet = VipSub(sub);
        packet.WriteByte((byte)result);
        return packet;
    }

    public static Packet VipContents(bool pinSet, bool open, uint expiresAt, IReadOnlyList<ItemSlot> slots)
    {
        var packet = VipResult(VipWarehouseSubOpcode.Open, VipWarehouseResult.Succeeded);
        packet.WriteByte((byte)(pinSet ? 1 : 0));
        for (byte storage = 0; storage < VipVault.Storages; storage++)
        {
            var own = storage == VipVault.OwnStorage;
            packet.WriteByte((byte)(own && open ? 1 : 0));
            packet.WriteUInt(own ? expiresAt : 0u);
        }

        foreach (var slot in slots)
        {
            packet.WriteInt(slot.ItemId);
            packet.WriteShort(slot.Durability);
            packet.WriteUShort(slot.Count);
            packet.WriteByte(slot.Flag);
            packet.WriteULong(0);
        }

        return packet;
    }

    public static Packet VipVaultExtended(byte storage, uint expiresAt)
    {
        var packet = VipResult(VipWarehouseSubOpcode.UseVault, VipWarehouseResult.Succeeded);
        packet.WriteByte(storage);
        packet.WriteUInt(expiresAt);
        return packet;
    }

    public static Packet VipKeyExpired(byte storage)
    {
        var packet = VipSub(VipWarehouseSubOpcode.KeyExpired);
        packet.WriteByte(storage);
        return packet;
    }

    public static Packet VipPasswordAccepted(VipWarehouseSubOpcode sub, VipWarehouseResult result)
    {
        var packet = VipResult(sub, result);
        packet.WriteByte(0);
        return packet;
    }

    public static Packet VipPasswordRejected(byte failures)
    {
        var packet = VipResult(VipWarehouseSubOpcode.EnterPassword, VipWarehouseResult.Rejected);
        packet.WriteByte(failures);
        return packet;
    }

    private static Packet VipSub(VipWarehouseSubOpcode sub)
    {
        var packet = new Packet(GameOpcodes.GS_VIP_WAREHOUSE);
        packet.WriteByte((byte)sub);
        return packet;
    }

    private static Packet Sub(byte sub)
    {
        var packet = new Packet(GameOpcodes.GS_WAREHOUSE);
        packet.WriteByte(sub);
        return packet;
    }
}
