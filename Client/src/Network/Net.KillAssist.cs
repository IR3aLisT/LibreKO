using System;

namespace LibreKO.Network;

public readonly record struct KillStreakReply(byte Kind, int CharacterId, string Name, byte Nation, int Count);

public readonly record struct AssistReply(int CharacterId, int Count, byte Grade);

public static class KillAssistReplies
{
    public const byte KillSub = 1;
    public const byte AssistSub = 2;

    public static KillStreakReply? ReadKill(Packet p)
    {
        if (p.RemainingBytes < 1 + 4 + 2) return null;
        byte kind = p.ReadByte();
        int id = p.ReadInt();
        string name = p.ReadString();
        if (p.RemainingBytes < 1 + 2) return null;
        byte nation = p.ReadByte();
        return new KillStreakReply(kind, id, name, nation, p.ReadUShort());
    }

    public static AssistReply? ReadAssist(Packet p)
    {
        if (p.RemainingBytes < 4 + 2 + 1) return null;
        return new AssistReply(p.ReadInt(), p.ReadUShort(), p.ReadByte());
    }
}

public partial class Net
{
    public event Action<KillStreakReply>? KillStreakEvent;
    public event Action<AssistReply>? AssistEvent;

    private void HandleKillAssist(Packet p)
    {
        if (p.RemainingBytes < 1) return;
        switch (p.ReadByte())
        {
            case KillAssistReplies.KillSub:
                if (KillAssistReplies.ReadKill(p) is { } kill) KillStreakEvent?.Invoke(kill);
                break;
            case KillAssistReplies.AssistSub:
                if (KillAssistReplies.ReadAssist(p) is { } assist) AssistEvent?.Invoke(assist);
                break;
        }
    }
}
