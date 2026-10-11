using System.Collections.Generic;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private CanvasLayer _seekLayer = null!;
    private Control _seekPanel = null!;
    private VBoxContainer _seekListBox = null!;
    private Label _seekPageLbl = null!, _seekStatusLbl = null!;
    private Button _seekRegisterBtn = null!;
    private OptionButton _wantedClassOpt = null!;
    private LineEdit _wantedMsgInput = null!;
    private HBoxContainer _seekFilters = null!, _fullMoonRow = null!;
    private Label _seekHeading = null!;
    private int _seekBoard = FullMoon.NoBoard;
    private byte SeekBoard => _seekBoard == FullMoon.FullMoonBoard ? FullMoon.FullMoonBoard : FullMoon.NormalBoard;

    private bool _seekShown;
    private bool _seeking;
    private int _seekPage;
    private int _seekTotal;
    private PartyBoardFilter _seekFilter = new(PartyBoardKind.All, false, false);
    private List<PartyBbsEntry> _seekEntries = new();

    private static readonly (string Label, int Code)[] WantedClasses =
    {
        ("Any", 0), ("Warrior", 1), ("Rogue", 2), ("Mage", 3), ("Priest", 4),
    };

    private void BuildSeekPartyPanel()
    {
        _seekLayer = new CanvasLayer { Layer = 71 };
        AddChild(_seekLayer);

        var win = new HudWindow("seek_party", "Party Board", new Vector2(300, 110), 420) { Visible = false };
        win.Closed += () =>
        {
            _seekShown = false;
            if (!_seeking) _seekBoard = FullMoon.NoBoard;
        };
        _seekLayer.AddChild(win);
        _seekPanel = win;

        var root = win.Body;
        root.AddThemeConstantOverride("separation", 6);

        _seekHeading = UiTheme.Text("", 14, UiTheme.GoldBright);
        root.AddChild(_seekHeading);
        var top = new HBoxContainer(); top.AddThemeConstantOverride("separation", 5); root.AddChild(top);
        _seekRegisterBtn = SmallButton("Look for a party", top);
        _seekRegisterBtn.Pressed += ToggleSeeking;
        SmallButton("Refresh", top).Pressed += () => Net.I.SendPartyBbsList(_seekPage, SeekBoard);
        SmallButton("Invite by name", top).Pressed += OpenInvitePrompt;

        _seekStatusLbl = HudStyle.Label(12);
        _seekStatusLbl.AddThemeColorOverride("font_color", new Color("b9c0c8"));
        root.AddChild(_seekStatusLbl);

        var filters = new HBoxContainer();
        filters.AddThemeConstantOverride("separation", 8);
        root.AddChild(filters);
        _seekFilters = filters;
        _fullMoonRow = new HBoxContainer { Visible = false };
        _fullMoonRow.AddThemeConstantOverride("separation", 8);
        root.AddChild(_fullMoonRow);
        SmallButton("Enter", _fullMoonRow).Pressed += EnterFullMoon;
        SmallButton("Party Board", _fullMoonRow).Pressed += ShoutFullMoonParty;
        var kind = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        kind.AddThemeFontSizeOverride("font_size", 12);
        kind.AddItem("Everyone", (int)PartyBoardKind.All);
        kind.AddItem("Players", (int)PartyBoardKind.Players);
        kind.AddItem("Parties", (int)PartyBoardKind.Parties);
        kind.ItemSelected += index => SetSeekFilter(_seekFilter with { Kind = (PartyBoardKind)index });
        filters.AddChild(kind);
        var zone = new CheckBox { Text = "This zone", FocusMode = Control.FocusModeEnum.None };
        zone.AddThemeFontSizeOverride("font_size", 12);
        zone.Toggled += on => SetSeekFilter(_seekFilter with { SameZone = on });
        filters.AddChild(zone);
        var band = new CheckBox { Text = "My level range", FocusMode = Control.FocusModeEnum.None };
        band.AddThemeFontSizeOverride("font_size", 12);
        band.Toggled += on => SetSeekFilter(_seekFilter with { MyLevelBand = on });
        filters.AddChild(band);

        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(420, 230) };
        root.AddChild(scroll);
        _seekListBox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _seekListBox.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(_seekListBox);

        var nav = new HBoxContainer(); nav.AddThemeConstantOverride("separation", 6); root.AddChild(nav);
        SmallButton("◄ Prev", nav).Pressed += () => { if (_seekPage > 0) { _seekPage--; Net.I.SendPartyBbsList(_seekPage, SeekBoard); } };
        _seekPageLbl = HudStyle.Label(12); _seekPageLbl.Text = "Page 1"; nav.AddChild(_seekPageLbl);
        SmallButton("Next ►", nav).Pressed += () =>
        {
            if ((_seekPage + 1) * 10 < _seekTotal) { _seekPage++; Net.I.SendPartyBbsList(_seekPage, SeekBoard); }
        };

        root.AddChild(new HSeparator());

        var wlbl = HudStyle.Label(13); wlbl.Text = "Recruit (party leader only):"; root.AddChild(wlbl);
        var wrow = new HBoxContainer(); wrow.AddThemeConstantOverride("separation", 5); root.AddChild(wrow);
        _wantedClassOpt = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        _wantedClassOpt.AddThemeFontSizeOverride("font_size", 12);
        for (int i = 0; i < WantedClasses.Length; i++) _wantedClassOpt.AddItem(WantedClasses[i].Label, i);
        wrow.AddChild(_wantedClassOpt);
        _wantedMsgInput = new LineEdit { MaxLength = 60, PlaceholderText = "Recruiting message",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _wantedMsgInput.TextSubmitted += _ => PostWanted();
        wrow.AddChild(_wantedMsgInput);
        SmallButton("Post", wrow).Pressed += PostWanted;
    }

    private void ToggleSeekParty()
    {
        if (!_seekShown && _seekBoard == FullMoon.NoBoard) _seekBoard = FullMoon.NormalBoard;
        _seekShown = !_seekShown;
        _seekPanel.Visible = _seekShown;
        if (_seekShown) { ApplySeekBoard(); Net.I.SendPartyBbsList(_seekPage, SeekBoard); UpdateSeekStatus(); }
    }

    private void ApplySeekBoard()
    {
        bool fullMoon = SeekBoard == FullMoon.FullMoonBoard;
        _seekFilters.Visible = !fullMoon;
        _fullMoonRow.Visible = fullMoon;
        _seekHeading.Visible = fullMoon;
        _seekHeading.Text = fullMoon ? ItemData.Text(FullMoon.ZoneNameText, "Full Moon Rift") : "";
        if (fullMoon) _seekFilter = new PartyBoardFilter(PartyBoardKind.All, false, false);
    }

    private void ToggleSeeking()
    {
        if (_seeking) Net.I.SendPartyBbsDelete(SeekBoard);
        else Net.I.SendPartyBbsRegister(SeekBoard);
    }

    private void PostWanted()
    {
        int code = WantedClasses[Mathf.Clamp(_wantedClassOpt.Selected, 0, WantedClasses.Length - 1)].Code;
        Net.I.SendPartyBbsWanted(code, _seekPage, _wantedMsgInput.Text.Trim(), SeekBoard);
    }

    private void UpdateSeekStatus()
    {
        _seekRegisterBtn.Text = _seeking ? "Stop looking" : "Look for a party";
        _seekStatusLbl.Text = _seeking
            ? "You are listed as looking for a party."
            : "Browse players seeking a party, or list yourself.";
    }

    private void OnBbsRegister(bool ok)
    {
        if (ok) { _seeking = true; CombatNotice("You are now listed as looking for a party."); }
        else CombatNotice("Can't seek a party while you're already in one.");
        UpdateSeekStatus();
        if (_seekShown) Net.I.SendPartyBbsList(_seekPage, SeekBoard);
    }

    private void OnBbsDelete()
    {
        _seeking = false;
        CombatNotice("Removed your seek-party listing.");
        UpdateSeekStatus();
        if (_seekShown) Net.I.SendPartyBbsList(_seekPage, SeekBoard);
    }

    private void OnBbsWantedFail() => CombatNotice("Only the party leader can post a recruiting message.");

    private void SetSeekFilter(PartyBoardFilter filter)
    {
        _seekFilter = filter;
        if (_seekShown) ShowSeekEntries();
    }

    private void OnBbsList(int page, int total, List<PartyBbsEntry> entries)
    {
        _seekPage = page;
        _seekTotal = total;
        _seekEntries = entries;
        if (!_seekShown) return;
        ShowSeekEntries();
    }

    private void ShowSeekEntries()
    {
        int page = _seekPage, total = _seekTotal;
        string self = Net.I.LastEnter.Name ?? "";
        var entries = _seekEntries.FindAll(e => _seekFilter.Passes(e, _zone, Sheet.Level)
            && !string.Equals(e.Name, self, System.StringComparison.OrdinalIgnoreCase));

        foreach (var c in _seekListBox.GetChildren()) c.QueueFree();

        int pages = Mathf.Max(1, (total + 9) / 10);
        _seekPageLbl.Text = $"Page {page + 1} / {pages}";

        if (entries.Count == 0)
        {
            var empty = HudStyle.Label(13);
            empty.Text = _seekEntries.Count > 0 ? "Nobody here matches these filters." : "Nobody is seeking a party right now.";
            empty.AddThemeColorOverride("font_color", new Color("b9c0c8"));
            _seekListBox.AddChild(empty);
            return;
        }

        foreach (var e in entries)
            _seekListBox.AddChild(BuildSeekRow(e));
    }

    private Control BuildSeekRow(PartyBbsEntry e)
    {
        var panel = new PanelContainer();
        var sb = new StyleBoxFlat { BgColor = new Color(1, 1, 1, 0.05f) };
        foreach (var s in new[] { "left", "right", "top", "bottom" }) sb.Set($"content_margin_{s}", 6f);
        sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight = sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 3;
        panel.AddThemeStyleboxOverride("panel", sb);

        var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 8);
        panel.AddChild(row);

        var info = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        info.AddThemeConstantOverride("separation", 1);
        row.AddChild(info);

        var head = HudStyle.Label(14);
        head.Text = e.IsLeaderRecruiting
            ? $"{e.Name}  ·  party {e.MemberCount}/8  ·  wants {ClassName(e.ClassOrWanted)}"
            : $"{e.Name}  ·  Lv {e.Level}  {ClassName(e.ClassOrWanted)}";
        info.AddChild(head);

        var sub = HudStyle.Label(11);
        string zone = SeekZoneName(e.ZoneId);
        sub.Text = e.Message.Length > 0 ? $"[{zone}]  {e.Message}" : $"[{zone}]";
        sub.AddThemeColorOverride("font_color", new Color("b9c0c8"));
        info.AddChild(sub);

        string name = e.Name;
        var inviteBtn = SmallButton("Invite", row);
        inviteBtn.Pressed += () =>
        {
            Net.I.SendPartyBbsInvite(name, SeekBoard);
            CombatNotice($"{name} {ItemData.Text(PartyInvitedText, "was invited into the party. Waiting for a response.")}");
        };
        return panel;
    }

    private static string SeekZoneName(int zoneId)
    {
        foreach (var z in ZoneCatalog.All) if (z.Id == zoneId) return z.Name;
        return $"Zone {zoneId}";
    }
}
