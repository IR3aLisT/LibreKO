using System;
using LibreKO.Domain;

namespace LibreKO.Network;

public partial class Net
{
    public event Action<int, ItemSlot[]>? ClanWhContentsEvent;

    public event Action<byte, byte>? ClanWhResultEvent;

    public const byte ClanWhFailed = 0;
    public const byte ClanWhSucceeded = 1;
    public const byte ClanWhNotInClan = 2;
    public const byte ClanWhNoRights = 3;
    public const byte ClanWhNotStorable = 4;
    public const byte ClanWhOpenSub = 1;
    public const byte ClanWhInputSub = 2;
    public const byte ClanWhOutputSub = 3;
    public const byte ClanWhMoveSub = 4;

    public const int ClanWhNetSlots = 192;

    private void HandleClanWarehouse(Packet p)
    {
        if (p.RemainingBytes < 1) return;
        byte sub = p.ReadByte();
        switch (sub)
        {
            case ClanWhOpenSub:
            {
                byte result = p.RemainingBytes >= 1 ? p.ReadByte() : ClanWhFailed;
                if (result != ClanWhSucceeded) { ClanWhResultEvent?.Invoke(sub, result); break; }
                int money = p.RemainingBytes >= 4 ? p.ReadInt() : 0;
                var slots = new ItemSlot[ClanWhNetSlots];
                for (int i = 0; i < ClanWhNetSlots && p.RemainingBytes >= 17; i++)
                {
                    int itemId = p.ReadInt();
                    short dur = UShortToShort(p.ReadUShort());
                    short count = UShortToShort(p.ReadUShort());
                    byte flag = p.ReadByte();
                    SkipBytes(p, 8);
                    slots[i] = itemId == 0 ? default : new ItemSlot { ItemId = itemId, Durability = dur, Count = count, Flag = flag };
                }
                ClanWhContentsEvent?.Invoke(money, slots);
                break;
            }
            case ClanWhInputSub: case ClanWhOutputSub: case ClanWhMoveSub: case 5:
                ClanWhResultEvent?.Invoke(sub, p.RemainingBytes >= 1 ? p.ReadByte() : ClanWhFailed);
                break;
        }
    }

    public void SendClanWhOpen()
    {
        var p = new Packet(GameOpcodes.GS_CLAN_WAREHOUSE);
        p.WriteByte(ClanWhOpenSub);
        _conn.Send(p);
    }

    public void SendClanWhMoveInside(int itemId, byte page, byte srcPos, byte dstPage, byte dstPos)
    {
        var p = new Packet(GameOpcodes.GS_CLAN_WAREHOUSE);
        p.WriteByte(ClanWhMoveSub);
        p.WriteInt(0);
        p.WriteInt(itemId);
        p.WriteByte(page);
        p.WriteByte(srcPos);
        p.WriteByte(dstPos);
        p.WriteByte(dstPage);
        _conn.Send(p);
    }

    public void SendClanWhInput(int itemId, byte page, byte srcInvPos, byte dstWhPos, int count)
        => SendClanWhMove(ClanWhInputSub, itemId, page, srcInvPos, dstWhPos, count);

    public void SendClanWhOutput(int itemId, byte page, byte srcWhPos, byte dstInvPos, int count)
        => SendClanWhMove(ClanWhOutputSub, itemId, page, srcWhPos, dstInvPos, count);

    private void SendClanWhMove(byte sub, int itemId, byte page, byte srcPos, byte dstPos, int count)
    {
        var p = new Packet(GameOpcodes.GS_CLAN_WAREHOUSE);
        p.WriteByte(sub);
        p.WriteInt(0);
        p.WriteInt(itemId);
        p.WriteByte(page);
        p.WriteByte(srcPos);
        p.WriteByte(dstPos);
        p.WriteInt(count);
        _conn.Send(p);
    }
}
