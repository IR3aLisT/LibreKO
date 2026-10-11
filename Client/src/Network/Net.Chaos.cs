using System;
using Godot;
using LibreKO.Domain;

namespace LibreKO.Network;

public partial class Net
{
    public const byte DashboardStyle = 7;
    private const int DashboardSubOffsetBytes = 8;
    private const uint DashboardEventSubA = 8;
    private const uint DashboardEventSubB = 9;
    private const uint DashboardEventSubC = 13;
    private const ulong MsPerSecond = 1000UL;

    public event Action<ChaosRanking>? ChaosRankEvent;
    public event Action? ChaosStartedEvent;
    public event Action<uint>? ChaosFinishEvent;

    public bool ChaosStarted { get; private set; }
    public bool ChaosOver { get; private set; }
    public int DashboardEventId { get; private set; }
    public bool ChaosBannerPrinted { get; set; }
    public int[]? ChaosHotbar { get; set; }
    private ulong _chaosFinishEndsAtMs;

    public int ChaosFinishSecondsLeft =>
        _chaosFinishEndsAtMs == 0 ? 0 : (int)Math.Max(0, ((long)_chaosFinishEndsAtMs - (long)Time.GetTicksMsec()) / (long)MsPerSecond);

    public bool ChaosFinished => _chaosFinishEndsAtMs != 0;

    public void ResetChaosBattle()
    {
        ChaosStarted = false;
        ChaosOver = false;
        DashboardEventId = 0;
        ChaosBannerPrinted = false;
        _chaosFinishEndsAtMs = 0;
    }

    public void ConsumeChaosOver() => ChaosOver = false;

    private bool TryHandleDashboard(Packet p)
    {
        if (p.RemainingBytes < 5) return false;
        p.ReadInt();
        byte style = p.ReadByte();
        if (style != DashboardStyle)
        {
            p.ResetOffset();
            return false;
        }
        if (p.RemainingBytes < DashboardSubOffsetBytes + 4) return true;
        p.ReadBytes(DashboardSubOffsetBytes);
        uint sub = p.ReadUInt();
        if (sub is DashboardEventSubA or DashboardEventSubB or DashboardEventSubC && p.RemainingBytes >= 1)
            DashboardEventId = p.ReadByte();
        return true;
    }

    private void NoteChaosState(int charId, int type, int value)
    {
        if (type != StateChange.Abnormal || value != SizeState.ChaosNormal || LastEnter.Zone != ChaosBattle.Zone || ChaosStarted)
            return;
        ChaosStarted = true;
        ChaosStartedEvent?.Invoke();
    }

    private bool TryHandleChaosFinish(ushort eventId, uint seconds)
    {
        if (eventId != ChaosBattle.IndunEvent) return false;
        _chaosFinishEndsAtMs = Time.GetTicksMsec() + seconds * MsPerSecond;
        if (ChaosStarted) ChaosOver = true;
        ChaosFinishEvent?.Invoke(seconds);
        return true;
    }
}
