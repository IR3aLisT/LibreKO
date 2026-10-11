using System;
using System.Collections.Generic;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const string WarResultId = "war_result";
    private const string WarRankingId = "war_ranking";
    private const string BdwTitle = "Border Defense War";
    private const string JuraidTitle = "Juraid Mountain";
    private const int WarResultWidth = 320;
    private const int WarRankingWidth = 520;
    private const int WarRankingNameWidth = 110;
    private const int WarRankingPlaceWidth = 22;
    private static readonly Color WarBonusColour = new("ff6a6a");

    private HudWindow? _warResult;
    private Label _warOutcome = null!;
    private Label _warResultScores = null!;
    private Label _warResultHome = null!;
    private HudWindow? _warRanking;
    private VBoxContainer _warRankKarus = null!;
    private VBoxContainer _warRankElmorad = null!;
    private RichTextLabel _warRankExp = null!;

    private string WarTitle => _zone == JuraidZone ? JuraidTitle : BdwTitle;

    private static string ClockText(int seconds) => $"{seconds / 60}:{seconds % 60:00}";

    private void ShowWarResult()
    {
        if (_warResult == null || !IsInstanceValid(_warResult)) BuildWarResult();
        _warResult!.Visible = true;
        RefreshWarResult(DateTime.UtcNow);
        if (_bdwTick.IsStopped()) _bdwTick.Start();
    }

    private void BuildWarResult()
    {
        _warResult = new HudWindow(WarResultId, WarTitle, bodyMinWidth: WarResultWidth, persistLayout: false) { Visible = false };
        _warResult.Closed += () => _warResult.Visible = false;
        _bdwHudLayer.AddChild(_warResult);

        var col = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        col.AddThemeConstantOverride("separation", 8);
        _warResult.Body.AddChild(col);
        _warOutcome = UiTheme.Text("", 26, UiTheme.TextHi, HorizontalAlignment.Center);
        _warOutcome.AddThemeConstantOverride("outline_size", 3);
        col.AddChild(_warOutcome);
        _warResultScores = UiTheme.Text("", 15, UiTheme.TextHi, HorizontalAlignment.Center);
        col.AddChild(_warResultScores);
        col.AddChild(UiTheme.Rule());
        _warResultHome = UiTheme.Text("", 13, UiTheme.GoldBright, HorizontalAlignment.Center);
        col.AddChild(_warResultHome);
    }

    private void RefreshWarResult(DateTime now)
    {
        if (_warResult == null || !IsInstanceValid(_warResult) || !_warResult.Visible) return;
        var war = Net.I.BorderWar;
        if (war.Winner is not { } winner)
        {
            _warResult.Visible = false;
            return;
        }
        var outcome = BorderWarState.OutcomeFor(winner, Net.I.LastEnter.Nation);
        _warOutcome.Text = outcome.ToString();
        _warOutcome.AddThemeColorOverride("font_color", outcome switch
        {
            WarOutcome.Victory => UiTheme.Good,
            WarOutcome.Defeat => UiTheme.Bad,
            _ => UiTheme.TextHi,
        });
        _warResultScores.Text = $"Karus  {war.KarusScore}   :   {war.ElmoradScore}  El Morad";
        int home = war.HomeSecondsLeft(now);
        _warResultHome.Text = home > 0 ? $"Rewards and return home in {ClockText(home)}" : "Returning home";
    }

    private void ToggleWarRanking()
    {
        if (_warRanking != null && IsInstanceValid(_warRanking) && _warRanking.Visible)
        {
            _warRanking.Visible = false;
            return;
        }
        if (_warRanking == null || !IsInstanceValid(_warRanking)) BuildWarRanking();
        _warRanking!.Visible = true;
        Net.I.SendRankRequest(Net.RankTypeBorderWar);
    }

    private void BuildWarRanking()
    {
        _warRanking = new HudWindow(WarRankingId, "Ranking", bodyMinWidth: WarRankingWidth) { Visible = false };
        _warRanking.Closed += () => _warRanking.Visible = false;
        _bdwHudLayer.AddChild(_warRanking);

        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 8);
        _warRanking.Body.AddChild(columns);
        _warRankKarus = WarRankColumn(columns, "Karus", WarScoreStrip.KarusColour);
        _warRankElmorad = WarRankColumn(columns, "El Morad", WarScoreStrip.ElmoradColour);

        var foot = new HBoxContainer();
        foot.AddThemeConstantOverride("separation", 8);
        _warRanking.Body.AddChild(foot);
        _warRankExp = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.Off,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _warRankExp.AddThemeFontSizeOverride("normal_font_size", 13);
        foot.AddChild(_warRankExp);
        var refresh = new Button { Text = "Refresh", FocusMode = Control.FocusModeEnum.None };
        refresh.Pressed += () => Net.I.SendRankRequest(Net.RankTypeBorderWar);
        foot.AddChild(refresh);
    }

    private static VBoxContainer WarRankColumn(HBoxContainer parent, string nation, Color colour)
    {
        var section = UiTheme.Section();
        section.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        parent.AddChild(section);
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 3);
        section.AddChild(col);
        col.AddChild(UiTheme.Text(nation, 14, colour, HorizontalAlignment.Center));
        col.AddChild(UiTheme.Rule());
        var rows = new VBoxContainer();
        rows.AddThemeConstantOverride("separation", 2);
        col.AddChild(rows);
        return rows;
    }

    private void OnBorderWarRanking(BorderWarRanking ranking)
    {
        if (_warRanking == null || !IsInstanceValid(_warRanking)) return;
        FillWarRankColumn(_warRankKarus, ranking.Karus);
        FillWarRankColumn(_warRankElmorad, ranking.Elmorad);
        string text = $"Victory reward:  {ranking.VictoryExp:N0} EXP";
        if (ranking.BonusExp > 0) text += $" [color=#{WarBonusColour.ToHtml(false)}](+{ranking.BonusExp:N0})[/color]";
        _warRankExp.Text = text;
    }

    private static void FillWarRankColumn(VBoxContainer rows, IReadOnlyList<BorderWarRankRow> entries)
    {
        foreach (var child in rows.GetChildren()) child.QueueFree();
        if (entries.Count == 0)
        {
            rows.AddChild(UiTheme.Text("No one yet", 12, UiTheme.TextLo, HorizontalAlignment.Center));
            return;
        }
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 6);
            rows.AddChild(row);
            var place = UiTheme.Text($"{i + 1}.", 12, UiTheme.GoldBright);
            place.CustomMinimumSize = new Vector2(WarRankingPlaceWidth, 0);
            row.AddChild(place);
            var name = UiTheme.Text(entry.Name, 12, UiTheme.TextHi);
            name.CustomMinimumSize = new Vector2(WarRankingNameWidth, 0);
            name.ClipText = true;
            row.AddChild(name);
            var clan = UiTheme.Text(entry.ClanName, 11, UiTheme.TextLo);
            clan.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            clan.ClipText = true;
            row.AddChild(clan);
            row.AddChild(UiTheme.Text($"{entry.Points}", 12, UiTheme.Gold, HorizontalAlignment.Right));
        }
    }
}
