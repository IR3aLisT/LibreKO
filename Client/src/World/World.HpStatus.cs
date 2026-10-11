using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const byte StatusBlind = 4;
    private const int BlindFx = 30011;
    private const double HpStatusCycleSeconds = 0.5;

    private Godot.Timer? _hpStatusTick;
    private Node3D? _blindFx;

    private void HpStatusInit()
    {
        Net.I.OwnStatusEvent += OnOwnStatus;
        _hpStatusTick = new Godot.Timer { WaitTime = HpStatusCycleSeconds, Autostart = true };
        _hpStatusTick.Timeout += RefreshHpStatusBars;
        AddChild(_hpStatusTick);
    }

    private void HpStatusDispose()
    {
        Net.I.OwnStatusEvent -= OnOwnStatus;
        ClearBlindFx();
    }

    private void OnOwnStatus(byte type, bool inflicted)
    {
        if (type == StatusBlind)
        {
            if (inflicted) ShowBlindFx(); else ClearBlindFx();
            return;
        }
        RefreshHpStatusBars();
    }

    private void RefreshHpStatusBars()
    {
        long second = (long)Now();
        if (_hpBar != null && IsInstanceValid(_hpBar))
            _hpBar.FillOverride = StatusFill(Net.I.OwnStatus, second);
        if (_targetHp != null && IsInstanceValid(_targetHp))
            _targetHp.FillOverride = _targetHudId >= 0 ? StatusFill(Net.I.PartyStatusOf(_targetHudId), second) : null;
        PluginNotifyTarget();
    }

    private static Color? StatusFill(System.Collections.Generic.IReadOnlyCollection<byte>? statuses, long second) =>
        PartyHud.HpLook(statuses, second) is var look and not PartyHpLook.Normal ? PartyHpFills[look] : null;

    private void ShowBlindFx()
    {
        if (_blindFx != null && IsInstanceValid(_blindFx)) return;
        if (Fx.NameForId(BlindFx) is not { } name) return;
        _blindFx = Fx.Spawn(name, _self, Vector3.Zero);
    }

    private void ClearBlindFx()
    {
        if (_blindFx != null && IsInstanceValid(_blindFx)) _blindFx.QueueFree();
        _blindFx = null;
    }
}
