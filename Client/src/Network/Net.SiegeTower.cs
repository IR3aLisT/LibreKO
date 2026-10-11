using System;
using LibreKO.Domain;

namespace LibreKO.Network;

public readonly record struct SiegeTowerReply(byte Sub, byte Result, int UserId, int NpcId, float X, float Z)
{
    private const int UseBytes = 2 * sizeof(int) + 3 * sizeof(ushort);
    private const int ExitBytes = 2 * sizeof(int);

    public static SiegeTowerReply? Read(Packet p)
    {
        if (p.RemainingBytes < 2) return null;
        byte sub = p.ReadByte();
        byte result = p.ReadByte();
        int user = 0, npc = 0;
        float x = 0f, z = 0f;
        if (result == SiegeTower.Succeeded && sub == SiegeTower.SubUse && p.RemainingBytes >= UseBytes)
        {
            user = p.ReadInt();
            npc = p.ReadInt();
            x = (float)p.ReadUShort() / SiegeTower.WireScale;
            z = (float)p.ReadUShort() / SiegeTower.WireScale;
        }
        else if (result == SiegeTower.Succeeded && sub == SiegeTower.SubExit && p.RemainingBytes >= ExitBytes)
        {
            user = p.ReadInt();
            npc = p.ReadInt();
        }
        return new SiegeTowerReply(sub, result, user, npc, x, z);
    }
}

public partial class Net
{
    public event Action<SiegeTowerReply>? SiegeTowerEvent;

    public bool SiegeTowerPending { get; private set; }

    private void HandleSiegeTower(Packet p)
    {
        SiegeTowerPending = false;
        if (SiegeTowerReply.Read(p) is { } reply) SiegeTowerEvent?.Invoke(reply);
    }

    public bool SendSiegeTowerBoard(int towerId) => SendSiegeTower(SiegeTower.SubBoard, p => p.WriteInt(towerId));

    public bool SendSiegeTowerUse(int npcId) => SendSiegeTower(SiegeTower.SubUse, p => p.WriteInt(npcId));

    public bool SendSiegeTowerLeave(ushort x, ushort z) => SendSiegeTower(SiegeTower.SubLeave, p =>
    {
        p.WriteUShort(x);
        p.WriteUShort(z);
    });

    public bool SendSiegeTowerExit() => SendSiegeTower(SiegeTower.SubExit, null);

    private bool SendSiegeTower(byte sub, Action<Packet>? body)
    {
        if (SiegeTowerPending) return false;
        var p = new Packet(GameOpcodes.GS_SIEGE_TOWER);
        p.WriteByte(sub);
        body?.Invoke(p);
        _conn.Send(p);
        SiegeTowerPending = true;
        return true;
    }
}
