using System;
using System.Collections.Generic;

namespace LibreKO.Network;

public partial class Net
{
    public event Action<VanguardReply>? VanguardEvent;

    public bool IsVanguard { get; private set; }
    public List<string> WantedNames { get; } = new();
    public int WantedCount { get; private set; }
    public List<VanguardMark> VanguardMarks { get; } = new();

    private void HandleVanguard(Packet p)
    {
        if (VanguardReply.Read(p) is not { } reply) return;
        switch (reply.Kind)
        {
            case VanguardReplyKind.Appointed:
                IsVanguard = true;
                break;
            case VanguardReplyKind.AppointFailed:
                IsVanguard = false;
                break;
            case VanguardReplyKind.List:
                WantedNames.Clear();
                WantedNames.AddRange(reply.Names);
                WantedCount = reply.Names.Count;
                break;
            case VanguardReplyKind.Positions:
                WantedNames.Clear();
                WantedNames.AddRange(reply.Names);
                VanguardMarks.Clear();
                VanguardMarks.AddRange(reply.Marks);
                break;
            case VanguardReplyKind.Remove:
                ForgetWanted(reply.Names[0]);
                break;
            case VanguardReplyKind.Closed:
                ResetVanguard();
                break;
        }
        VanguardEvent?.Invoke(reply);
    }

    public bool ForgetWanted(string name) =>
        WantedNames.RemoveAll(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)) > 0;

    public void SendVanguardAccept()
    {
        var p = new Packet(GameOpcodes.GS_VANGUARD);
        p.WriteByte(VanguardReply.SubAppoint);
        p.WriteByte(VanguardReply.AcceptRequest);
        _conn.Send(p);
    }

    private void ResetVanguard()
    {
        IsVanguard = false;
        WantedNames.Clear();
        VanguardMarks.Clear();
    }
}
