using System;
using LibreKO.Domain;

namespace LibreKO.Network;

public partial class Net
{
    public event Action<SoccerGoal>? SoccerGoalEvent;
    public event Action<int>? SoccerClockEvent;
    public event Action<int, int, int>? SoccerSyncEvent;
    public event Action<SoccerEnd>? SoccerEndEvent;

    public int SoccerTeam { get; set; }

    private void HandleSoccer(Packet p)
    {
        if (p.RemainingBytes < 1) return;
        switch (p.ReadByte())
        {
            case Soccer.SubGoal when ReadSoccerGoal(p) is { } goal:
                SoccerGoalEvent?.Invoke(goal);
                break;
            case Soccer.SubClock when p.RemainingBytes >= 2:
                SoccerClockEvent?.Invoke(Math.Max(0, (int)p.ReadShort()));
                break;
            case Soccer.SubSync when p.RemainingBytes >= 5:
                p.ReadByte();
                int seconds = Math.Max(0, (int)p.ReadShort());
                SoccerSyncEvent?.Invoke(seconds, p.ReadByte(), p.ReadByte());
                break;
            case Soccer.SubEnd when p.RemainingBytes >= 3:
                SoccerEndEvent?.Invoke(new SoccerEnd(p.ReadByte(), p.ReadByte(), p.ReadByte()));
                break;
        }
    }

    public void SendEmote(int action)
    {
        var p = new Packet(GameOpcodes.GS_STATE_CHANGE);
        p.WriteByte(StateChange.Action);
        p.WriteInt(action);
        _conn.Send(p);
    }

    internal static SoccerGoal? ReadSoccerGoal(Packet p) =>
        p.RemainingBytes < 8 ? null : new SoccerGoal(p.ReadInt(), p.ReadByte(), p.ReadByte(), p.ReadByte(), p.ReadByte());
}
