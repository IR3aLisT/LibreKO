using System;
using System.Collections.Generic;
using Godot;
using LibreKO.Domain;

namespace LibreKO;

public partial class World
{
    private const double EventsRefreshSeconds = 300;
    private const double EventsTickSeconds = 1;
    private static readonly TimeSpan EventsAlertLead = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan EventsUpcomingWindow = TimeSpan.FromHours(24);
    private const float EventsPulseSpeed = 4f;
    private const float EventsPulseLow = 0.35f;
    private const float EventsRowGap = 4f;
    private const float EventsNameWidth = 190f;
    private const float EventsBandWidth = 74f;
    private const float EventsClockWidth = 92f;
    private const float EventsStateWidth = 64f;
    private const int EventsTopLevel = 83;

    private Button _eventsIcon = null!;
    private TextureRect _eventsImage = null!;
    private CanvasLayer _eventsLayer = null!;
    private HudWindow _eventsPanel = null!;
    private VBoxContainer _eventsNowRows = null!, _eventsUpcomingRows = null!;
    private Label _eventsNowEmpty = null!, _eventsUpcomingEmpty = null!;
    private readonly List<(EventNow Entry, Label Clock)> _eventsNowClocks = new();
    private EventSchedule? _eventSchedule;
    private double _eventsRefreshIn, _eventsTickIn;
    private bool _eventsAlert;

    private void EventsInit()
    {
        _eventsIcon = TopIconButton(_topIconLayer, "system/clock", "Events", ToggleEvents, out _eventsImage);
        _eventsIcon.Resized += PlaceTopIcons;
        BuildEventsWindow();
        Net.I.EventScheduleEvent += OnEventSchedule;
        _eventsRefreshIn = 0;
    }

    private void EventsDispose()
    {
        Net.I.EventScheduleEvent -= OnEventSchedule;
    }

    private void BuildEventsWindow()
    {
        _eventsLayer = new CanvasLayer { Layer = 62 };
        AddChild(_eventsLayer);
        _eventsPanel = new HudWindow("events", "Events") { Visible = false };
        _eventsPanel.Closed += () => _eventsPanel.Visible = false;
        _eventsLayer.AddChild(_eventsPanel);

        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 6);
        _eventsPanel.Body.AddChild(body);

        body.AddChild(UiTheme.SectionTitle("Now"));
        _eventsNowRows = new VBoxContainer();
        _eventsNowRows.AddThemeConstantOverride("separation", (int)EventsRowGap);
        body.AddChild(_eventsNowRows);
        _eventsNowEmpty = UiTheme.Text("Nothing is running right now.", 12, UiTheme.TextLo);
        body.AddChild(_eventsNowEmpty);

        body.AddChild(new HSeparator());
        body.AddChild(UiTheme.SectionTitle("Next 24 hours"));
        _eventsUpcomingRows = new VBoxContainer();
        _eventsUpcomingRows.AddThemeConstantOverride("separation", (int)EventsRowGap);
        body.AddChild(_eventsUpcomingRows);
        _eventsUpcomingEmpty = UiTheme.Text("Nothing scheduled in the next 24 hours.", 12, UiTheme.TextLo);
        body.AddChild(_eventsUpcomingEmpty);
    }

    private void ToggleEvents()
    {
        _eventsPanel.Visible = !_eventsPanel.Visible;
        if (!_eventsPanel.Visible) return;
        RefreshEventsWindow();
        Net.I.SendEventScheduleRequest();
    }

    private void OnEventSchedule(EventSchedule schedule)
    {
        _eventSchedule = schedule;
        RefreshEventsWindow();
        UpdateEventsAlert();
    }

    private void EventsTick(double delta)
    {
        if (_eventsPanel == null || !IsInstanceValid(_eventsPanel)) return;
        _eventsRefreshIn -= delta;
        if (_eventsRefreshIn <= 0)
        {
            _eventsRefreshIn = EventsRefreshSeconds;
            Net.I.SendEventScheduleRequest();
        }

        _eventsTickIn -= delta;
        if (_eventsTickIn <= 0)
        {
            _eventsTickIn = EventsTickSeconds;
            UpdateEventsAlert();
            if (_eventsPanel.Visible) TickEventClocks();
        }

        if (!IsInstanceValid(_eventsImage)) return;
        float alpha = _eventsAlert
            ? Mathf.Lerp(EventsPulseLow, 1f, 0.5f + 0.5f * Mathf.Sin((float)Now() * EventsPulseSpeed))
            : 1f;
        var tint = TopIconColor(_eventsAlert);
        _eventsImage.SelfModulate = new Color(tint, alpha);
    }

    private void UpdateEventsAlert() =>
        _eventsAlert = _eventSchedule?.AlertDue(Sheet.Level, DateTime.UtcNow, EventsAlertLead) == true;

    private void RefreshEventsWindow()
    {
        if (!IsInstanceValid(_eventsPanel)) return;
        foreach (Node child in _eventsNowRows.GetChildren()) child.QueueFree();
        foreach (Node child in _eventsUpcomingRows.GetChildren()) child.QueueFree();
        _eventsNowClocks.Clear();

        var schedule = _eventSchedule;
        var utcNow = DateTime.UtcNow;
        int nowShown = 0;
        if (schedule != null)
            foreach (var entry in schedule.Now)
            {
                var clock = EventsCell("", EventsClockWidth, UiTheme.GoldBright, HorizontalAlignment.Right);
                _eventsNowRows.AddChild(EventsRow(entry.Name, entry.MinLevel, entry.MaxLevel,
                    PhaseText(entry.Phase), clock, NowAction(entry)));
                _eventsNowClocks.Add((entry, clock));
                nowShown++;
            }
        _eventsNowEmpty.Visible = nowShown == 0;
        _eventsNowEmpty.Text = schedule == null ? "Loading the schedule..." : "Nothing is running right now.";

        int upcomingShown = 0;
        var localToday = DateTime.Now.Date;
        if (schedule != null)
            foreach (var upcoming in schedule.Upcoming(utcNow, EventsUpcomingWindow))
            {
                var local = TimeZoneInfo.ConvertTimeFromUtc(upcoming.StartUtc, TimeZoneInfo.Local);
                string at = local.Date == localToday ? local.ToString("HH:mm") : local.ToString("ddd HH:mm");
                var time = EventsCell(at, EventsClockWidth, UiTheme.TextHi, HorizontalAlignment.Right);
                _eventsUpcomingRows.AddChild(EventsRow(upcoming.Slot.Name, upcoming.Slot.MinLevel,
                    upcoming.Slot.MaxLevel, "", time, null));
                upcomingShown++;
            }
        _eventsUpcomingEmpty.Visible = schedule != null && upcomingShown == 0;
        TickEventClocks();
        Callable.From(() => { if (IsInstanceValid(_eventsPanel)) _eventsPanel.ResetSize(); }).CallDeferred();
    }

    private void TickEventClocks()
    {
        if (_eventSchedule is not { } schedule) return;
        var utcNow = DateTime.UtcNow;
        foreach (var (entry, clock) in _eventsNowClocks)
        {
            if (!IsInstanceValid(clock)) continue;
            var left = TimeSpan.FromSeconds(schedule.SecondsLeft(entry, utcNow));
            string time = left.TotalHours >= 1 ? left.ToString(@"h\:mm\:ss") : left.ToString(@"mm\:ss");
            clock.Text = $"{time} left";
        }
    }

    private Control EventsRow(string name, int minLevel, int maxLevel, string state, Label clock, Button? action)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        bool fits = EventSchedule.Fits(Sheet.Level, minLevel, maxLevel);
        row.AddChild(EventsCell(name, EventsNameWidth, fits ? UiTheme.TextHi : UiTheme.TextLo, HorizontalAlignment.Left));
        row.AddChild(EventsCell(LevelBand(minLevel, maxLevel), EventsBandWidth, fits ? UiTheme.TextLo : UiTheme.Bad,
            HorizontalAlignment.Left));
        row.AddChild(EventsCell(state, EventsStateWidth, UiTheme.Neutral, HorizontalAlignment.Left));
        row.AddChild(clock);
        if (action != null) row.AddChild(action);
        else row.AddChild(new Control { CustomMinimumSize = new Vector2(EventsActionWidth, 0f) });
        return row;
    }

    private const float EventsActionWidth = 64f;

    private static Label EventsCell(string text, float width, Color colour, HorizontalAlignment align)
    {
        var label = UiTheme.Text(text, 12, colour, align);
        if (width <= 0f) return label;
        label.CustomMinimumSize = new Vector2(width, 0f);
        label.ClipText = true;
        label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        label.TooltipText = text;
        label.MouseFilter = Control.MouseFilterEnum.Pass;
        return label;
    }

    private static string LevelBand(int minLevel, int maxLevel) =>
        minLevel <= 0 && maxLevel <= 0 ? "All levels" : $"Lv {Math.Max(1, minLevel)}-{(maxLevel > 0 ? maxLevel : EventsTopLevel)}";

    private static string PhaseText(EventPhase phase) => phase == EventPhase.SignUp ? "Sign-up" : "Running";

    private Button? NowAction(EventNow entry)
    {
        Action? act = entry switch
        {
            { Kind: EventKind.Temple, Phase: EventPhase.SignUp } => OfferBifrostJoin,
            { Kind: EventKind.Lottery } => ToggleLottery,
            _ => null,
        };
        if (act == null) return null;
        var button = UiTheme.SmallButton(entry.Kind == EventKind.Lottery ? "Open" : "Join", entry.Name);
        button.CustomMinimumSize = new Vector2(EventsActionWidth, button.CustomMinimumSize.Y);
        button.Pressed += () => act();
        return button;
    }
}
