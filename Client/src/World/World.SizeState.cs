using System.Collections.Generic;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private sealed class SizeTween
    {
        public Node3D Model = null!;
        public Vector3 From;
        public Vector3 To;
        public double Start;
    }

    private readonly Dictionary<int, SizeTween> _sizeTweens = new();
    private readonly Dictionary<int, Node3D> _sizeFx = new();
    private readonly Dictionary<int, int> _sizeStates = new();
    private const int TransformedRace = 0;
    private readonly List<int> _sizeDone = new();

    private void SizeStateInit()
    {
        Net.I.StateChangeEvent += OnSizeState;
        ApplySizeState(_myId, SizeState.OnSpawn(Net.I.SelfAbnormal, _selfRace, _zone), instant: true);
    }

    private void SizeStateDispose() => Net.I.StateChangeEvent -= OnSizeState;

    private void OnSizeState(int charId, int type, int value)
    {
        if (type == StateChange.Abnormal) ApplySizeState(charId, value, instant: false);
    }

    private void SizeStateOnSpawn(int charId, int recorded, int race) =>
        ApplySizeState(charId, SizeState.OnSpawn(recorded, race, _zone), instant: true);

    private void SizeStateForgetEntity(int id)
    {
        _sizeTweens.Remove(id);
        _sizeStates.Remove(id);
        if (_sizeFx.Remove(id, out var fx) && IsInstanceValid(fx)) Fx.Free(fx);
    }

    private Node3D? SizeBodyOf(int charId) =>
        charId == _myId ? _selfVisual : _ents.TryGetValue(charId, out var e) && !e.IsNpc ? e.Body : null;

    private Node3D? SizeModelOf(Node3D body, int charId, out bool transformed)
    {
        transformed = false;
        if (TransformHost(charId)?.GetNodeOrNull<Node3D>(TransformNodeName) is { } worn)
        {
            transformed = true;
            return worn;
        }
        return body.GetNodeOrNull<Node3D>(ModelNodeName);
    }

    private void ReapplySizeState(int charId)
    {
        if (_sizeStates.TryGetValue(charId, out int state)) ApplySizeState(charId, state, instant: true);
    }

    private void ApplySizeState(int charId, int state, bool instant)
    {
        if (SizeBodyOf(charId) is not { } body || !IsInstanceValid(body)) return;
        var model = SizeModelOf(body, charId, out bool transformed);
        int race = transformed ? TransformedRace : charId == _myId ? _selfRace : _ents.TryGetValue(charId, out var e) ? e.Race : 0;
        if (SizeState.Target(state, race, _zone) != null) _sizeStates[charId] = state;
        int shown = ZoneLookHolds(charId) ? SizeState.Normal : state;
        if (SizeState.Target(shown, race, _zone) is { } target && model != null)
        {
            var to = new Vector3(target.X, target.Y, target.Z);
            if (instant)
            {
                _sizeTweens.Remove(charId);
                model.Scale = to;
            }
            else
            {
                _sizeTweens[charId] = new SizeTween { Model = model, From = model.Scale, To = to, Start = Now() };
            }
        }
        if (SizeState.AddsFx(shown) && !_sizeFx.ContainsKey(charId)
            && Fx.NameForId(SizeState.GiantTargetFxId) is { } fxName
            && Fx.Spawn(fxName, body, Vector3.Zero, oneShot: false) is { } fx)
            _sizeFx[charId] = fx;
        else if (SizeState.RemovesFx(shown) && _sizeFx.Remove(charId, out var running) && IsInstanceValid(running))
            Fx.Free(running);
    }

    private void SizeStateTick(double now)
    {
        if (_sizeTweens.Count == 0) return;
        _sizeDone.Clear();
        foreach (var (id, tween) in _sizeTweens)
        {
            if (!IsInstanceValid(tween.Model))
            {
                _sizeDone.Add(id);
                continue;
            }
            float t = SizeState.Progress(now - tween.Start);
            tween.Model.Scale = tween.From.Lerp(tween.To, t);
            if (t >= 1f) _sizeDone.Add(id);
        }
        foreach (int id in _sizeDone) _sizeTweens.Remove(id);
    }
}
