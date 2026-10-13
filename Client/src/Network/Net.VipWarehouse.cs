using System;
using LibreKO.Domain;

namespace LibreKO.Network;

public partial class Net
{
    public const int VipWarehouseSlots = 48;

    private const byte VipWhOpen = 0x01;
    public const byte VipWhInputSub = 0x02;
    public const byte VipWhOutputSub = 0x03;
    public const byte VipWhMoveSub = 0x04;
    private const byte VipWhUseKey = 0x06;
    private const byte VipWhKeyExpired = 0x07;
    public const byte VipWhSetPinSub = 0x08;
    public const byte VipWhCancelPinSub = 0x09;
    public const byte VipWhChangePinSub = 0x0A;
    public const byte VipWhEnterPinSub = 0x0B;

    public const byte VipFail = 0;
    public const byte VipSuccess = 1;
    public const byte VipRejected = 2;
    public const byte VipExpired = 3;
    public const byte VipNotAVaultKey = 4;
    public const byte VipRentalAtCap = 5;

    public event Action<VipVaultContents>? VipWarehouseContentsEvent;

    public event Action<byte, byte>? VipWarehouseResultEvent;

    public event Action? VipWarehouseExpiredEvent;

    public event Action<VipVaultKeyReply>? VipVaultRenewedEvent;

    public event Action? VipVaultKeyExpiredEvent;

    public event Action? VipWarehousePinPromptEvent;

    public event Action<byte, bool>? VipWarehousePinResultEvent;

    public event Action<int>? VipWarehousePinRejectedEvent;

    private void HandleVipWarehouse(Packet p)
    {
        if (p.RemainingBytes < 1) return;
        byte sub = p.ReadByte();
        switch (sub)
        {
            case VipWhOpen:
            {
                byte result = p.RemainingBytes >= 1 ? p.ReadByte() : VipFail;
                if (result == VipExpired) { VipWarehouseExpiredEvent?.Invoke(); break; }
                if (result != VipSuccess) break;
                if (VipVaultContents.Read(p, VipWarehouseSlots) is { } contents)
                    VipWarehouseContentsEvent?.Invoke(contents);
                break;
            }
            case VipWhInputSub: case VipWhOutputSub: case VipWhMoveSub:
                VipWarehouseResultEvent?.Invoke(sub, p.RemainingBytes >= 1 ? p.ReadByte() : VipFail);
                break;
            case VipWhUseKey:
                if (VipVaultKeyReply.Read(p) is { } reply) VipVaultRenewedEvent?.Invoke(reply);
                break;
            case VipWhKeyExpired:
                VipVaultKeyExpiredEvent?.Invoke();
                break;
            case VipWhSetPinSub: case VipWhCancelPinSub: case VipWhChangePinSub:
            {
                bool ok = p.RemainingBytes >= 1 && p.ReadByte() == VipSuccess;
                VipWarehousePinResultEvent?.Invoke(sub, ok);
                break;
            }
            case VipWhEnterPinSub:
            {
                byte result = p.RemainingBytes >= 1 ? p.ReadByte() : VipFail;
                if (result == VipSuccess)
                {
                    if (p.RemainingBytes >= 1) { p.ReadByte(); VipWarehousePinResultEvent?.Invoke(VipWhEnterPinSub, true); }
                    else VipWarehousePinPromptEvent?.Invoke();
                }
                else if (result == VipRejected && p.RemainingBytes >= 1) VipWarehousePinRejectedEvent?.Invoke(p.ReadByte());
                else VipWarehousePinResultEvent?.Invoke(VipWhEnterPinSub, false);
                break;
            }
        }
    }

    public void SendVipVaultUseKey(int itemId, byte bagPos)
    {
        var p = new Packet(GameOpcodes.GS_VIP_WAREHOUSE);
        p.WriteByte(VipWhUseKey);
        p.WriteInt(0);
        p.WriteInt(itemId);
        p.WriteByte(0);
        p.WriteByte(bagPos);
        p.WriteByte(VipVaultContents.OwnStorage);
        _conn.Send(p);
    }

    public void SendVipWarehouseOpen()
    {
        var p = new Packet(GameOpcodes.GS_VIP_WAREHOUSE);
        p.WriteByte(VipWhOpen);
        _conn.Send(p);
    }

    public void SendVipWarehouseInput(int itemId, byte srcInvPos, byte dstVipPos, int count)
        => SendVipWarehouseMove(VipWhInputSub, itemId, VipVaultContents.OwnStorage, srcInvPos, dstVipPos, count);

    public void SendVipWarehouseOutput(int itemId, byte srcVipPos, byte dstInvPos, int count)
        => SendVipWarehouseMove(VipWhOutputSub, itemId, VipVaultContents.OwnStorage, srcVipPos, dstInvPos, count);

    public void SendVipWarehouseMoveInside(int itemId, byte from, byte to)
    {
        var p = new Packet(GameOpcodes.GS_VIP_WAREHOUSE);
        p.WriteByte(VipWhMoveSub);
        p.WriteInt(0);
        p.WriteInt(itemId);
        p.WriteByte(VipVaultContents.OwnStorage);
        p.WriteByte(from);
        p.WriteByte(to);
        _conn.Send(p);
    }

    private void SendVipWarehouseMove(byte sub, int itemId, byte page, byte srcPos, byte dstPos, int count)
    {
        var p = new Packet(GameOpcodes.GS_VIP_WAREHOUSE);
        p.WriteByte(sub);
        p.WriteInt(0);
        p.WriteInt(itemId);
        p.WriteByte(page);
        p.WriteByte(srcPos);
        p.WriteByte(dstPos);
        p.WriteInt(count);
        _conn.Send(p);
    }

    public void SendVipWarehouseSetPin(string pin) => SendVipWarehousePin(VipWhSetPinSub, pin);

    public void SendVipWarehouseChangePin(string pin) => SendVipWarehousePin(VipWhChangePinSub, pin);

    public void SendVipWarehouseEnterPin(string pin) => SendVipWarehousePin(VipWhEnterPinSub, pin);

    private void SendVipWarehousePin(byte sub, string pin)
    {
        var p = new Packet(GameOpcodes.GS_VIP_WAREHOUSE);
        p.WriteByte(sub);
        p.WriteSByteString(pin);
        _conn.Send(p);
    }

    public void SendVipWarehouseCancelPin()
    {
        var p = new Packet(GameOpcodes.GS_VIP_WAREHOUSE);
        p.WriteByte(VipWhCancelPinSub);
        _conn.Send(p);
    }
}
