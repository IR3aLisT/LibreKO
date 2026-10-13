using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const string DailyRankArt = "res://assets/ui/daily_rank/";
    private const string DailyRankBoardsFile = DailyRankArt + "boards.json";
    private const int NoDailyRankBoard = -1;
    private const double DailyRankBlinkSeconds = 1.0;
    private const int DailyRankInfoLines = 3;
    private static readonly Vector2 DailyRankOrigin = new(44, 62);
    private static readonly Vector2 DailyRankCanvas = new(495, 400);
    private static readonly Color DailyRankInfoColor = new("ead98a");
    private static readonly Color DailyRankPodiumColor = new("e4da96");
    private static readonly Color DailyRankMineColor = new("adff2f");
    private static readonly Color DailyRankUpColor = new("cd5c5c");
    private static readonly Color DailyRankDownColor = new("87ceeb");
    private static readonly int[] DailyRankRowTops = { 188, 212, 234, 256, 278, 300, 322, 343 };
    private static readonly int[] DailyRankPodiumTops = { 185, 246, 306 };

    private CanvasLayer _drLayer = null!;
    private HudWindow? _drWindow;
    private Button _drIcon = null!;
    private TextureRect _drIconImage = null!;
    private readonly List<DailyRankBoardInfo> _drBoards = new();
    private readonly Dictionary<int, DailyRankBoardData> _drCache = new();
    private int _drCurrent = NoDailyRankBoard;
    private double _drLastRequest = double.NegativeInfinity;
    private bool _drBlink;
    private bool _drBlinkLit;
    private double _drBlinkIn;
    private RichTextLabel _drInfo = null!;
    private OptionButton _drPicker = null!;
    private readonly Label[] _drPodium = new Label[DailyRank.PodiumPlaces];
    private readonly Label[] _drRanks = new Label[DailyRank.RowsPerPage];
    private readonly Label[] _drNames = new Label[DailyRank.RowsPerPage];
    private VScrollBar _drScroll = null!;
    private Label _drMyLabel = null!, _drMyPlace = null!, _drChange = null!;
    private TextureRect _drChangeIcon = null!;
    private Texture2D? _drUp, _drDown, _drSame;

    private void DailyRankInit()
    {
        _drLayer = new CanvasLayer { Layer = 63 };
        AddChild(_drLayer);
        LoadDailyRankBoards();
        _drIcon = TopIconButton(_topIconLayer, "system/medal", SystemText(DailyRank.TitleText, "Daily Rank"),
            OnDailyRankIcon, out _drIconImage);
        _drIcon.Resized += PlaceDailyRankIcon;
        Callable.From(PlaceDailyRankIcon).CallDeferred();
        _drBlink = DailyRank.ShouldBlink(Config.DailyRankLastViewDay, DateTime.Now);
        Net.I.DailyRankBoardEvent += OnDailyRankBoard;
        Net.I.DailyRankResetEvent += OnDailyRankReset;
    }

    private void DailyRankDispose()
    {
        Net.I.DailyRankBoardEvent -= OnDailyRankBoard;
        Net.I.DailyRankResetEvent -= OnDailyRankReset;
    }

    private void LoadDailyRankBoards()
    {
        _drBoards.Clear();
        using var file = Godot.FileAccess.Open(DailyRankBoardsFile, Godot.FileAccess.ModeFlags.Read);
        if (file == null) return;
        foreach (var row in Json.ParseString(file.GetAsText()).AsGodotArray())
        {
            var board = row.AsGodotDictionary();
            var lines = board["lines"].AsGodotArray().Select(line => line.AsString()).ToArray();
            _drBoards.Add(new DailyRankBoardInfo(board["id"].AsInt32(), board["title"].AsString(), lines));
        }
    }

    private void PlaceDailyRankIcon() => PlaceTopIcon(_drIcon, _drIconImage, HudPlacement.DailyRankIcon);

    private void OnDailyRankIcon()
    {
        ToggleDailyRank();
        Config.SetDailyRankLastViewDay(DailyRank.DayKey(DateTime.Now));
        _drBlink = false;
        _drIconImage.SelfModulate = TopIconColor(false);
    }

    private void DailyRankTick(double delta)
    {
        if (!_drBlink || !IsInstanceValid(_drIconImage)) return;
        _drBlinkIn -= delta;
        if (_drBlinkIn > 0) return;
        _drBlinkIn = DailyRankBlinkSeconds;
        _drBlinkLit = !_drBlinkLit;
        _drIconImage.SelfModulate = TopIconColor(_drBlinkLit);
    }

    private void ToggleDailyRank()
    {
        if (_drWindow is { Visible: true })
        {
            _drWindow.Visible = false;
            return;
        }
        _drWindow ??= BuildDailyRank();
        if (_drCurrent == NoDailyRankBoard) ShowDailyRankBoard(DailyRank.FirstBoard);
        else if (DailyRank.AlwaysRequested(_drCurrent)) ShowDailyRankBoard(_drCurrent);
        else RenderDailyRank(_drCurrent);
        ShowOnTop(_drWindow);
    }

    private void ShowDailyRankBoard(int board)
    {
        if (!DailyRank.AlwaysRequested(board) && _drCache.ContainsKey(board))
        {
            RenderDailyRank(board);
            return;
        }
        double now = Time.GetTicksMsec() / 1000.0;
        double waited = now - _drLastRequest;
        if (waited < DailyRank.RequestGapSeconds)
        {
            CombatNotice(SystemText(DailyRank.TooSoonText, "It is available in %d seconds")
                .Replace("%d", ((int)(DailyRank.RequestGapSeconds - waited)).ToString()), UiTheme.Bad);
            return;
        }
        _drLastRequest = now;
        Net.I.SendDailyRankRequest(board);
    }

    private void OnDailyRankBoard(DailyRankBoardData data)
    {
        _drCache[data.Board] = data;
        if (_drWindow != null) RenderDailyRank(data.Board);
    }

    private void OnDailyRankReset()
    {
        _drCache.Clear();
        _drCurrent = NoDailyRankBoard;
        if (_drWindow != null) _drWindow.Visible = false;
    }

    private HudWindow BuildDailyRank()
    {
        _drUp = GD.Load<Texture2D>(DailyRankArt + "change_up.png");
        _drDown = GD.Load<Texture2D>(DailyRankArt + "change_down.png");
        _drSame = GD.Load<Texture2D>(DailyRankArt + "change_same.png");

        var window = new HudWindow("daily_rank", SystemText(DailyRank.TitleText, "Daily Rank"), new Vector2(320, 110)) { Visible = false };
        window.Closed += () => window.Visible = false;
        _drLayer.AddChild(window);
        var canvas = new Control { CustomMinimumSize = DailyRankCanvas, MouseFilter = Control.MouseFilterEnum.Pass };
        window.Body.AddChild(canvas);

        _drInfo = new RichTextLabel { BbcodeEnabled = true, FitContent = true, ScrollActive = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        _drInfo.AddThemeColorOverride("default_color", DailyRankInfoColor);
        _drInfo.AddThemeFontSizeOverride("normal_font_size", 12);
        DailyRankPlace(canvas, _drInfo, new Rect2(54, 69, 473, 52));

        _drPicker = UiTheme.Dropdown(_drBoards.Select(board => board.Title).ToList());
        _drPicker.ItemSelected += index => ShowDailyRankBoard(_drBoards[(int)index].Id);
        DailyRankPlace(canvas, _drPicker, new Rect2(300, 125, 231, 26));

        var podium = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        podium.AddThemeStyleboxOverride("panel", UiTheme.Panel());
        DailyRankPlace(canvas, podium, new Rect2(50, 181, 212, 189));
        for (int i = 0; i < DailyRank.PodiumPlaces; i++)
        {
            var medal = new TextureRect
            {
                Texture = GD.Load<Texture2D>($"{DailyRankArt}medal_{i + 1}.png"),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            DailyRankPlace(canvas, medal, new Rect2(54, DailyRankPodiumTops[i], 60, 60));
            _drPodium[i] = UiTheme.Text("", 14, DailyRankPodiumColor);
            _drPodium[i].VerticalAlignment = VerticalAlignment.Center;
            _drPodium[i].ClipText = true;
            DailyRankPlace(canvas, _drPodium[i], new Rect2(121, DailyRankPodiumTops[i], 144, 60));
        }

        var list = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        list.AddThemeStyleboxOverride("panel", UiTheme.Panel());
        list.GuiInput += OnDailyRankListInput;
        DailyRankPlace(canvas, list, new Rect2(267, 181, 264, 197));
        for (int i = 0; i < DailyRank.RowsPerPage; i++)
        {
            _drRanks[i] = UiTheme.Text("", 12, UiTheme.TextHi, HorizontalAlignment.Center);
            DailyRankPlace(canvas, _drRanks[i], new Rect2(271, DailyRankRowTops[i], 40, 18));
            _drNames[i] = UiTheme.Text("", 12, UiTheme.TextHi);
            _drNames[i].ClipText = true;
            DailyRankPlace(canvas, _drNames[i], new Rect2(324, DailyRankRowTops[i], 170, 18));
        }
        _drScroll = new VScrollBar { MinValue = 0, Step = 1, Page = 1, MouseFilter = Control.MouseFilterEnum.Stop };
        _drScroll.ValueChanged += _ => RenderDailyRankRows();
        DailyRankPlace(canvas, _drScroll, new Rect2(505, 185, 16, 190));

        _drMyLabel = UiTheme.Text("", 12, DailyRankMineColor, HorizontalAlignment.Right);
        DailyRankPlace(canvas, _drMyLabel, new Rect2(161, 386, 112, 20));
        _drMyPlace = UiTheme.Text("", 12, DailyRankMineColor, HorizontalAlignment.Center);
        DailyRankPlace(canvas, _drMyPlace, new Rect2(279, 386, 59, 20));
        _drChangeIcon = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        DailyRankPlace(canvas, _drChangeIcon, new Rect2(341, 384, 24, 24));
        _drChange = UiTheme.Text("", 12, UiTheme.TextHi);
        DailyRankPlace(canvas, _drChange, new Rect2(367, 386, 60, 20));

        var footer = UiTheme.Text(SystemText(DailyRank.FooterText, "Daily Rank updates everyday and resets every month"),
            12, DailyRankInfoColor, HorizontalAlignment.Center);
        footer.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        DailyRankPlace(canvas, footer, new Rect2(54, 426, 473, 30));
        return window;
    }

    private static void DailyRankPlace(Control canvas, Control child, Rect2 reference)
    {
        canvas.AddChild(child);
        child.Position = reference.Position - DailyRankOrigin;
        child.Size = reference.Size;
        child.CustomMinimumSize = reference.Size;
    }

    private void OnDailyRankListInput(InputEvent e)
    {
        if (e is not InputEventMouseButton { Pressed: true } wheel) return;
        if (wheel.ButtonIndex == MouseButton.WheelUp) _drScroll.Value -= 1;
        else if (wheel.ButtonIndex == MouseButton.WheelDown) _drScroll.Value += 1;
    }

    private void RenderDailyRank(int board)
    {
        if (_drWindow == null) return;
        _drCurrent = board;
        var info = _drBoards.FirstOrDefault(b => b.Id == board);
        if (info != null)
        {
            _drInfo.Text = string.Join("\n", info.Lines.Take(DailyRankInfoLines));
            _drPicker.Select(_drBoards.IndexOf(info));
        }

        _drCache.TryGetValue(board, out var data);
        var entries = data?.Entries ?? new List<DailyRankEntry>();
        for (int i = 0; i < DailyRank.PodiumPlaces; i++)
            _drPodium[i].Text = i < entries.Count ? entries[i].Name : "";

        _drMyLabel.Text = board == DailyRank.ClanBoard
            ? SystemText(DailyRank.MyClanRankText, "My Clan Rank")
            : SystemText(DailyRank.MyRankText, "My Rank:");
        _drMyPlace.Text = DailyRank.PlaceShown(data?.MyPlace ?? 0);
        var (text, icon) = DailyRank.ChangeShown(board, data?.Change ?? 0);
        _drChange.Text = text;
        _drChange.AddThemeColorOverride("font_color", icon switch
        {
            DailyRankChangeIcon.Up => DailyRankUpColor,
            DailyRankChangeIcon.Down => DailyRankDownColor,
            _ => UiTheme.TextHi,
        });
        _drChangeIcon.Texture = icon switch
        {
            DailyRankChangeIcon.Up => _drUp,
            DailyRankChangeIcon.Down => _drDown,
            DailyRankChangeIcon.Same => _drSame,
            _ => null,
        };

        _drScroll.MaxValue = DailyRank.PageCount(entries.Count);
        _drScroll.SetValueNoSignal(0);
        RenderDailyRankRows();
    }

    private void RenderDailyRankRows()
    {
        var entries = _drCache.TryGetValue(_drCurrent, out var data) ? data.Entries : new List<DailyRankEntry>();
        var page = DailyRank.Page(entries, (int)_drScroll.Value).ToList();
        for (int i = 0; i < DailyRank.RowsPerPage; i++)
        {
            _drRanks[i].Text = i < page.Count ? page[i].Rank.ToString() : "";
            _drNames[i].Text = i < page.Count ? page[i].Name : "";
        }
    }
}
