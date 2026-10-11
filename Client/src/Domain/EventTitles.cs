using System.Collections.Generic;

namespace LibreKO.Domain;

public sealed class EventTitles
{
    private readonly List<EventTitle> _entries = new();

    public IReadOnlyList<EventTitle> Entries => _entries;
    public bool Expanded { get; set; }

    public int ShownCount => _entries.Count == 0 ? 0 : Expanded ? _entries.Count : 1;
    public bool HasToggle => _entries.Count > 1;

    public void Show(string title, string message)
    {
        int at = _entries.FindIndex(entry => entry.Title == title);
        if (at >= 0) _entries[at] = new EventTitle(title, message);
        else _entries.Add(new EventTitle(title, message));
    }

    public bool Remove(string title) => _entries.RemoveAll(entry => entry.Title == title) > 0;

    public void Clear()
    {
        _entries.Clear();
        Expanded = false;
    }
}

public readonly record struct EventTitle(string Title, string Message);
