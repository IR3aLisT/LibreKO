using System;
using System.Collections.Generic;

namespace LibreKO.Domain;

public enum LogTab { All, State, Item, System }

public sealed class TabbedLog
{
    private readonly int _max;
    private readonly Dictionary<LogTab, Queue<string>> _lines = new();

    public TabbedLog(int max)
    {
        _max = max;
        foreach (var tab in Enum.GetValues<LogTab>())
            _lines[tab] = new Queue<string>();
    }

    public LogTab Shown { get; private set; } = LogTab.All;

    public IReadOnlyCollection<string> Lines(LogTab tab) => _lines[tab];

    public IReadOnlyCollection<string> ShownLines => _lines[Shown];

    public bool Show(LogTab tab)
    {
        if (tab == Shown) return false;
        Shown = tab;
        return true;
    }

    public bool Add(string line, LogTab tab, List<string> droppedFromShown)
    {
        droppedFromShown.Clear();
        Push(LogTab.All, line, Shown == LogTab.All ? droppedFromShown : null);
        if (tab != LogTab.All)
            Push(tab, line, Shown == tab ? droppedFromShown : null);
        return Shown == LogTab.All || Shown == tab;
    }

    private void Push(LogTab tab, string line, List<string>? dropped)
    {
        var lines = _lines[tab];
        lines.Enqueue(line);
        while (lines.Count > _max)
        {
            string old = lines.Dequeue();
            dropped?.Add(old);
        }
    }
}
