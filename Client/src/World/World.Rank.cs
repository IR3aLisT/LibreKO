using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const int LadderRankWidth = 34;
    private const int LadderMarkSize = 20;
    private const int LadderMarkWidth = 36;
    private const int LadderNameWidth = 120;
    private const int LadderClanWidth = 120;
    private const int LadderPointWidth = 120;
    private const int LadderFontSize = 12;
    private static readonly Color LadderBonusColour = new("ff6a6a");

    private sealed class LadderRow
    {
        public Label Rank = null!, Name = null!, Clan = null!, Points = null!, Bonus = null!;
        public TextureRect Mark = null!;
    }

    private CanvasLayer _rankLayer = null!;
    private HudWindow _rankPanel = null!;
    private readonly LadderRow[] _ladderKarus = new LadderRow[PkLadder.RowsShown];
    private readonly LadderRow[] _ladderElMorad = new LadderRow[PkLadder.RowsShown];
    private LadderRow _ladderOwnKarus = null!, _ladderOwnElMorad = null!;
    private bool _rankShown, _rankHeld, _ladderRequested;
    private double _ladderReceivedAt;
    private PkLadderReply? _ladder;

    private void RankInit()
    {
        BuildRankPanel();
        Net.I.RankEvent += OnRankData;
    }

    private void RankDispose()
    {
        Net.I.RankEvent -= OnRankData;
    }

    private void BuildRankPanel()
    {
        _rankLayer = new CanvasLayer { Layer = 75 };
        AddChild(_rankLayer);

        _rankPanel = new HudWindow("rank", ItemData.Text(PkLadder.TitleText, "Today's Rank")) { Visible = false };
        _rankPanel.Closed += CloseRank;
        _rankLayer.AddChild(_rankPanel);

        var halves = new HBoxContainer();
        halves.AddThemeConstantOverride("separation", 18);
        _rankPanel.Body.AddChild(halves);
        halves.AddChild(BuildLadderSide("Karus", _ladderKarus, out _ladderOwnKarus));
        halves.AddChild(BuildLadderSide("El Morad", _ladderElMorad, out _ladderOwnElMorad));

        var notice = UiTheme.Text(PkLadder.Notice, LadderFontSize, UiTheme.TextLo, HorizontalAlignment.Center);
        _rankPanel.Body.AddChild(notice);
    }

    private static Control BuildLadderSide(string nation, LadderRow[] rows, out LadderRow own)
    {
        var side = new VBoxContainer();
        side.AddThemeConstantOverride("separation", 3);
        side.AddChild(UiTheme.SectionTitle(nation));

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 6);
        header.AddChild(LadderCell("Rank", LadderRankWidth, UiTheme.TextLo, HorizontalAlignment.Center));
        header.AddChild(LadderCell("Mark", LadderMarkWidth, UiTheme.TextLo, HorizontalAlignment.Center));
        header.AddChild(LadderCell("Name", LadderNameWidth, UiTheme.TextLo));
        header.AddChild(LadderCell("Clan", LadderClanWidth, UiTheme.TextLo));
        header.AddChild(LadderCell("Point", LadderPointWidth, UiTheme.TextLo, HorizontalAlignment.Right));
        side.AddChild(header);

        for (int i = 0; i < rows.Length; i++)
            rows[i] = AddLadderRow(side);
        side.AddChild(new HSeparator());
        own = AddLadderRow(side);
        return side;
    }

    private static LadderRow AddLadderRow(VBoxContainer side)
    {
        var line = new HBoxContainer { CustomMinimumSize = new Vector2(0, LadderMarkSize + 2) };
        line.AddThemeConstantOverride("separation", 6);
        var row = new LadderRow
        {
            Rank = LadderCell("", LadderRankWidth, UiTheme.TextHi, HorizontalAlignment.Center),
            Mark = new TextureRect
            {
                CustomMinimumSize = new Vector2(LadderMarkWidth, LadderMarkSize),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            },
            Name = LadderCell("", LadderNameWidth, UiTheme.TextHi),
            Clan = LadderCell("", LadderClanWidth, UiTheme.TextLo),
        };
        var points = new HBoxContainer { CustomMinimumSize = new Vector2(LadderPointWidth, 0), Alignment = BoxContainer.AlignmentMode.End };
        points.AddThemeConstantOverride("separation", 4);
        row.Points = UiTheme.Text("", LadderFontSize, UiTheme.Gold);
        row.Bonus = UiTheme.Text("", LadderFontSize, LadderBonusColour);
        points.AddChild(row.Points);
        points.AddChild(row.Bonus);

        line.AddChild(row.Rank);
        line.AddChild(row.Mark);
        line.AddChild(row.Name);
        line.AddChild(row.Clan);
        line.AddChild(points);
        side.AddChild(line);
        return row;
    }

    private static Label LadderCell(string text, int width, Color color,
        HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var lbl = UiTheme.Text(text, LadderFontSize, color, align);
        lbl.CustomMinimumSize = new Vector2(width, 0);
        lbl.ClipText = true;
        return lbl;
    }

    private void PkLadderTick()
    {
        bool typing = GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit or SpinBox;
        if (!PkLadder.IsLadderZone(_zone) || typing || !Held(KeyAction.PkLadder))
        {
            if (_rankHeld) CloseRank();
            _rankHeld = false;
            return;
        }
        _rankHeld = true;
        if (_rankShown) return;
        if (PkLadder.IsFresh(_ladderReceivedAt, Now()))
        {
            ShowOnTop(_rankPanel);
            _rankShown = true;
            return;
        }
        if (_ladderRequested) return;
        _ladderRequested = true;
        Net.I.SendRankRequest(Net.RankTypePkZone);
    }

    public void CloseRank()
    {
        if (!_rankShown) return;
        _rankShown = false;
        _rankPanel.Visible = false;
    }

    private void OnRankData(PkLadderReply reply)
    {
        _ladder = reply;
        _ladderReceivedAt = Now();
        _ladderRequested = false;
        RenderLadder();
    }

    private void RenderLadder()
    {
        if (_ladder is not { } ladder) return;
        FillLadderSide(_ladderKarus, ladder.Karus);
        FillLadderSide(_ladderElMorad, ladder.ElMorad);

        var me = Net.I.LastEnter;
        bool karus = me.Nation == Nations.Karus;
        ClearLadderRow(karus ? _ladderOwnElMorad : _ladderOwnKarus);
        FillLadderRow(karus ? _ladderOwnKarus : _ladderOwnElMorad, PkLadder.Place(ladder.MyRank),
            RankSymbol.PlatePath(me.KnightsRank, me.PersonalRank), me.Name, me.ClanName, ladder.MyPoints, ladder.MyBonus);
    }

    private static void FillLadderSide(LadderRow[] rows, System.Collections.Generic.IReadOnlyList<RankEntry> entries)
    {
        for (int i = 0; i < rows.Length; i++)
        {
            if (i >= entries.Count) { ClearLadderRow(rows[i]); continue; }
            var e = entries[i];
            FillLadderRow(rows[i], PkLadder.Place(e.Place), RankSymbol.TexturePath(e.Symbol), e.Name, e.ClanName, e.Loyalty, e.Bonus);
        }
    }

    private static void FillLadderRow(LadderRow row, string place, string? mark, string name, string clan, int points, int bonus)
    {
        row.Rank.Text = place;
        row.Mark.Texture = mark != null ? GD.Load<Texture2D>(mark) : null;
        row.Name.Text = name;
        row.Clan.Text = clan;
        row.Points.Text = PkLadder.Points(points);
        row.Bonus.Text = PkLadder.Bonus(bonus);
        row.Bonus.Visible = row.Bonus.Text.Length > 0;
    }

    private static void ClearLadderRow(LadderRow row)
    {
        row.Rank.Text = row.Name.Text = row.Clan.Text = row.Points.Text = row.Bonus.Text = "";
        row.Mark.Texture = null;
    }
}
