using System;
using Godot;
using LibreKO.Domain;

namespace LibreKO.Network;

public partial class Net
{
    public event Action<RoyaleSignUp>? RoyaleSignUpEvent;
    public event Action<RoyaleEntry>? RoyaleEntryEvent;
    public event Action<RoyaleStats>? RoyaleStatsEvent;
    public event Action<int>? RoyaleExpEvent;
    public event Action<string, int>? RoyaleAbandonedEvent;
    public event Action<int, int>? RoyaleGasEvent;
    public event Action<int, int, bool>? RoyalePingEvent;
    public event Action<KnightRoyale.Reward>? RoyaleRewardEvent;
    public event Action<string, int>? RoyaleWinnerEvent;
    public event Action<RoyaleLoot>? RoyaleLootEvent;
    public event Action<RoyaleItem>? RoyaleItemEvent;
    public event Action? RoyaleRestoreEvent;
    public event Action<bool>? RoyaleDestroyEvent;

    public int RoyaleSurvivors { get; private set; }
    public int RoyaleReleasedArea { get; private set; }
    public int RoyaleNextArea { get; private set; }
    public bool RoyaleCountdown { get; private set; }
    private double _royaleNextGasAt;

    public int RoyaleNextGasSeconds =>
        RoyaleCountdown ? (int)Math.Max(0, Math.Ceiling(_royaleNextGasAt - Time.GetTicksMsec() / 1000.0)) : 0;

    private void HandleKnightRoyale(Packet p)
    {
        if (p.RemainingBytes < 1) return;
        byte sub = p.ReadByte();
        switch (sub)
        {
            case KnightRoyaleWire.SubSignUp:
                RoyaleSignUpEvent?.Invoke(KnightRoyaleWire.ReadSignUp(p));
                break;
            case KnightRoyaleWire.SubEntry:
                var entry = KnightRoyaleWire.ReadEntry(p);
                if (entry.Result != 1) break;
                RoyaleReleasedArea = 0;
                RoyaleNextArea = 0;
                RoyaleCountdown = true;
                RoyaleEntryEvent?.Invoke(entry);
                break;
            case KnightRoyaleWire.SubProgress:
                HandleRoyaleProgress(p);
                break;
            case KnightRoyaleWire.SubNotice:
                HandleRoyaleNotice(p);
                break;
            case KnightRoyaleWire.SubLoot:
                RoyaleLootEvent?.Invoke(KnightRoyaleWire.ReadLoot(p));
                break;
            case KnightRoyaleWire.SubItemAdd:
                RoyaleItemEvent?.Invoke(KnightRoyaleWire.ReadItem(p));
                break;
            case KnightRoyaleWire.SubRestore:
                RoyaleCountdown = false;
                RoyaleRestoreEvent?.Invoke();
                break;
            case KnightRoyaleWire.SubDestroy:
                RoyaleDestroyEvent?.Invoke(p.RemainingBytes >= 1 && p.ReadByte() == 1);
                break;
        }
    }

    private void HandleRoyaleProgress(Packet p)
    {
        if (p.RemainingBytes < 1) return;
        byte kind = p.ReadByte();
        if (kind == KnightRoyaleWire.ProgressExp && p.RemainingBytes >= 2) RoyaleExpEvent?.Invoke(p.ReadShort());
        else if (kind == KnightRoyaleWire.ProgressStats) RoyaleStatsEvent?.Invoke(KnightRoyaleWire.ReadStats(p));
    }

    private void HandleRoyaleNotice(Packet p)
    {
        if (p.RemainingBytes < 1) return;
        byte kind = p.ReadByte();
        switch (kind)
        {
            case KnightRoyaleWire.NoticeAbandoned:
                var (name, survivors) = KnightRoyaleWire.ReadNamed(p);
                RoyaleSurvivors = survivors;
                RoyaleAbandonedEvent?.Invoke(name, survivors);
                break;
            case KnightRoyaleWire.NoticeGas:
                if (p.RemainingBytes < 2) break;
                int released = p.ReadByte();
                int minutes = p.ReadByte();
                RoyaleReleasedArea = released;
                RoyaleNextArea = released + 1;
                RoyaleCountdown = !KnightRoyale.GasEnds(RoyaleNextArea);
                _royaleNextGasAt = Time.GetTicksMsec() / 1000.0 + minutes * 60.0;
                RoyaleGasEvent?.Invoke(released, minutes * 60);
                break;
            case KnightRoyaleWire.NoticeChest:
            case KnightRoyaleWire.NoticeDelegate:
                var (x, z) = KnightRoyaleWire.ReadSpot(p);
                RoyalePingEvent?.Invoke(x, z, kind == KnightRoyaleWire.NoticeDelegate);
                break;
            case KnightRoyaleWire.NoticeReward:
                RoyaleRewardEvent?.Invoke(KnightRoyaleWire.ReadReward(p));
                break;
            case KnightRoyaleWire.NoticeWinner:
                var (winner, area) = KnightRoyaleWire.ReadNamed(p);
                RoyaleWinnerEvent?.Invoke(winner, area);
                break;
            case KnightRoyaleWire.NoticeSurvivors:
                if (p.RemainingBytes >= 1) RoyaleSurvivors = p.ReadByte();
                RoyaleAbandonedEvent?.Invoke("", RoyaleSurvivors);
                break;
        }
    }

    public void SendRoyaleSignUp(bool apply)
    {
        var p = new Packet(GameOpcodes.GS_KNIGHT_ROYALE);
        p.WriteByte(KnightRoyaleWire.SubSignUp);
        p.WriteByte(apply ? KnightRoyaleWire.Apply : KnightRoyaleWire.Cancel);
        _conn.Send(p);
    }

    public void SendRoyaleTake(int boxId, int itemId, int index)
    {
        var p = new Packet(GameOpcodes.GS_KNIGHT_ROYALE);
        p.WriteByte(KnightRoyaleWire.SubLoot);
        p.WriteInt(boxId);
        p.WriteInt(itemId);
        p.WriteUShort((ushort)index);
        _conn.Send(p);
    }

    public void SendRoyaleDestroy(int bagSlot, int itemId)
    {
        var p = new Packet(GameOpcodes.GS_KNIGHT_ROYALE);
        p.WriteByte(KnightRoyaleWire.SubDestroy);
        p.WriteByte((byte)bagSlot);
        p.WriteInt(itemId);
        _conn.Send(p);
    }
}
