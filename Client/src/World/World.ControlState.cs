using System.Collections.Generic;
using Godot;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const string StatusFrozenText = "Frozen";
    private const string StatusStunnedText = "Stunned";
    private const string StatusBlindedText = "Blinded";
    private const string StatusSlowedText = "Slowed";
    private const double MillisecondsPerSecond = 1000.0;

    private static readonly Color StatusFrozenColour = new(0.6f, 0.85f, 1f);
    private static readonly Color StatusStunnedColour = new(1f, 0.9f, 0.3f);
    private static readonly Color StatusBlindedColour = new(0.75f, 0.5f, 1f);
    private static readonly Color StatusSlowedColour = new(0.4f, 0.6f, 1f);

    private readonly Dictionary<int, double> _controlUntil = new();
    private readonly List<int> _controlExpired = new();

    private void ControlStateInit() => Net.I.ControlStateEvent += OnControlState;

    private void ControlStateDispose() => Net.I.ControlStateEvent -= OnControlState;

    private void OnControlState(ControlStateReply reply)
    {
        var (text, colour) = StatusLine(reply.Flags);
        if (text.Length == 0)
        {
            ClearControlStatus(reply.UnitId);
            return;
        }
        _controlUntil[reply.UnitId] = Now() + reply.LongestMs / MillisecondsPerSecond;
        PlateOf(reply.UnitId)?.SetStatus(text, colour);
    }

    private static (string Text, Color Colour) StatusLine(byte flags) =>
        (flags & ControlStateReply.Frozen) != 0 ? (StatusFrozenText, StatusFrozenColour)
        : (flags & ControlStateReply.Stunned) != 0 ? (StatusStunnedText, StatusStunnedColour)
        : (flags & ControlStateReply.Blinded) != 0 ? (StatusBlindedText, StatusBlindedColour)
        : (flags & (ControlStateReply.Slowed | ControlStateReply.AttackSlowed)) != 0 ? (StatusSlowedText, StatusSlowedColour)
        : ("", Colors.White);

    private PlateStack? PlateOf(int id) =>
        id == _myId ? SelfPlate() : _ents.TryGetValue(id, out var e) ? e.Plate : null;

    private void ControlStateTick()
    {
        if (_controlUntil.Count == 0) return;
        double now = Now();
        _controlExpired.Clear();
        foreach (var (id, until) in _controlUntil)
            if (now >= until) _controlExpired.Add(id);
        foreach (int id in _controlExpired)
            ClearControlStatus(id);
    }

    private void ClearControlStatus(int id)
    {
        _controlUntil.Remove(id);
        PlateOf(id)?.SetStatus("", Colors.White);
    }
}
