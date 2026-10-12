using System;
using System.Collections.Generic;

namespace LibreKO.Domain;

public sealed class PartyRejoin
{
    public const int TipOnText = 44730;
    public const int TipOffText = 44731;
    public const int TurnedOnText = 44732;
    public const int TurnedOffText = 44733;

    private readonly Dictionary<int, string> _names = new();

    public bool On { get; private set; } = true;

    public void Toggle() => On = !On;

    public void Remember(int id, string name)
    {
        if (name.Length > 0) _names[id] = name;
    }

    public void Forget(int id) => _names.Remove(id);

    public void Clear() => _names.Clear();

    public bool Recall(string name, bool amLeader, int partyCount, int maxMembers, bool alreadyMember)
    {
        if (!On || !amLeader || partyCount >= maxMembers || alreadyMember) return false;
        foreach (var (id, remembered) in _names)
        {
            if (!string.Equals(remembered, name, StringComparison.OrdinalIgnoreCase)) continue;
            _names.Remove(id);
            return true;
        }
        return false;
    }
}
