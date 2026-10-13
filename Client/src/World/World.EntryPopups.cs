using System;
using System.Collections.Generic;
using Godot;
using LibreKO.Domain;

namespace LibreKO;

public partial class World
{
    private readonly EntryPopupQueue _entryPopups = new();
    private readonly Dictionary<EntryPopup, Func<Control?>> _entryOpeners = new();
    private Control? _entryPopupShown;

    private void QueueEntryPopup(EntryPopup popup, Func<Control?> open)
    {
        _entryOpeners[popup] = open;
        _entryPopups.Add(popup);
        ShowNextEntryPopup();
    }

    private void ReleaseEntryPopups()
    {
        _entryPopups.Release();
        ShowNextEntryPopup();
    }

    private void ShowNextEntryPopup()
    {
        while (_entryPopups.TakeNext() is { } popup)
        {
            if (_entryOpeners.Remove(popup, out var open) && open() is { Visible: true } shown && IsInstanceValid(shown))
            {
                _entryPopupShown = shown;
                shown.VisibilityChanged += OnEntryPopupVisibility;
                shown.TreeExiting += OnEntryPopupGone;
                return;
            }
            _entryPopups.Closed();
        }
    }

    private void OnEntryPopupVisibility()
    {
        if (_entryPopupShown is { Visible: true }) return;
        OnEntryPopupGone();
    }

    private void OnEntryPopupGone()
    {
        if (_entryPopupShown != null && IsInstanceValid(_entryPopupShown))
        {
            _entryPopupShown.VisibilityChanged -= OnEntryPopupVisibility;
            _entryPopupShown.TreeExiting -= OnEntryPopupGone;
        }
        _entryPopupShown = null;
        _entryPopups.Closed();
        if (IsInsideTree()) Callable.From(ShowNextEntryPopup).CallDeferred();
    }
}
