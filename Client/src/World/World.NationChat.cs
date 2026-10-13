using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const float NationChatRowHeight = 22f;
    private const int NationChatListWidth = 440;
    private const int NationChatBarGap = 6;
    private const float NationChatSearchWidth = 180f;
    private const int NationChatSmallWidth = 250;
    private const string NationChatRowTip = "Double-click to enter";
    private static readonly Color NationChatMine = new("00ff00");
    private static readonly Color NationChatRefreshWait = new("ff3b3b");

    private CanvasLayer _ncLayer = null!;
    private HudWindow _ncList = null!;
    private bool _nationChatShown;
    private bool _ncOnPublic;
    private Button _ncPublicTab = null!;
    private Button _ncRefresh = null!;
    private Button _ncPrivateTab = null!;
    private VBoxContainer _ncPublicPage = null!;
    private VBoxContainer _ncPrivatePage = null!;
    private VBoxContainer _ncPublicRows = null!;
    private VBoxContainer _ncPrivateRows = null!;
    private Button _ncPubJoin = null!;
    private Button _ncPubLeave = null!;
    private Button _ncPubOpen = null!;
    private List<NationChannel> _ncChannels = [];
    private List<NationRoom> _ncRooms = [];
    private int _ncSelectedChannel = NationChatRules.NoRoom;
    private int _ncSelectedRoom = NationChatRules.NoRoom;
    private ulong _ncPublicRefreshAt;
    private ulong _ncPrivateRefreshAt;

    private HudWindow _ncCreate = null!;
    private LineEdit _ncCreateTitle = null!;
    private LineEdit _ncCreatePassword = null!;
    private Label _ncCreatePasswordTitle = null!;
    private OptionButton _ncCreateOption = null!;
    private OptionButton _ncCreateSize = null!;

    private HudWindow _ncPassword = null!;
    private LineEdit _ncPasswordEdit = null!;
    private LineEdit _ncSearchEdit = null!;
    private NationRoomMembers? _ncMemberList;

    private static string NcText(int id, string fallback) => ItemData.Text(id, fallback);

    private void NationChatInit()
    {
        _ncLayer = new CanvasLayer { Layer = 68 };
        _ncLayer.SetMeta(Audio.QuietMeta, true);
        AddChild(_ncLayer);
        BuildNationChannelList();
        BuildNationCreate();
        BuildNationPassword();

        Net.I.NationChannelsEvent += OnNationChannels;
        Net.I.NationRoomsEvent += OnNationRooms;
        Net.I.NationSearchEvent += OnNationSearch;
        Net.I.NationChatResultEvent += OnNationChatResult;
        Net.I.NationMembersEvent += OnNationMembers;
        Net.I.NationChatStateEvent += RefreshNationButtons;
        Net.I.RoomChatNoticeEvent += OnRoomChatNotice;
    }

    private void NationChatDispose()
    {
        Net.I.NationChannelsEvent -= OnNationChannels;
        Net.I.NationRoomsEvent -= OnNationRooms;
        Net.I.NationSearchEvent -= OnNationSearch;
        Net.I.NationChatResultEvent -= OnNationChatResult;
        Net.I.NationMembersEvent -= OnNationMembers;
        Net.I.NationChatStateEvent -= RefreshNationButtons;
        Net.I.RoomChatNoticeEvent -= OnRoomChatNotice;
    }

    internal void ToggleNationChat()
    {
        if (_nationChatShown) { CloseNationChat(); return; }
        _nationChatShown = true;
        _ncList.Visible = true;
        ShowNationTab(publicTab: false);
    }

    private void CloseNationChat()
    {
        if (!_nationChatShown) return;
        _nationChatShown = false;
        _ncList.Visible = false;
        _ncCreate.Visible = false;
        _ncPassword.Visible = false;
    }

    private void BuildNationChannelList()
    {
        _ncList = new HudWindow("nation_chat", "Channel list", bodyMinWidth: NationChatListWidth) { Visible = false };
        _ncList.Closed += CloseNationChat;
        _ncLayer.AddChild(_ncList);

        var tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 4);
        _ncList.Body.AddChild(tabs);
        _ncPublicTab = UiTheme.TopTabButton(NcText(NationChatTexts.Public, "Public"));
        _ncPublicTab.Pressed += () => ShowNationTab(publicTab: true);
        tabs.AddChild(_ncPublicTab);
        _ncPrivateTab = UiTheme.TopTabButton(NcText(NationChatTexts.Private, "Private"));
        _ncPrivateTab.Pressed += () => ShowNationTab(publicTab: false);
        tabs.AddChild(_ncPrivateTab);
        _ncRefresh = UiTheme.IconButton("⟳", "");
        _ncRefresh.Pressed += RefreshNationList;
        tabs.AddChild(_ncRefresh);

        _ncPublicPage = new VBoxContainer();
        _ncPublicPage.AddThemeConstantOverride("separation", 6);
        _ncList.Body.AddChild(_ncPublicPage);
        _ncPublicRows = NationRowBox(_ncPublicPage, NationChatRules.PublicRows);
        var pubButtons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        pubButtons.AddThemeConstantOverride("separation", 8);
        _ncPublicPage.AddChild(pubButtons);
        _ncPubJoin = NationButton(pubButtons, "Enter", JoinNationChannel);
        _ncPubLeave = NationButton(pubButtons, "Leave", () => Net.I.SendNationPublicLeave());
        _ncPubOpen = NationButton(pubButtons, "Open", ShowChatRoom);
        NationButton(pubButtons, "Cancel", CloseNationChat);

        _ncPrivatePage = new VBoxContainer();
        _ncPrivatePage.AddThemeConstantOverride("separation", 6);
        _ncList.Body.AddChild(_ncPrivatePage);
        _ncPrivateRows = NationRowBox(_ncPrivatePage, NationChatRules.PrivateRows);
        var bar = new HBoxContainer();
        bar.AddThemeConstantOverride("separation", NationChatBarGap);
        _ncPrivatePage.AddChild(bar);
        _ncSearchEdit = new LineEdit
        {
            MaxLength = NationChatRules.TitleMaxLength,
            PlaceholderText = "Room name",
            CustomMinimumSize = new Vector2(NationChatSearchWidth, 0),
        };
        _ncSearchEdit.TextSubmitted += _ => SubmitNationSearch();
        bar.AddChild(_ncSearchEdit);
        NationButton(bar, "Search", SubmitNationSearch).SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        bar.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore });
        NationButton(bar, "Create room", OpenNationCreate).SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
    }

    private static VBoxContainer NationRowBox(VBoxContainer page, int rows)
    {
        var section = UiTheme.Section();
        page.AddChild(section);
        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, rows * NationChatRowHeight),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        section.AddChild(scroll);
        var box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", 0);
        scroll.AddChild(box);
        return box;
    }

    private static Button NationButton(Container parent, string text, Action pressed)
    {
        var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.None, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        button.AddThemeFontSizeOverride("font_size", 12);
        button.Pressed += pressed;
        parent.AddChild(button);
        return button;
    }

    private void ShowNationTab(bool publicTab)
    {
        _ncOnPublic = publicTab;
        _ncPublicPage.Visible = publicTab;
        _ncPrivatePage.Visible = !publicTab;
        _ncPublicTab.SetPressedNoSignal(publicTab);
        _ncPrivateTab.SetPressedNoSignal(!publicTab);
        _ncSelectedChannel = _ncSelectedRoom = NationChatRules.NoRoom;
        _ncRefresh.TooltipText = publicTab
            ? NcText(NationChatTexts.PublicRefreshTip, "Refreshing public channel info. May be used once every 10 secs.")
            : NcText(NationChatTexts.PrivateRefreshTip, "Refreshing private channel info. May be used once every 10 secs.");
        if (publicTab) Net.I.SendNationChannels();
        else Net.I.SendNationRooms();
        RefreshNationButtons();
        _ncList.ResetSize();
    }

    private void RefreshNationList()
    {
        ulong now = Time.GetTicksMsec();
        ref ulong at = ref (_ncOnPublic ? ref _ncPublicRefreshAt : ref _ncPrivateRefreshAt);
        if (now < at)
        {
            int left = (int)Math.Ceiling((at - now) / 1000.0);
            string wait = string.Format(NcText(NationChatTexts.RefreshWait, "Available after {0} seconds.").Replace("%d", "{0}"), left);
            CombatLogAdd(wait, CombatLogKind.Status, NationChatRefreshWait);
            return;
        }
        at = now + (ulong)(NationChatRules.RefreshCooldownSeconds * 1000);
        if (_ncOnPublic) Net.I.SendNationChannels();
        else Net.I.SendNationRooms();
    }

    private void RefreshNationButtons()
    {
        if (!IsInstanceValid(_ncList)) return;
        var state = Net.I.NationChat;
        _ncPubJoin.Visible = !state.InPublic;
        _ncPubLeave.Visible = state.InPublic;
        _ncPubOpen.Visible = state.InPublic;
    }

    private void OnNationChannels(List<NationChannel> channels)
    {
        _ncChannels = channels;
        foreach (var child in _ncPublicRows.GetChildren()) child.QueueFree();
        foreach (var channel in channels)
        {
            bool mine = Net.I.NationChat.InPublic && Net.I.NationChat.RoomId == channel.Id;
            int id = channel.Id;
            _ncPublicRows.AddChild(NationRow(NationChatRules.Row(channel.Name, channel.Members, NationChatRules.PublicCapacity),
                mine, id == _ncSelectedChannel, () => SelectNationChannel(id), JoinNationChannel));
        }
    }

    private void OnNationRooms(List<NationRoom> rooms)
    {
        _ncRooms = rooms;
        foreach (var child in _ncPrivateRows.GetChildren()) child.QueueFree();
        foreach (var room in rooms)
        {
            bool mine = Net.I.NationChat.InPrivate && Net.I.NationChat.RoomId == room.Id;
            int id = room.Id;
            _ncPrivateRows.AddChild(NationRow(NationChatRules.Row(room.Name, room.Members, room.MaxMembers),
                mine, id == _ncSelectedRoom, () => SelectNationRoom(id), JoinNationRoom));
        }
    }

    private void OnNationSearch(List<NationRoom> rooms)
    {
        ShowNationTabQuietly(publicTab: false);
        OnNationRooms(rooms);
    }

    private void ShowNationTabQuietly(bool publicTab)
    {
        _ncOnPublic = publicTab;
        _ncPublicPage.Visible = publicTab;
        _ncPrivatePage.Visible = !publicTab;
        _ncPublicTab.SetPressedNoSignal(publicTab);
        _ncPrivateTab.SetPressedNoSignal(!publicTab);
    }

    private Control NationRow(string text, bool mine, bool selected, Action select, Action join)
    {
        var row = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, NationChatRowHeight),
            MouseFilter = Control.MouseFilterEnum.Stop,
            TooltipText = NationChatRowTip,
        };
        var box = UiTheme.Row(selected: true);
        if (!selected) { box.BgColor = Colors.Transparent; box.BorderColor = Colors.Transparent; }
        row.AddThemeStyleboxOverride("panel", box);
        var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        line.AddThemeConstantOverride("separation", 4);
        row.AddChild(line);
        var label = UiTheme.Text(text, 12, mine ? NationChatMine : selected ? UiTheme.GoldBright : UiTheme.TextHi);
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        label.VerticalAlignment = VerticalAlignment.Center;
        line.AddChild(label);
        row.GuiInput += ev =>
        {
            if (ev is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click) return;
            select();
            if (click.DoubleClick) join();
        };
        return row;
    }

    private void SelectNationChannel(int id)
    {
        _ncSelectedChannel = id;
        OnNationChannels(_ncChannels);
    }

    private void SelectNationRoom(int id)
    {
        _ncSelectedRoom = id;
        OnNationRooms(_ncRooms);
    }

    private void JoinNationChannel()
    {
        if (_ncSelectedChannel == NationChatRules.NoRoom) { NationNotice(NationChatTexts.SelectRoom, "Select a chatroom."); return; }
        var state = Net.I.NationChat;
        if (state.InPublic && state.RoomId == _ncSelectedChannel) { ShowChatRoom(); return; }
        if (state.InRoom) { NationNotice(NationChatTexts.AlreadyInRoom, "You already are part of another chatroom."); return; }
        var channel = _ncChannels.Find(c => c.Id == _ncSelectedChannel);
        Net.I.NameNextRoom(channel.Name, false, channel.Members, NationChatRules.PublicCapacity);
        Net.I.SendNationPublicEnter(_ncSelectedChannel);
    }

    private void JoinNationRoom()
    {
        if (_ncSelectedRoom == NationChatRules.NoRoom) { NationNotice(NationChatTexts.SelectRoom, "Select a chatroom."); return; }
        var state = Net.I.NationChat;
        if (state.InPrivate && state.RoomId == _ncSelectedRoom) { ShowChatRoom(); return; }
        if (state.InRoom) { NationNotice(NationChatTexts.AlreadyInRoom, "You already are part of another chatroom."); return; }
        var room = _ncRooms.Find(r => r.Id == _ncSelectedRoom);
        Net.I.NameNextRoom(room.Name, room.Locked, room.Members, room.MaxMembers);
        if (room.Locked)
        {
            _ncPasswordEdit.Text = "";
            _ncPassword.Visible = true;
            _ncPasswordEdit.GrabFocus();
            return;
        }
        Net.I.SendNationPrivateEnter(room.Id, "");
    }

    private void NationNotice(int textId, string fallback) =>
        CombatLogAdd(NcText(textId, fallback), CombatLogKind.Status, UiTheme.GoldBright);

    private void BuildNationCreate()
    {
        _ncCreate = new HudWindow("nation_chat_create", "Create room", bodyMinWidth: NationChatSmallWidth) { Visible = false };
        _ncCreate.Closed += () => _ncCreate.Visible = false;
        _ncLayer.AddChild(_ncCreate);
        var col = _ncCreate.Body;
        col.AddThemeConstantOverride("separation", 6);
        col.AddChild(UiTheme.Text("Room name", 12, UiTheme.TextLo));
        _ncCreateTitle = new LineEdit { MaxLength = NationChatRules.TitleMaxLength };
        col.AddChild(_ncCreateTitle);
        _ncCreatePasswordTitle = UiTheme.Text("Password", 12, UiTheme.TextLo);
        col.AddChild(_ncCreatePasswordTitle);
        _ncCreatePassword = new LineEdit { MaxLength = NationChatRules.PasswordMaxLength, Secret = true };
        col.AddChild(_ncCreatePassword);
        col.AddChild(UiTheme.Text("Options", 12, UiTheme.TextLo));
        _ncCreateOption = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        _ncCreateOption.AddItem(NcText(NationChatTexts.Public, "Public"));
        _ncCreateOption.AddItem(NcText(NationChatTexts.Private, "Private"));
        _ncCreateOption.ItemSelected += _ => ApplyNationCreateOption();
        col.AddChild(_ncCreateOption);
        col.AddChild(UiTheme.Text("Max admissions", 12, UiTheme.TextLo));
        _ncCreateSize = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        foreach (int size in NationChatRules.RoomSizes) _ncCreateSize.AddItem(size.ToString());
        col.AddChild(_ncCreateSize);
        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 8);
        col.AddChild(buttons);
        NationButton(buttons, "Confirm", SubmitNationRoom);
        NationButton(buttons, "Cancel", () => _ncCreate.Visible = false);
    }

    private void ApplyNationCreateOption()
    {
        bool isPrivate = _ncCreateOption.Selected == 1;
        _ncCreatePasswordTitle.AddThemeColorOverride("font_color", isPrivate ? UiTheme.GoldBright : UiTheme.TextLo);
        _ncCreatePassword.Editable = isPrivate;
        if (!isPrivate) _ncCreatePassword.Text = "";
    }

    private void OpenNationCreate()
    {
        if (RefusedInSiegeWar()) return;
        if (Net.I.NationChat.InRoom) { NationNotice(NationChatTexts.CreateWhileInRoom, "You may not create another chatroom when already in one."); return; }
        _ncCreateTitle.Text = "";
        _ncCreatePassword.Text = "";
        _ncCreateOption.Selected = 0;
        _ncCreateSize.Selected = 0;
        ApplyNationCreateOption();
        _ncCreate.Visible = true;
        _ncCreateTitle.GrabFocus();
    }

    private void SubmitNationRoom()
    {
        string title = _ncCreateTitle.Text.Trim();
        bool isPrivate = _ncCreateOption.Selected == 1;
        string password = _ncCreatePassword.Text;
        string refusal = NationChatRules.Validate(title, isPrivate, password, NcText);
        if (refusal.Length > 0)
        {
            CombatLogAdd(refusal, CombatLogKind.Status, UiTheme.GoldBright);
            return;
        }
        int size = NationChatRules.RoomSizes[Math.Max(0, _ncCreateSize.Selected)];
        Net.I.SendNationCreate(title, isPrivate, password, size);
    }

    private void BuildNationPassword()
    {
        _ncPassword = new HudWindow("nation_chat_password", "Password", bodyMinWidth: NationChatSmallWidth) { Visible = false };
        _ncPassword.Closed += () => _ncPassword.Visible = false;
        _ncLayer.AddChild(_ncPassword);
        _ncPassword.Body.AddThemeConstantOverride("separation", 6);
        _ncPassword.Body.AddChild(UiTheme.Text("Enter the password.", 12, UiTheme.TextLo));
        _ncPasswordEdit = new LineEdit { MaxLength = NationChatRules.PasswordMaxLength, Secret = true };
        _ncPasswordEdit.TextSubmitted += _ => SubmitNationPassword();
        _ncPassword.Body.AddChild(_ncPasswordEdit);
        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 8);
        _ncPassword.Body.AddChild(buttons);
        NationButton(buttons, "Confirm", SubmitNationPassword);
        NationButton(buttons, "Cancel", () => _ncPassword.Visible = false);
    }

    private void SubmitNationPassword()
    {
        if (_ncPasswordEdit.Text.Length == 0 || _ncSelectedRoom == NationChatRules.NoRoom) return;
        Net.I.SendNationPrivateEnter(_ncSelectedRoom, _ncPasswordEdit.Text);
        _ncPassword.Visible = false;
    }

    private void SubmitNationSearch()
    {
        string word = _ncSearchEdit.Text.Trim();
        if (word.Length == 0) Net.I.SendNationRooms();
        else Net.I.SendNationSearch(word);
    }

    private void OnNationMembers(NationRoomMembers list) => _ncMemberList = list;

    private void OnNationChatResult(byte sub, byte code)
    {
        int text = sub switch
        {
            NationChatReplies.PublicEnter => NationChatRules.PublicEnterText(code),
            NationChatReplies.PublicLeave or NationChatReplies.PrivateLeave => code == 0 ? NationChatTexts.YouLeft : 0,
            NationChatReplies.Create => NationChatRules.CreateText(code),
            NationChatReplies.PrivateEnter => NationChatRules.PrivateEnterText(code),
            NationChatReplies.Search => NationChatRules.SearchText(code),
            NationChatReplies.Options => NationChatRules.OptionsText(code),
            NationChatReplies.HandOver => NationChatRules.HandOverText(code),
            NationChatReplies.Kick => NationChatRules.KickText(code),
            NationChatReplies.Members => code == NationChatRules.MembersNoRoomCode ? NationChatTexts.NoRoom : 0,
            _ => 0,
        };
        if (text != 0) NationNotice(text, "");

        bool created = sub == NationChatReplies.Create && code == NationChatReplies.Created;
        if (created) _ncCreate.Visible = false;
        if (sub == NationChatReplies.Options) ChatRoomConfigReplied(code, text);

        if (!_nationChatShown) return;
        if (sub is NationChatReplies.PublicEnter or NationChatReplies.PublicLeave && code == 0) Net.I.SendNationChannels();
        if (sub is NationChatReplies.PrivateEnter or NationChatReplies.PrivateLeave or NationChatReplies.Options
                or NationChatReplies.Kick && (code == 0 || code == NationChatRules.KickedCode)
            || created)
            Net.I.SendNationRooms();
    }

    private void OnRoomChatNotice(byte kind, int members, string name)
    {
        string line = kind switch
        {
            Net.RoomChatEntered => NcText(NationChatTexts.Entered, "%s has entered. (Current players: %d)"),
            Net.RoomChatLeft => NcText(NationChatTexts.Left, "%s has left. (Current players: %d)"),
            Net.RoomChatNewOwner => NcText(NationChatTexts.NewOwner, "%s has become the room owner."),
            _ => NcText(NationChatTexts.Kicked, "%s has been kicked."),
        };
        ChatRoomNotice(line.Replace("%s", name).Replace("%d", members.ToString()));
        RefreshChatRoomHeader();
        if (Net.I.Voice != null || (Net.I.NationChat.InPrivate && ChatRoomShown)) Net.I.SendNationMembers();
    }
}
