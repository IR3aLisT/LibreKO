using System;
using System.Collections.Generic;

namespace LibreKO.Domain;

public sealed class PartyOrder
{
    public const int Up = -1;
    public const int Down = 1;

    private readonly List<int> _ids = new();

    public IReadOnlyList<int> Ids => _ids;

    public List<T> Arrange<T>(IReadOnlyList<T> members, Func<T, int> idOf)
    {
        var present = new HashSet<int>();
        foreach (var member in members) present.Add(idOf(member));
        _ids.RemoveAll(id => !present.Contains(id));
        foreach (var member in members)
            if (!_ids.Contains(idOf(member))) _ids.Add(idOf(member));
        var arranged = new List<T>(members);
        arranged.Sort((a, b) => _ids.IndexOf(idOf(a)).CompareTo(_ids.IndexOf(idOf(b))));
        return arranged;
    }

    public bool Move(int id, int step)
    {
        int count = _ids.Count;
        int from = _ids.IndexOf(id);
        if (from < 0 || count < 2) return false;
        int to = ((from + step) % count + count) % count;
        (_ids[from], _ids[to]) = (_ids[to], _ids[from]);
        return true;
    }
}
