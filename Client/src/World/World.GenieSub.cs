using System;
using System.Collections.Generic;
using Godot;
using LibreKO.Domain;

namespace LibreKO;

public partial class World
{
    private const string GenieSubId = "genie_sub";
    private const int GenieSubRows = 3;
    private const int GenieSubLastRow = GenieSubRows - 1;
    private const int GeniePointsPerSpirit = 120;
    private const double GenieToggleCooldown = 10.0;
    private const float GenieSubWidth = 189f;
    private const int GenieWaitText = 31007;
    private const int GenieStartText = 31008;
    private const int GenieStopText = 31009;
    private const int GenieTitleText = 31016;
    private const float GenieSubMargin = 12f;

    private CanvasLayer _genieSubLayer = null!;
    private PanelContainer? _genieSub;
    private Button _genieSubToggle = null!;
    private ProgressBar _genieSubBar = null!;
    private Label _genieSubPoints = null!;
    private readonly List<(CheckBox On, Label Name, Button? Action)> _genieSubRows = new();
    private int _geniePoints;
    private double _genieToggledAt = double.NegativeInfinity;

    private void GenieSubInit()
    {
        _genieSubLayer = new CanvasLayer { Layer = _genieLayer.Layer };
        AddChild(_genieSubLayer);
        PluginHudSeam(_genieSubLayer, LibreKO.Plugins.HudPart.GenieSub);
        _genieSub = new PanelContainer
        {
            CustomMinimumSize = new Vector2(GenieSubWidth, 0),
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        _genieSub.AddThemeStyleboxOverride("panel", UiTheme.Panel(6, true));
        _genieSubLayer.AddChild(_genieSub);

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 3);
        _genieSub.AddChild(col);

        var head = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        head.AddThemeConstantOverride("separation", 4);
        col.AddChild(head);
        var title = UiTheme.Text(ItemData.Text(GenieTitleText, "Knight Genie"), 12, UiTheme.GoldBright);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        title.VerticalAlignment = VerticalAlignment.Center;
        title.MouseFilter = Control.MouseFilterEnum.Ignore;
        head.AddChild(title);
        _genieSubToggle = new Button { FocusMode = Control.FocusModeEnum.None };
        _genieSubToggle.AddThemeFontSizeOverride("font_size", 11);
        _genieSubToggle.Pressed += ToggleGenieFromSub;
        head.AddChild(_genieSubToggle);
        var open = UiTheme.IconButton("≡", "Genie settings");
        open.Pressed += ToggleGenie;
        head.AddChild(open);

        var bar = new Control { CustomMinimumSize = new Vector2(0, 12) };
        col.AddChild(bar);
        _genieSubBar = new ProgressBar { ShowPercentage = false, MaxValue = GeniePointsPerSpirit, MouseFilter = Control.MouseFilterEnum.Ignore };
        _genieSubBar.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bar.AddChild(_genieSubBar);
        _genieSubPoints = UiTheme.Text("", 10, Colors.White, HorizontalAlignment.Center);
        _genieSubPoints.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _genieSubPoints.VerticalAlignment = VerticalAlignment.Center;
        _genieSubPoints.MouseFilter = Control.MouseFilterEnum.Ignore;
        bar.AddChild(_genieSubPoints);

        for (int i = 0; i < GenieSubRows; i++)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 4);
            col.AddChild(row);
            int index = i;
            var on = new CheckBox { FocusMode = Control.FocusModeEnum.None, TooltipText = "Attack this monster" };
            on.Toggled += pressed => ToggleGenieSubRow(index, pressed);
            row.AddChild(on);
            var name = UiTheme.Text("", 11, UiTheme.TextHi);
            name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            name.ClipText = true;
            name.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(name);
            Button? action = null;
            if (index < GenieSubLastRow)
            {
                action = new Button { FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(22, 0) };
                action.AddThemeFontSizeOverride("font_size", 11);
                action.Pressed += () => GenieSubRowAction(index);
                row.AddChild(action);
            }
            _genieSubRows.Add((on, name, action));
        }

        HudLayout.Attach(_genieSub, GenieSubId, head, GenieSubDefaultPosition);
        RefreshGenieSub();
    }

    private Vector2 GenieSubDefaultPosition()
    {
        var viewport = _genieSub!.GetViewportRect().Size;
        return new Vector2(viewport.X - GenieSubWidth - GenieSubMargin, viewport.Y * 0.5f);
    }

    private void ToggleGenieFromSub()
    {
        double now = Now();
        if (now - _genieToggledAt < GenieToggleCooldown)
        {
            int left = (int)Math.Ceiling(GenieToggleCooldown - (now - _genieToggledAt));
            CombatNotice(ItemData.Text(GenieWaitText, "Knight Genie will be available after %d seconds.").Replace("%d", left.ToString()));
            return;
        }
        _genieToggledAt = now;
        ToggleGenieRun();
    }

    private void ToggleGenieSubRow(int index, bool on)
    {
        var targets = GenieTargetsInOrder();
        if (index == GenieSubLastRow)
        {
            if (on && targets.Count == GenieSubLastRow) AddGenieTarget();
            else if (!on && index < targets.Count) RemoveGenieTarget(targets[index]);
            RefreshGenieSub();
            return;
        }
        if (index < targets.Count) SetGenieTargetOn(targets[index], on);
    }

    private void GenieSubRowAction(int index)
    {
        var targets = GenieTargetsInOrder();
        if (index < targets.Count) RemoveGenieTarget(targets[index]);
        else AddGenieTarget();
        RefreshGenieSub();
    }

    private void RefreshGenieSub()
    {
        if (_genieSub == null || !IsInstanceValid(_genieSub)) return;
        PluginNotifyGenie();
        _genieSub.Visible = _geniePoints > 0 || Net.I.GenieRunning;
        bool running = Net.I.GenieRunning;
        _genieSubToggle.Text = running ? "Stop" : "Start";
        _genieSubToggle.TooltipText = ItemData.Text(running ? GenieStopText : GenieStartText, running ? "Disable Knight Genie" : "Start Knight Genie");
        _genieSubBar.MaxValue = Math.Max(GeniePointsPerSpirit, _geniePoints);
        _genieSubBar.Value = _geniePoints;
        _genieSubPoints.Text = _geniePoints.ToString();
        var targets = GenieTargetsInOrder();
        for (int i = 0; i < _genieSubRows.Count; i++)
        {
            var (on, name, action) = _genieSubRows[i];
            bool filled = i < targets.Count;
            name.Text = filled ? targets[i] : "";
            if (action == null)
            {
                on.Disabled = targets.Count < GenieSubLastRow;
                on.SetPressedNoSignal(filled);
                on.TooltipText = filled ? "Return" : "Add the selected monster";
                continue;
            }
            on.Disabled = !filled;
            on.SetPressedNoSignal(filled && !_genieMonstersOff.Contains(targets[i]));
            action.Text = filled ? "−" : "+";
            action.TooltipText = filled ? "Return" : "Add the selected monster";
        }
    }
}
