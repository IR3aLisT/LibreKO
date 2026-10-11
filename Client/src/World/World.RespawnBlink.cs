using System.Collections.Generic;
using Godot;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private sealed class BlinkState
    {
        public double Since;
        public bool? Dimmed;
    }

    private readonly Dictionary<int, BlinkState> _blinking = new();
    private readonly List<int> _blinkGone = new();

    private void RespawnBlinkInit()
    {
        Net.I.StateChangeEvent += OnRespawnBlinkState;
        if (Net.I.SelfAbnormal == RespawnBlink.Blinking) StartRespawnBlink(_myId);
    }

    private void RespawnBlinkDispose() => Net.I.StateChangeEvent -= OnRespawnBlinkState;

    private void OnRespawnBlinkState(int charId, int type, int value)
    {
        if (type != StateChange.Abnormal) return;
        if (value == RespawnBlink.Blinking) StartRespawnBlink(charId);
        else if (RespawnBlink.Ends(value) && _blinking.Remove(charId)) EndRespawnBlink(charId);
    }

    private void StartRespawnBlink(int charId) => _blinking[charId] = new BlinkState { Since = Now() };

    private void EndRespawnBlink(int charId)
    {
        if (StealthBodyOf(charId) is { } body) SetBlinkAlpha(body, 1f);
        if (_stealthIds.Contains(charId)) ApplyStealthFade(charId, stealthed: true);
    }

    private void RespawnBlinkTick(double now)
    {
        if (_blinking.Count == 0) return;
        _blinkGone.Clear();
        foreach (var (id, state) in _blinking)
        {
            if (StealthBodyOf(id) is not { } body)
            {
                _blinkGone.Add(id);
                continue;
            }
            bool dim = RespawnBlink.Dimmed(now - state.Since);
            if (dim == state.Dimmed) continue;
            state.Dimmed = dim;
            SetBlinkAlpha(body, RespawnBlink.AlphaAt(now - state.Since));
        }
        foreach (int id in _blinkGone) _blinking.Remove(id);
    }

    private static void SetBlinkAlpha(Node3D body, float alpha)
    {
        float transparency = Mathf.Clamp(1f - alpha, 0f, 1f);
        foreach (var node in StealthDescendants(body))
            if (node is GeometryInstance3D gi)
                Fx.SetTransparency(gi, transparency);
    }
}
