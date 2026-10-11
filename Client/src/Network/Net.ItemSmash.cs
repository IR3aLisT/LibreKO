using System;

namespace LibreKO.Network;

public partial class Net
{
    public const byte ItemSmashSub = 13;

    public event Action<ItemSmashReply>? ItemSmashReplyEvent;

    public void SendItemSmash(int itemId, int bagSlot, int npcRuntimeId)
    {
        var p = new Packet(GameOpcodes.GS_ITEM_UPGRADE);
        p.WriteByte(ItemSmashSub);
        p.WriteInt(itemId);
        p.WriteByte((byte)bagSlot);
        p.WriteInt(npcRuntimeId);
        _conn.Send(p);
    }

    private void HandleItemSmashReply(Packet p)
    {
        if (ItemSmashReply.Read(p) is { } reply)
            ItemSmashReplyEvent?.Invoke(reply);
    }
}
