using System;
using System.Collections.Generic;

namespace LibreKO.Domain;

public sealed class DestroyQueue
{
    public readonly record struct Entry(int Slot, int ItemId);

    private readonly List<Entry> _entries = new();

    public IReadOnlyList<Entry> Entries => _entries;
    public bool Running { get; private set; }
    public int Count => _entries.Count;

    public bool Add(int slot, int itemId)
    {
        if (Running || _entries.Exists(e => e.Slot == slot)) return false;
        _entries.Add(new Entry(slot, itemId));
        return true;
    }

    public void Remove(int slot)
    {
        if (!Running) _entries.RemoveAll(e => e.Slot == slot);
    }

    public void Clear()
    {
        _entries.Clear();
        Running = false;
    }

    public void DropStale(Func<int, int, bool> stillThere)
    {
        if (!Running) _entries.RemoveAll(e => !stillThere(e.Slot, e.ItemId));
    }

    public Entry? Start()
    {
        if (Running || _entries.Count == 0) return null;
        Running = true;
        return _entries[0];
    }

    public Entry? Advance(bool ok)
    {
        if (!Running || _entries.Count == 0) return null;
        _entries.RemoveAt(0);
        if (ok && _entries.Count > 0) return _entries[0];
        Clear();
        return null;
    }
}
