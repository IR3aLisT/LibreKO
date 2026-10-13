using System;
using LibreKO.Domain;

namespace LibreKO.Network;

public partial class Net
{
    public event Action<string, string>? RankerPaperEvent;
    public event Action<byte, short>? RankerResultEvent;

    private void HandleRankerMessage(Packet p)
    {
        if (p.RemainingBytes < 3) return;
        byte sub = p.ReadByte();
        short result = p.ReadShort();
        if (sub == RankerMessage.SubView && result == RankerMessage.Done)
        {
            string name = p.RemainingBytes >= 2 ? p.ReadString() : "";
            string message = p.RemainingBytes >= 2 ? p.ReadString() : "";
            RankerPaperEvent?.Invoke(name, message);
            return;
        }
        RankerResultEvent?.Invoke(sub, result);
    }

    public void SendRankerView(byte npcType) => SendRanker(RankerMessage.SubView, npcType);

    public void SendRankerRedress(byte npcType) => SendRanker(RankerMessage.SubRedress, npcType);

    public void SendRankerWrite(byte npcType, string message)
    {
        var p = new Packet(GameOpcodes.GS_RANKER_MESSAGE);
        p.WriteByte(RankerMessage.SubWrite);
        p.WriteByte(npcType);
        p.WriteString(message);
        _conn.Send(p);
    }

    private void SendRanker(byte sub, byte npcType)
    {
        var p = new Packet(GameOpcodes.GS_RANKER_MESSAGE);
        p.WriteByte(sub);
        p.WriteByte(npcType);
        _conn.Send(p);
    }
}
