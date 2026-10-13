using System.Collections.Generic;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private VBoxContainer _friendsContent = null!;
    private VBoxContainer _friendsList = null!;
    private LineEdit _friendAddInput = null!;
    private Label _friendHeader = null!, _friendStatus = null!;
    private bool _friendsLoaded;
    private readonly List<FriendEntry> _friends = new();
    private static readonly Color FriendNoticeColour = new("ffff00");
    private static readonly Color FriendRefusalColour = new("ff0000");


    private bool FriendsPageVisible =>
        MainWindowOpen("Character") && _friendsContent is { Visible: true };

    private void FriendsInit()
    {
        BuildFriendsPanel();
        Net.I.FriendListEvent += OnFriendList;
        Net.I.FriendAddResultEvent += OnFriendAdd;
        Net.I.FriendRemoveResultEvent += OnFriendRemove;
        Net.I.FriendLoggedInEvent += OnFriendLoggedIn;
        Net.I.FriendRequestEvent += OnFriendRequest;
        Net.I.FriendRequestResultEvent += OnFriendRequestResult;
    }

    private void FriendsDispose()
    {
        Net.I.FriendListEvent -= OnFriendList;
        Net.I.FriendAddResultEvent -= OnFriendAdd;
        Net.I.FriendRemoveResultEvent -= OnFriendRemove;
        Net.I.FriendLoggedInEvent -= OnFriendLoggedIn;
        Net.I.FriendRequestEvent -= OnFriendRequest;
        Net.I.FriendRequestResultEvent -= OnFriendRequestResult;
    }

    private void RequestFriend(string name)
    {
        Net.I.SendFriendAdd(name);
        FriendLine(ItemData.Text(FriendTexts.RequestSentText, "You have sent a friend request to %s.").Replace("%s", name), FriendNoticeColour);
    }

    private void FriendLine(string text, Color colour)
    {
        if (text.Length == 0) return;
        CombatLogAdd(text, CombatLogKind.Status, colour);
        if (FriendsPageVisible) SetFriendStatus(text, colour == FriendRefusalColour);
    }

    private void OnFriendLoggedIn(string name)
    {
        FriendLine(ItemData.Text(FriendTexts.LoggedInText, "%s has logged in.").Replace("%s", name), FriendNoticeColour);
        Net.I.SendFriendListRequest();
    }

    private void OnFriendRequest(string requester)
    {
        bool answered = false;
        var ask = Notice.Confirm(this,
            ItemData.Text(FriendTexts.RequestAskText, "[%s] sent a friend request. Will you accept?").Replace("%s", requester),
            "OK", "Cancel",
            () => { answered = true; Net.I.SendFriendAnswer(requester, true); },
            () => { answered = true; Net.I.SendFriendAnswer(requester, false); },
            title: "Friends");
        GetTree().CreateTimer(FriendTexts.RequestSeconds).Timeout += () =>
        {
            if (answered || !IsInstanceValid(ask)) return;
            answered = true;
            Net.I.SendFriendAnswer(requester, false);
            ask.QueueFree();
        };
    }

    private void OnFriendRequestResult(byte code)
    {
        if (FriendTexts.RequestResultText(code) is { } textId)
            FriendLine(ItemData.Text(textId, ""), FriendRefusalColour);
    }

    private void BuildFriendsPanel()
    {
        var root = new VBoxContainer { CustomMinimumSize = new Vector2(CharacterPageWidth, 0) };
        root.AddThemeConstantOverride("separation", 8);
        _friendsContent = root;

        _friendHeader = UiTheme.Text("", 12, UiTheme.TextLo);
        root.AddChild(_friendHeader);

        var addRow = new HBoxContainer();
        addRow.AddThemeConstantOverride("separation", 6);
        _friendAddInput = new LineEdit { PlaceholderText = "Character name", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MaxLength = 20 };
        _friendAddInput.TextSubmitted += _ => DoFriendAdd();
        addRow.AddChild(_friendAddInput);
        var addBtn = new Button { Text = "Add", FocusMode = Control.FocusModeEnum.None };
        addBtn.Pressed += DoFriendAdd;
        addRow.AddChild(addBtn);
        root.AddChild(addRow);

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(1, 300),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        root.AddChild(scroll);
        _friendsList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _friendsList.AddThemeConstantOverride("separation", 3);
        scroll.AddChild(_friendsList);

        root.AddChild(new HSeparator());
        var footer = new HBoxContainer();
        _friendStatus = UiTheme.Text("", 12, UiTheme.TextLo);
        _friendStatus.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        footer.AddChild(_friendStatus);
        var refresh = new Button { Text = "Refresh", FocusMode = Control.FocusModeEnum.None };
        refresh.Pressed += () => Net.I.SendFriendListRequest();
        footer.AddChild(refresh);
        root.AddChild(footer);
    }

    private void EnsureFriendsLoaded()
    {
        if (_friendsLoaded) return;
        _friendsLoaded = true;
        Net.I.SendFriendListRequest();
    }

    private void DoFriendAdd()
    {
        string name = _friendAddInput.Text.Trim();
        if (name.Length == 0) return;
        if (name == (Net.I.LastEnter.Name ?? "")) { SetFriendStatus("You can't add yourself.", true); return; }
        RequestFriend(name);
        _friendAddInput.Clear();
    }

    private void OnFriendList(List<FriendEntry> list)
    {
        _friends.Clear();
        _friends.AddRange(list);
        RefreshFriends();
        RefreshPlayerNameColours();
    }

    private void OnFriendAdd(byte code, string name, FriendEntry entry)
    {
        if (code == FriendTexts.Succeeded && name.Length > 0)
        {
            _friends.RemoveAll(f => string.Equals(f.Name, name, System.StringComparison.OrdinalIgnoreCase));
            _friends.Add(entry);
            RefreshFriends();
        }
        if (FriendTexts.AddResultText(code) is { } textId)
            FriendLine(ItemData.Text(textId, ""), code == FriendTexts.Succeeded ? FriendNoticeColour : FriendRefusalColour);
    }

    private void OnFriendRemove(byte code, string name)
    {
        if (code == FriendTexts.Succeeded)
        {
            _friends.RemoveAll(f => string.Equals(f.Name, name, System.StringComparison.OrdinalIgnoreCase));
            RefreshFriends();
        }
        if (FriendTexts.RemoveResultText(code) is { } textId)
            FriendLine(ItemData.Text(textId, ""), FriendRefusalColour);
    }

    private void RefreshFriends()
    {
        foreach (var c in _friendsList.GetChildren()) c.QueueFree();
        int online = 0;
        _friends.Sort((a, b) => b.Status.CompareTo(a.Status));
        foreach (var f in _friends)
        {
            if (f.IsOnline) online++;
            _friendsList.AddChild(BuildFriendRow(f));
        }
        if (_friends.Count == 0)
            _friendsList.AddChild(UiTheme.Text("No friends yet. Add one above.", 12, UiTheme.TextLo, HorizontalAlignment.Center));
        _friendHeader.Text = $"Friends  ({online} online)";
        PluginHost.Game.RaiseFriends();
    }

    private Control BuildFriendRow(FriendEntry f)
    {
        var row = new PanelContainer();
        row.AddThemeStyleboxOverride("panel", UiTheme.Row());
        var hb = new HBoxContainer();
        hb.AddThemeConstantOverride("separation", 6);
        row.AddChild(hb);

        var dot = UiTheme.Text("●", 12, f.InParty ? UiTheme.Neutral : f.IsOnline ? UiTheme.Good : UiTheme.TextDim);
        hb.AddChild(dot);

        var text = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        text.AddThemeConstantOverride("separation", -2);
        hb.AddChild(text);

        var name = UiTheme.Text(f.Name, 13, f.IsOnline ? UiTheme.TextHi : UiTheme.TextLo);
        text.AddChild(name);
        text.AddChild(UiTheme.Text(FriendDetailLine(f), 11, UiTheme.TextDim));

        string n = f.Name;
        var whisper = new Button
        {
            Text = "Whisper",
            TooltipText = $"Whisper {f.Name}",
            FocusMode = Control.FocusModeEnum.None,
            Disabled = !f.IsOnline,
        };
        whisper.AddThemeFontSizeOverride("font_size", 10);
        whisper.Pressed += () => OpenWhisperWith(n);
        hb.AddChild(whisper);

        var party = new Button
        {
            Text = "Party",
            TooltipText = f.InParty ? $"{f.Name} is already in a party" : $"Invite {f.Name} to your party",
            FocusMode = Control.FocusModeEnum.None,
            Disabled = !f.IsOnline || f.InParty,
        };
        party.AddThemeFontSizeOverride("font_size", 10);
        party.Pressed += () => InvitePlayerToParty(n);
        hb.AddChild(party);

        var rm = UiTheme.IconButton("×", $"Remove {f.Name}");
        rm.CustomMinimumSize = new Vector2(22, 22);
        rm.Pressed += () => Net.I.SendFriendRemove(n);
        hb.AddChild(rm);
        return row;
    }

    private static string FriendDetailLine(FriendEntry f)
    {
        var parts = new List<string>(4);
        if (f.Level > 0) parts.Add($"Lv {f.Level}");
        if (f.Class > 0) parts.Add(ClassName(f.Class));
        if (f.Nation is Nations.Karus or Nations.ElMorad) parts.Add(Nations.Name(f.Nation));

        if (f.InParty) parts.Add("in party");
        else if (!f.IsOnline) parts.Add("offline");
        else parts.Add(FriendZoneName(f.ZoneId));

        return string.Join("   ·   ", parts);
    }

    private static string FriendZoneName(int zoneId)
    {
        if (zoneId <= 0) return "online";
        foreach (var zone in ZoneCatalog.All)
            if (zone.Id == zoneId) return zone.Name;
        return $"zone {zoneId}";
    }

    private void SetFriendStatus(string text, bool warn)
    {
        _friendStatus.Text = text;
        _friendStatus.AddThemeColorOverride("font_color", warn ? UiTheme.Bad : UiTheme.TextLo);
    }
}
