using System;
using Godot;
using LibreKO.Domain;

namespace LibreKO;

public partial class World
{
    private const float ChaosGuideColumnWidth = 300f;
    private const int ChaosGuideTextSize = 13;
    private const string ChaosGuideHeading = "The Great Disorder — a doomed temple. Trust no one.";
    private const string ChaosGuideBattle = "Kill everyone and survive at all costs!";
    private const string ChaosGuideCubeIntro = "Item gained when coming in contact with the Chaos Cube!";
    private const string ChaosGuideRewardIntro = "Blue Treasure Box, Green Treasure Box, Red Treasure Box, Oracle Voucher, Chaos Emblem";

    private static readonly (string Rank, string Gain)[] ChaosCubeRows =
    {
        ("Top 20%", "Light Pit"),
        ("Top 21% ~ 50%", "Drain Restore"),
        ("Bottom 51% ~ 100%", "Killing Blade (kills with one strike)"),
    };

    private static readonly (string Rank, string Gain)[] ChaosRewardRows =
    {
        ("Top 1% ~ 20%", "Chaos Emblem, Blue Treasure Box, Oracle Voucher"),
        ("21% ~ 40%", "Chaos Emblem, Green Treasure Box"),
        ("41% ~ 80%", "Chaos Emblem, Red Treasure Box"),
        ("81% ~ 100%", "Chaos Emblem"),
    };

    private HudWindow? _chaosGuide;
    private CanvasLayer? _chaosGuideLayer;

    private void ShowChaosGuideOnEntry()
    {
        if (_zone != ChaosZone || TimedItems.HiddenToday(Config.ChaosGuideHiddenDay, DateTime.Now)) return;
        QueueEntryPopup(EntryPopup.ZoneGuide, () =>
        {
            _chaosGuide ??= BuildChaosGuide();
            ShowOnTop(_chaosGuide);
            return _chaosGuide;
        });
    }

    private HudWindow BuildChaosGuide()
    {
        _chaosGuideLayer = new CanvasLayer { Layer = 74 };
        AddChild(_chaosGuideLayer);
        var window = new HudWindow("chaos_caption", "Chaos") { Visible = false };
        window.Closed += () => window.Visible = false;
        _chaosGuideLayer.AddChild(window);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 10);
        window.Body.AddChild(column);

        var heading = UiTheme.Text(ChaosGuideHeading, 15, UiTheme.GoldBright, HorizontalAlignment.Right);
        column.AddChild(heading);

        column.AddChild(UiTheme.SectionTitle("Battle Method"));
        column.AddChild(UiTheme.Text(ChaosGuideBattle, 16, UiTheme.GoldVivid, HorizontalAlignment.Center));

        var halves = new HBoxContainer();
        halves.AddThemeConstantOverride("separation", 16);
        column.AddChild(halves);
        halves.AddChild(ChaosGuideHalf("Acquire the Chaos Cube!", ChaosGuideCubeIntro, ChaosCubeRows));
        halves.AddChild(ChaosGuideHalf("Reward", ChaosGuideRewardIntro, ChaosRewardRows));

        var footer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        footer.AddThemeConstantOverride("separation", 10);
        var today = UiTheme.FlatCheck("Do not open this window today", false);
        today.Toggled += on =>
        {
            Config.SetChaosGuideHiddenDay(on ? TimedItems.DayKey(DateTime.Now) : 0);
            if (on) window.Visible = false;
        };
        footer.AddChild(today);
        footer.AddChild(LookActionButton("Close", () => window.Visible = false));
        column.AddChild(footer);
        return window;
    }

    private static Control ChaosGuideHalf(string title, string intro, (string Rank, string Gain)[] rows)
    {
        var half = new VBoxContainer { CustomMinimumSize = new Vector2(ChaosGuideColumnWidth, 0f) };
        half.AddThemeConstantOverride("separation", 6);
        half.AddChild(UiTheme.SectionTitle(title));

        var lead = UiTheme.Text(intro, ChaosGuideTextSize, UiTheme.Gold);
        lead.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        lead.CustomMinimumSize = new Vector2(ChaosGuideColumnWidth, 0f);
        half.AddChild(lead);

        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 12);
        grid.AddThemeConstantOverride("v_separation", 4);
        foreach (var (rank, gain) in rows)
        {
            var label = UiTheme.Text(rank, ChaosGuideTextSize, UiTheme.TextHi);
            label.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
            grid.AddChild(label);
            var value = UiTheme.Text(gain, ChaosGuideTextSize, UiTheme.TextLo);
            value.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            value.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            grid.AddChild(value);
        }
        half.AddChild(grid);
        return half;
    }
}
