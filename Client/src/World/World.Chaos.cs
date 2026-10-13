using System.Collections.Generic;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const int ChaosLadderFontSize = 13;
    private const int ChaosTeamWidth = 52;
    private const int ChaosRankWidth = 44;
    private const int ChaosNameWidth = 260;
    private const int ChaosCountWidth = 60;
    private const int ChaosRowHeight = 25;
    private const string ChaosWinBadge = "Win";
    private const string ChaosLoseBadge = "Lose";
    private static readonly Color ChaosOwnRowColour = new(1f, 0.85f, 0.4f, 0.16f);

    private sealed class ChaosLadderRow
    {
        public Control Root = null!;
        public ColorRect Own = null!;
        public Label Team = null!, Rank = null!, Name = null!, Kills = null!, Deaths = null!;
    }

    private CanvasLayer? _chaosLayer;
    private HudWindow? _chaosLadder;
    private readonly ChaosLadderRow[] _chaosRows = new ChaosLadderRow[ChaosBattle.RowsShown];
    private Label? _chaosExp, _chaosPremiumExp;
    private ChaosRanking? _chaosRanking;
    private double _chaosRankingAt = double.NegativeInfinity;
    private bool _chaosLadderShown, _chaosFinal, _chaosFinalPending;

    private void ChaosInit()
    {
        Net.I.ChaosStartedEvent += OnChaosStarted;
        Net.I.ChaosFinishEvent += OnChaosFinish;
        Net.I.ChaosRankEvent += OnChaosRanking;
        if (_zone == ChaosBattle.Zone && !Net.I.ChaosBannerPrinted)
        {
            Net.I.ChaosBannerPrinted = true;
            PrintChaosBanner();
        }
    }

    private void ChaosDispose()
    {
        Net.I.ChaosStartedEvent -= OnChaosStarted;
        Net.I.ChaosFinishEvent -= OnChaosFinish;
        Net.I.ChaosRankEvent -= OnChaosRanking;
    }

    private void PrintChaosBanner()
    {
        CombatNotice(ChaosBattle.BannerRule, ChaosBattle.BannerColour);
        foreach (int text in ChaosBattle.BannerTexts)
            CombatNotice(ItemData.Text(text, ""), ChaosBattle.BannerColour);
        CombatNotice(ChaosBattle.BannerRule, ChaosBattle.BannerColour);
    }

    private void OnChaosStarted()
    {
        ShowSkillMsgOut(ItemData.Text(ChaosBattle.StartedText, "The battle has begun."));
        if (_chaosGuide != null && IsInstanceValid(_chaosGuide)) _chaosGuide.Visible = false;
    }

    private void OnChaosFinish(uint seconds)
    {
        ShowSkillMsgOut(ItemData.Text(ChaosBattle.OverText, "The battle is over."));
    }

    private void ChaosTick()
    {
        if (_zone != ChaosBattle.Zone) return;
        if (Net.I.ChaosOver && Now() >= _msgOutUntil)
        {
            Net.I.ConsumeChaosOver();
            _chaosFinalPending = true;
            Net.I.SendRankRequest(Net.RankTypeChaosDungeon);
        }
        if (_chaosFinal) return;

        bool typing = GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit or SpinBox;
        if (typing || !Held(KeyAction.PkLadder))
        {
            if (_chaosLadderShown) HideChaosLadder();
            return;
        }
        if (_chaosLadderShown || Net.I.ChaosFinished) return;
        ShowChaosLadder(final: false);
        if (Now() - _chaosRankingAt >= ChaosBattle.RankingThrottleSeconds)
        {
            _chaosRankingAt = Now();
            Net.I.SendRankRequest(Net.RankTypeChaosDungeon);
        }
    }

    private void OnChaosRanking(ChaosRanking ranking)
    {
        _chaosRanking = ranking;
        if (_chaosFinalPending)
        {
            _chaosFinalPending = false;
            ShowChaosLadder(final: true);
            return;
        }
        if (_chaosLadderShown) RenderChaosLadder();
    }

    private void ShowChaosLadder(bool final)
    {
        EnsureChaosLadder();
        _chaosFinal = final;
        _chaosLadder!.SetCloseVisible(final);
        _chaosLadder.MouseFilter = final ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore;
        RenderChaosLadder();
        ShowOnTop(_chaosLadder);
        _chaosLadderShown = true;
    }

    private void HideChaosLadder()
    {
        _chaosLadderShown = false;
        _chaosFinal = false;
        if (_chaosLadder != null && IsInstanceValid(_chaosLadder)) _chaosLadder.Visible = false;
    }

    private void EnsureChaosLadder()
    {
        if (_chaosLadder != null && IsInstanceValid(_chaosLadder)) return;
        _chaosLayer = new CanvasLayer { Layer = 75 };
        AddChild(_chaosLayer);
        _chaosLadder = new HudWindow("chaos_ladder", "Combat status") { Visible = false };
        _chaosLadder.Closed += HideChaosLadder;
        _chaosLayer.AddChild(_chaosLadder);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 0);
        _chaosLadder.Body.AddChild(column);

        var header = ChaosLine();
        header.AddChild(ChaosCell("Team", ChaosTeamWidth, UiTheme.TextLo, HorizontalAlignment.Center));
        header.AddChild(ChaosCell("Rank", ChaosRankWidth, UiTheme.TextLo, HorizontalAlignment.Center));
        header.AddChild(ChaosCell("Character name", ChaosNameWidth, UiTheme.TextLo, HorizontalAlignment.Center));
        header.AddChild(ChaosCell("Kill", ChaosCountWidth, UiTheme.TextLo, HorizontalAlignment.Center));
        header.AddChild(ChaosCell("Death", ChaosCountWidth, UiTheme.TextLo, HorizontalAlignment.Center));
        column.AddChild(header);

        for (int i = 0; i < _chaosRows.Length; i++)
            _chaosRows[i] = AddChaosRow(column);

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 24);
        footer.AddChild(UiTheme.Text("Experience acquired:", ChaosLadderFontSize, UiTheme.TextLo));
        _chaosExp = UiTheme.Text("0", ChaosLadderFontSize, UiTheme.Gold);
        footer.AddChild(_chaosExp);
        footer.AddChild(UiTheme.Text("Premium experience acquired:", ChaosLadderFontSize, UiTheme.TextLo));
        _chaosPremiumExp = UiTheme.Text("0", ChaosLadderFontSize, UiTheme.Gold);
        footer.AddChild(_chaosPremiumExp);
        column.AddChild(new HSeparator());
        column.AddChild(footer);
    }

    private static HBoxContainer ChaosLine()
    {
        var line = new HBoxContainer { CustomMinimumSize = new Vector2(0, ChaosRowHeight), MouseFilter = Control.MouseFilterEnum.Ignore };
        line.AddThemeConstantOverride("separation", 6);
        return line;
    }

    private static Label ChaosCell(string text, int width, Color colour, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var label = UiTheme.Text(text, ChaosLadderFontSize, colour, align);
        label.CustomMinimumSize = new Vector2(width, 0);
        label.ClipText = true;
        return label;
    }

    private static ChaosLadderRow AddChaosRow(VBoxContainer column)
    {
        var holder = new Control { CustomMinimumSize = new Vector2(0, ChaosRowHeight), MouseFilter = Control.MouseFilterEnum.Ignore };
        var own = new ColorRect { Color = ChaosOwnRowColour, Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        own.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        holder.AddChild(own);
        var line = ChaosLine();
        line.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        holder.AddChild(line);
        var row = new ChaosLadderRow
        {
            Root = holder,
            Own = own,
            Team = ChaosCell("", ChaosTeamWidth, UiTheme.Gold, HorizontalAlignment.Center),
            Rank = ChaosCell("", ChaosRankWidth, UiTheme.TextHi, HorizontalAlignment.Center),
            Name = ChaosCell("", ChaosNameWidth, UiTheme.TextHi, HorizontalAlignment.Center),
            Kills = ChaosCell("", ChaosCountWidth, UiTheme.TextHi, HorizontalAlignment.Center),
            Deaths = ChaosCell("", ChaosCountWidth, UiTheme.TextHi, HorizontalAlignment.Center),
        };
        line.AddChild(row.Team);
        line.AddChild(row.Rank);
        line.AddChild(row.Name);
        line.AddChild(row.Kills);
        line.AddChild(row.Deaths);
        column.AddChild(holder);
        return row;
    }

    private void RenderChaosLadder()
    {
        if (_chaosLadder == null) return;
        var ranking = _chaosRanking;
        var lines = ranking == null ? new List<ChaosLadderLine>() : ChaosBattle.Ladder(ranking.Rows, Net.I.LastEnter.Name ?? "");
        bool realNames = _chaosFinal || Net.I.IsGm;
        int nation = Net.I.LastEnter.Nation;
        for (int i = 0; i < _chaosRows.Length; i++)
        {
            var row = _chaosRows[i];
            if (i >= lines.Count)
            {
                row.Root.Visible = false;
                continue;
            }
            var line = lines[i];
            row.Root.Visible = true;
            row.Own.Visible = line.Own;
            row.Team.Text = line.Winner ? ChaosWinBadge : line.Loser ? ChaosLoseBadge : "";
            row.Team.AddThemeColorOverride("font_color", line.Winner ? UiTheme.GoldBright : UiTheme.TextLo);
            row.Rank.Text = line.Rank.ToString();
            row.Rank.AddThemeColorOverride("font_color", line.Winner ? ChaosBattle.WinnerRank : ChaosBattle.OtherRank);
            row.Name.Text = realNames ? line.Name : ItemData.Text(ChaosBattle.MaskedNameText(line.Own, nation), line.Name);
            row.Name.AddThemeColorOverride("font_color", line.Winner ? ChaosBattle.WinnerName : ChaosBattle.OtherName);
            row.Kills.Text = line.Kills.ToString();
            row.Deaths.Text = line.Deaths.ToString();
        }
        _chaosExp!.Text = (ranking?.Exp ?? 0).ToString();
        _chaosPremiumExp!.Text = (ranking?.PremiumExp ?? 0).ToString();
        _chaosLadder.ResetSize();
    }

    private void ShowChaosPlate()
    {
        string title = ChaosBattle.EventTitle;
        if (Net.I.ChaosFinished)
        {
            int left = Net.I.ChaosFinishSecondsLeft;
            ShowTimerPlate(title, ItemData.Text(ChaosBattle.PlateOverText, "Chaos Battle is over."),
                NestDungeon.TerminationLine(left, completed: true));
            return;
        }
        int clock = Net.I.NestSecondsLeft;
        if (clock <= 0)
        {
            if (_nestTimerPanel is { Visible: true }) _nestTimerPanel.Visible = false;
            return;
        }
        ShowTimerPlate(title, null, NestDungeon.TerminationLine(clock, completed: false));
    }
}
