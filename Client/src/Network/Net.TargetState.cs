using System;

namespace LibreKO.Network;

public partial class Net
{
    public event Action<TargetEffectsReply>? TargetEffectsEvent;

    public event Action<ControlStateReply>? ControlStateEvent;

    public event Action<DropListReply>? DropListEvent;

    private void HandleTargetState(Packet p)
    {
        if (p.RemainingBytes < 1) return;
        switch (p.ReadByte())
        {
            case TargetEffectsReply.SubEffects:
                if (TargetEffectsReply.Read(p) is { } effects) TargetEffectsEvent?.Invoke(effects);
                break;
            case DropListReply.SubDropList:
                if (DropListReply.Read(p) is { } drops) DropListEvent?.Invoke(drops);
                break;
            case ControlStateReply.SubControl:
                if (ControlStateReply.Read(p) is { } control) ControlStateEvent?.Invoke(control);
                break;
        }
    }

    public void SendDropListRequest(int targetId)
    {
        var p = new Packet(GameOpcodes.GS_TARGET_STATE);
        p.WriteByte(DropListReply.SubDropList);
        p.WriteInt(targetId);
        _conn.Send(p);
    }

    public void SendTargetEffectsRequest(int targetId)
    {
        var p = new Packet(GameOpcodes.GS_TARGET_STATE);
        p.WriteByte(TargetEffectsReply.SubEffects);
        p.WriteInt(targetId);
        _conn.Send(p);
    }
}
