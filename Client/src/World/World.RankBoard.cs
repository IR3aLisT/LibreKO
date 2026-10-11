using System.Collections.Generic;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const string RankBoardKarusTitle = "Karus Board";
    private const string RankBoardElMoradTitle = "El Morad Board";
    private const string RankBoardFooter =
        ">>>The representatives on the list of selection criteria changes at 7am the day after a lunar war.<<<";
    private const string RankBoardEmptyCell = "-";
    private const int RankBoardFont = 12;
    private const int RankBoardHeadFont = 11;
    private const float RankBoardRankWidth = 34f;
    private const float RankBoardMarkWidth = 66f;
    private const float RankBoardNameWidth = 150f;
    private const float RankBoardClanWidth = 150f;
    private const float RankBoardWorkWidth = 120f;
    private const float RankBoardMarkSize = 18f;
    private const float RankBoardRowHeight = 24f;
    private static readonly Color RankBoardHeadColour = new("ff9d3c");
    private static readonly Color RankBoardTitleColour = new("f7eea4");
    private static readonly Color RankBoardFirstColour = new("80ffff");
    private static readonly Color RankBoardClanColour = new("b6e0d9");
    private static readonly Color RankBoardFooterColour = new(1f, 1f, 0f);

    private sealed class RankBoardCells
    {
        public Label Rank = null!;
        public TextureRect Mark = null!;
        public Label Name = null!;
        public Label Clan = null!;
        public Label? Work;
    }

    private CanvasLayer? _rankBoardLayer;
    private HudWindow _rankBoard = null!;
    private Label _rankBoardTitle = null!;
    private RankBoardCells _rankBoardKing = null!;
    private readonly RankBoardCells[] _rankBoardClans = new RankBoardCells[RankBoard.Rows];
    private readonly RankBoardCells[] _rankBoardPlayers = new RankBoardCells[RankBoard.Rows];
    private readonly RankBoardCells[] _rankBoardCommanders = new RankBoardCells[RankBoard.HallRows];
    private readonly RankBoardCells[] _rankBoardKillers = new RankBoardCells[RankBoard.HallRows];
    private List<RankBoardRow> _rankBoardRows = new();

    private void RankBoardInit() => Net.I.RankBoardEvent += OnRankBoard;

    private void RankBoardDispose()
    {
        Net.I.RankBoardEvent -= OnRankBoard;
        _rankBoardLayer = null;
    }

    private void OnRankBoard(List<RankBoardRow> rows)
    {
        if (rows.Count == 0)
        {
            Notice.Show(this, ItemData.Text(RankBoard.EmptyBoardText, "No content in current board"));
            return;
        }
        CloseNpcDialog();
        if (_rankBoardLayer == null || !IsInstanceValid(_rankBoardLayer)) BuildRankBoard();
        _rankBoardRows = rows;
        FillRankBoard();
        _rankBoardLayer!.Visible = true;
        _rankBoard.Visible = true;
    }

    private void FillRankBoard()
    {
        _rankBoardTitle.Text = Net.I.LastEnter.Nation == Nations.Karus ? RankBoardKarusTitle : RankBoardElMoradTitle;
        FillRankBoardCells(_rankBoardKing, RankBoard.Column(_rankBoardRows, RankBoardKind.King, 1)[0], -1);
        FillRankBoardColumn(_rankBoardClans, RankBoardKind.Clan);
        FillRankBoardColumn(_rankBoardPlayers, RankBoardKind.Player);
        FillRankBoardColumn(_rankBoardCommanders, RankBoardKind.CommanderKiller);
        FillRankBoardColumn(_rankBoardKillers, RankBoardKind.TopKiller);
    }

    private void FillRankBoardColumn(RankBoardCells[] cells, RankBoardKind kind)
    {
        var column = RankBoard.Column(_rankBoardRows, kind, cells.Length);
        for (int i = 0; i < cells.Length; i++) FillRankBoardCells(cells[i], column[i], i);
    }

    private void FillRankBoardCells(RankBoardCells cells, RankBoardRow? row, int index)
    {
        cells.Name.Text = row?.Name is { Length: > 0 } name ? name : RankBoardEmptyCell;
        cells.Clan.Text = row is { HasClan: true } ? row.Value.ClanName : "";
        var mark = row is { HasClan: true } ? ClanMark(row.Value.ClanId, row.Value.MarkVersion) : null;
        cells.Mark.Texture = mark;
        if (cells.Work != null)
            cells.Work.Text = row is { Kind: RankBoardKind.TopKiller, Tail: > 0 } killer ? $"{killer.Tail:n0}" : RankBoardEmptyCell;
        if (index >= 0) cells.Rank.Text = $"{index + 1}";
    }

    private void RefreshRankBoardMarks()
    {
        if (_rankBoardLayer != null && IsInstanceValid(_rankBoardLayer) && _rankBoard.Visible) FillRankBoard();
    }

    private void BuildRankBoard()
    {
        _rankBoardLayer = new CanvasLayer { Layer = 73 };
        AddChild(_rankBoardLayer);
        _rankBoard = new HudWindow("rank_board", "Rank Board") { Visible = false };
        _rankBoard.Closed += () => _rankBoard.Visible = false;
        _rankBoardLayer.AddChild(_rankBoard);
        var body = _rankBoard.Body;
        body.AddThemeConstantOverride("separation", 10);

        _rankBoardTitle = RankBoardLabel("", RankBoardFont + 2, RankBoardTitleColour, HorizontalAlignment.Center);
        body.AddChild(_rankBoardTitle);

        var king = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        king.AddThemeConstantOverride("separation", 8);
        var kingLabel = RankBoardLabel("King", RankBoardFont, RankBoardHeadColour, HorizontalAlignment.Right);
        kingLabel.CustomMinimumSize = new Vector2(RankBoardNameWidth / 2f, 0);
        king.AddChild(kingLabel);
        _rankBoardKing = RankBoardRowCells(king, withRank: false, withWork: false);
        body.AddChild(RankBoardSection(king));

        var ladders = new HBoxContainer();
        ladders.AddThemeConstantOverride("separation", 12);
        ladders.AddChild(RankBoardTable("Clan Ranking", "Clan Master", _rankBoardClans, withWork: false));
        ladders.AddChild(RankBoardTable("Ladder Ranking", "Character name", _rankBoardPlayers, withWork: false));
        body.AddChild(ladders);

        var halls = new HBoxContainer();
        halls.AddThemeConstantOverride("separation", 12);
        halls.AddChild(RankBoardTable("War Hall of fame · Hero who killed an enemy commander", "Character name", _rankBoardCommanders, withWork: true));
        halls.AddChild(RankBoardTable("War Hall of fame · Hero who has killed the most", "Character name", _rankBoardKillers, withWork: true));
        body.AddChild(halls);

        var footer = RankBoardLabel(RankBoardFooter, RankBoardHeadFont, RankBoardFooterColour, HorizontalAlignment.Center);
        body.AddChild(footer);
    }

    private Control RankBoardTable(string title, string nameHead, RankBoardCells[] cells, bool withWork)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", 2);
        box.AddChild(RankBoardLabel(title, RankBoardFont, RankBoardTitleColour, HorizontalAlignment.Left));

        var head = new HBoxContainer();
        head.AddChild(RankBoardHeadCell("Rank", RankBoardRankWidth));
        head.AddChild(RankBoardHeadCell("Clan mark", RankBoardMarkWidth));
        head.AddChild(RankBoardHeadCell(nameHead, RankBoardNameWidth, stretch: true));
        head.AddChild(RankBoardHeadCell("Clan name", RankBoardClanWidth, stretch: true));
        if (withWork) head.AddChild(RankBoardHeadCell("Hero achievements", RankBoardWorkWidth));
        box.AddChild(head);

        for (int i = 0; i < cells.Length; i++)
        {
            var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, RankBoardRowHeight) };
            cells[i] = RankBoardRowCells(row, withRank: true, withWork: withWork);
            if (i == 0) cells[i].Rank.AddThemeColorOverride("font_color", RankBoardFirstColour);
            box.AddChild(row);
        }
        return RankBoardSection(box);
    }

    private RankBoardCells RankBoardRowCells(HBoxContainer row, bool withRank, bool withWork)
    {
        var cells = new RankBoardCells
        {
            Rank = RankBoardLabel("", RankBoardFont, UiTheme.TextHi, HorizontalAlignment.Center),
            Mark = new TextureRect
            {
                CustomMinimumSize = new Vector2(RankBoardMarkWidth, RankBoardMarkSize),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            },
            Name = RankBoardLabel(RankBoardEmptyCell, RankBoardFont, UiTheme.TextHi, HorizontalAlignment.Center),
            Clan = RankBoardLabel("", RankBoardFont, RankBoardClanColour, HorizontalAlignment.Center),
        };
        cells.Rank.CustomMinimumSize = new Vector2(RankBoardRankWidth, 0);
        cells.Name.CustomMinimumSize = new Vector2(RankBoardNameWidth, 0);
        cells.Clan.CustomMinimumSize = new Vector2(RankBoardClanWidth, 0);
        cells.Name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        cells.Clan.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        if (withRank) row.AddChild(cells.Rank);
        row.AddChild(cells.Mark);
        row.AddChild(cells.Name);
        row.AddChild(cells.Clan);
        if (withWork)
        {
            cells.Work = RankBoardLabel(RankBoardEmptyCell, RankBoardHeadFont, UiTheme.TextHi, HorizontalAlignment.Center);
            cells.Work.CustomMinimumSize = new Vector2(RankBoardWorkWidth, 0);
            row.AddChild(cells.Work);
        }
        return cells;
    }

    private static PanelContainer RankBoardSection(Control content)
    {
        var section = UiTheme.Section();
        section.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        section.AddChild(content);
        return section;
    }

    private static Label RankBoardHeadCell(string text, float width, bool stretch = false)
    {
        var label = RankBoardLabel(text, RankBoardHeadFont, RankBoardHeadColour, HorizontalAlignment.Center);
        label.CustomMinimumSize = new Vector2(width, 0);
        if (stretch) label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        return label;
    }

    private static Label RankBoardLabel(string text, int size, Color colour, HorizontalAlignment align)
    {
        var label = UiTheme.Text(text, size, colour, align);
        label.ClipText = true;
        label.VerticalAlignment = VerticalAlignment.Center;
        return label;
    }
}
