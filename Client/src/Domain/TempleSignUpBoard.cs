using System;
using System.Collections.Generic;
using System.Linq;

namespace LibreKO.Domain;

public enum TempleEventType : byte
{
    None = 0,
    Chaos = 1,
    BorderDefenseWar = 2,
    JuraidMountain = 3,
    UnderTheCastle = 4,
    ForgottenTemple = 5,
}

public sealed class TempleSignUpBoard
{
    private readonly Dictionary<TempleEventType, DateTime> _closesAt = new();
    private readonly List<TempleEventType> _order = new();

    public TempleEventType Current { get; private set; }
    public TempleEventType Registered { get; private set; }
    public bool AnyOpen => _order.Count > 0;
    public IReadOnlyList<TempleEventType> Open => _order;

    public bool IsOpen(TempleEventType type) => _closesAt.ContainsKey(type);

    public bool OpenSignUp(TempleEventType type, int seconds, DateTime utcNow)
    {
        if (type == TempleEventType.None || seconds <= 0) return false;
        bool fresh = !_closesAt.ContainsKey(type);
        _closesAt[type] = utcNow.AddSeconds(seconds);
        if (!fresh) return false;
        _order.Add(type);
        if (Current == TempleEventType.None || Registered != Current) Current = type;
        return true;
    }

    public void Close(TempleEventType type)
    {
        if (!_closesAt.Remove(type)) return;
        _order.Remove(type);
        if (Registered == type) Registered = TempleEventType.None;
        if (Current == type) Current = _order.Count > 0 ? _order[^1] : TempleEventType.None;
    }

    public List<TempleEventType> Expire(DateTime utcNow)
    {
        var expired = _closesAt.Where(e => e.Value <= utcNow).Select(e => e.Key).ToList();
        foreach (var type in expired) Close(type);
        return expired;
    }

    public void CloseAll()
    {
        foreach (var type in _order.ToList()) Close(type);
    }

    public int SecondsLeft(TempleEventType type, DateTime utcNow) =>
        _closesAt.TryGetValue(type, out var at) ? Math.Max(0, (int)Math.Ceiling((at - utcNow).TotalSeconds)) : 0;

    public bool Select(TempleEventType type)
    {
        if (!_closesAt.ContainsKey(type)) return false;
        Current = type;
        return true;
    }

    public void SetRegistered(TempleEventType type)
    {
        if (_closesAt.ContainsKey(type)) Registered = type;
    }

    public void ClearRegistered() => Registered = TempleEventType.None;
}
