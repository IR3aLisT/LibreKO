using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const int EventTitleGap = 4;
    private static readonly Vector2 EventTitleMinSize = new(140f, 23f);
    private const int EventTitleFontSize = 12;
    private const string EventTitlesExpandGlyph = "▾";
    private const string EventTitlesCollapseGlyph = "▴";

    private VBoxContainer _eventTitleColumn = null!;

    private void EventTitlesInit()
    {
        BuildEventTitleColumn();
        _premiumLayer.AddChild(_eventTitleColumn);
        _premiumChip.Resized += PlaceEventTitles;
        Net.I.EventTitlesChangedEvent += RefreshEventTitles;
        RefreshEventTitles();
    }

    private void BuildEventTitleColumn()
    {
        _eventTitleColumn = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _eventTitleColumn.AddThemeConstantOverride("separation", EventTitleGap);
        _eventTitleColumn.Resized += PlaceEventTitles;
    }

    private void EventTitlesDispose() => Net.I.EventTitlesChangedEvent -= RefreshEventTitles;

    private void PlaceEventTitles()
    {
        if (!IsInstanceValid(_eventTitleColumn) || !IsInstanceValid(_premiumChip)) return;
        HudAnchor.Pin(_eventTitleColumn, HudAnchor.Spot.TopRight, new Vector2(
            HudAnchor.Edge + MiniMap.SquareSize + StatusHudGap,
            HudAnchor.Edge + _premiumChip.Size.Y + EventTitleGap));
    }

    private void RefreshEventTitles()
    {
        if (!IsInstanceValid(_eventTitleColumn)) return;
        foreach (var child in _eventTitleColumn.GetChildren())
        {
            _eventTitleColumn.RemoveChild(child);
            child.QueueFree();
        }
        var titles = Net.I.EventTitles;
        for (int i = 0; i < titles.ShownCount; i++)
            _eventTitleColumn.AddChild(EventTitleChip(titles.Entries[i], i == 0 && titles.HasToggle ? titles.Expanded : null));
        _eventTitleColumn.Visible = titles.ShownCount > 0;
        _eventTitleColumn.ResetSize();
        Callable.From(PlaceEventTitles).CallDeferred();
    }

    private Control EventTitleChip(EventTitle entry, bool? expanded)
    {
        var chip = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Stop,
            TooltipText = entry.Message,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd,
            CustomMinimumSize = EventTitleMinSize,
        };
        chip.AddThemeStyleboxOverride("panel", UiTheme.Chip());
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        chip.AddChild(row);

        var label = UiTheme.Text(entry.Title, EventTitleFontSize, UiTheme.TextHi);
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(label);

        if (expanded is { } open)
        {
            var toggle = new Button
            {
                Text = open ? EventTitlesCollapseGlyph : EventTitlesExpandGlyph,
                FocusMode = Control.FocusModeEnum.None,
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
                TooltipText = open ? "Show one event notice" : "Show every event notice",
            };
            var bare = new StyleBoxEmpty();
            foreach (string state in new[] { "normal", "hover", "pressed", "focus" })
                toggle.AddThemeStyleboxOverride(state, bare);
            toggle.AddThemeFontSizeOverride("font_size", EventTitleFontSize);
            toggle.AddThemeColorOverride("font_color", UiTheme.Gold);
            toggle.AddThemeColorOverride("font_hover_color", UiTheme.GoldBright);
            toggle.Pressed += ToggleEventTitles;
            row.AddChild(toggle);
        }
        return chip;
    }

    private void ToggleEventTitles()
    {
        Net.I.EventTitles.Expanded = !Net.I.EventTitles.Expanded;
        RefreshEventTitles();
    }
}
