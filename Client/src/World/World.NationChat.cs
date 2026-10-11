using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const string NationChatIconDir = "res://assets/ui/nation_chat/";
    private const float NationChatRowHeight = 22f;
    private const float NationChatIconSize = 18f;
    private const int NationChatListWidth = 440;
    private const int NationChatGridColumns = 3;
    private const int NationChatGridGap = 6;
    private const int NationChatSmallWidth = 250;
    private const int NationMembersWidth = 220;
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
    private Button _ncPriJoin = null!;
    private Button _ncPriLeave = null!;
    private Button _ncPriChange = null!;
    private Button _ncPriPersons = null!;
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
    private Button _ncCreateOk = null!;
    private Button _ncCreateEdit = null!;

    private HudWindow _ncPassword = null!;
    private LineEdit _ncPasswordEdit = null!;
    private HudWindow _ncSearch = null!;
    private LineEdit _ncSearchEdit = null!;

    private HudWindow _ncMembers = null!;
    private Label _ncMembersTitle = null!;
    private Label _ncMembersCount = null!;
    private VBoxContainer _ncMemberRows = null!;
    private NationRoomMembers? _ncMemberList;
    private int _ncSelectedMember = NationChatRules.NoRoom;

    private static string NcText(int id, string fallback) => ItemData.Text(id, fallback);

    private void NationChatInit()
    {
        _ncLayer = new CanvasLayer { Layer = 68 };
        AddChild(_ncLayer);
        BuildNationChannelList();
        BuildNationCreate();
        BuildNationPassword();
        BuildNationSearch();
        BuildNationMembers();

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
        _ncSearch.Visible = false;
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
        NationButton(pubButtons, "Cancel", CloseNationChat);

        _ncPrivatePage = new VBoxContainer();
        _ncPrivatePage.AddThemeConstantOverride("separation", 6);
        _ncList.Body.AddChild(_ncPrivatePage);
        _ncPrivateRows = NationRowBox(_ncPrivatePage, NationChatRules.PrivateRows);
        var priButtons = new GridContainer { Columns = NationChatGridColumns };
        priButtons.AddThemeConstantOverride("h_separation", NationChatGridGap);
        priButtons.AddThemeConstantOverride("v_separation", NationChatGridGap);
        _ncPrivatePage.AddChild(priButtons);
        var joinCell = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        priButtons.AddChild(joinCell);
        _ncPriJoin = NationButton(joinCell, "Enter", JoinNationRoom);
        _ncPriLeave = NationButton(joinCell, "Leave", () => Net.I.SendNationPrivateLeave());
        NationButton(priButtons, "Create room", OpenNationCreate);
        NationButton(priButtons, "Search", OpenNationSearch);
        _ncPriChange = NationButton(priButtons, "Change options", OpenNationEdit);
        _ncPriPersons = NationButton(priButtons, "Number of admissions", OpenNationMembers);
        NationButton(priButtons, "Cancel", CloseNationChat);
        float cell = (NationChatListWidth - NationChatGridGap * (NationChatGridColumns - 1)) / (float)NationChatGridColumns;
        foreach (var child in priButtons.GetChildren())
            if (child is Control control) control.CustomMinimumSize = new Vector2(cell, 0);
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
        _ncPriJoin.Visible = !state.InPrivate;
        _ncPriLeave.Visible = state.InPrivate;
        _ncPriChange.Disabled = !(state.InPrivate && state.Owner);
        _ncPriPersons.Disabled = !state.InPrivate;
        if (!state.InPrivate && IsInstanceValid(_ncMembers)) _ncMembers.Visible = false;
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
                mine, id == _ncSelectedChannel, false, null, null, () => SelectNationChannel(id), JoinNationChannel));
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
            string? nationIcon = room.Nation == Nations.Karus ? "karus.png" : room.Nation == Nations.ElMorad ? "elmorad.png" : null;
            _ncPrivateRows.AddChild(NationRow(NationChatRules.Row(room.Name, room.Members, room.MaxMembers),
                mine, id == _ncSelectedRoom, true, room.Locked ? "lock.png" : null, nationIcon,
                () => SelectNationRoom(id), JoinNationRoom));
        }
    }

    private void OnNationSearch(List<NationRoom> rooms)
    {
        _ncSearch.Visible = false;
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

    private Control NationRow(string text, bool mine, bool selected, bool withIcons, string? lockIcon, string? nationIcon, Action select, Action join)
    {
        var row = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, NationChatRowHeight),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        var box = UiTheme.Row(selected: true);
        if (!selected) { box.BgColor = Colors.Transparent; box.BorderColor = Colors.Transparent; }
        row.AddThemeStyleboxOverride("panel", box);
        var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        line.AddThemeConstantOverride("separation", 4);
        row.AddChild(line);
        if (withIcons)
        {
            line.AddChild(NationIcon(lockIcon));
            line.AddChild(NationIcon(nationIcon));
        }
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

    private static Control NationIcon(string? file)
    {
        var rect = new TextureRect
        {
            CustomMinimumSize = new Vector2(NationChatIconSize, NationChatIconSize),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        if (file != null) rect.Texture = GD.Load<Texture2D>(NationChatIconDir + file);
        return rect;
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
        if (Net.I.NationChat.InRoom) { NationNotice(NationChatTexts.AlreadyInRoom, "You already are part of another chatroom."); return; }
        Net.I.SendNationPublicEnter(_ncSelectedChannel);
    }

    private void JoinNationRoom()
    {
        if (_ncSelectedRoom == NationChatRules.NoRoom) { NationNotice(NationChatTexts.SelectRoom, "Select a chatroom."); return; }
        if (Net.I.NationChat.InRoom) { NationNotice(NationChatTexts.AlreadyInRoom, "You already are part of another chatroom."); return; }
        var room = _ncRooms.Find(r => r.Id == _ncSelectedRoom);
        if (room.Nation != Net.I.LastEnter.Nation) { NationNotice(NationChatTexts.NationMismatch, "Nations do not match."); return; }
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
        _ncCreateOk = NationButton(buttons, "Confirm", () => SubmitNationRoom(editing: false));
        _ncCreateEdit = NationButton(buttons, "Edit", () => SubmitNationRoom(editing: true));
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
        _ncCreateOk.Visible = true;
        _ncCreateEdit.Visible = false;
        _ncCreate.Visible = true;
        _ncCreateTitle.GrabFocus();
    }

    private void OpenNationEdit()
    {
        var state = Net.I.NationChat;
        if (!state.InPrivate || !state.Owner) return;
        _ncCreateTitle.Text = state.RoomName;
        _ncCreateOption.Selected = state.RoomPrivate ? 1 : 0;
        ApplyNationCreateOption();
        _ncCreatePassword.Text = state.RoomPassword;
        _ncCreateSize.Selected = Math.Max(0, Array.IndexOf(NationChatRules.RoomSizes, state.RoomMax));
        _ncCreateOk.Visible = false;
        _ncCreateEdit.Visible = true;
        _ncCreate.Visible = true;
    }

    private void SubmitNationRoom(bool editing)
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
        if (editing) Net.I.SendNationOptions(title, isPrivate, password, size);
        else Net.I.SendNationCreate(title, isPrivate, password, size);
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

    private void BuildNationSearch()
    {
        _ncSearch = new HudWindow("nation_chat_search", "Search", bodyMinWidth: NationChatSmallWidth) { Visible = false };
        _ncSearch.Closed += () => _ncSearch.Visible = false;
        _ncLayer.AddChild(_ncSearch);
        _ncSearch.Body.AddThemeConstantOverride("separation", 6);
        _ncSearch.Body.AddChild(UiTheme.Text("Enter the search word.", 12, UiTheme.TextLo));
        _ncSearchEdit = new LineEdit { MaxLength = NationChatRules.TitleMaxLength };
        _ncSearchEdit.TextSubmitted += _ => SubmitNationSearch();
        _ncSearch.Body.AddChild(_ncSearchEdit);
        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 8);
        _ncSearch.Body.AddChild(buttons);
        NationButton(buttons, "Search", SubmitNationSearch);
        NationButton(buttons, "Cancel", () => _ncSearch.Visible = false);
    }

    private void OpenNationSearch()
    {
        _ncSearchEdit.Text = "";
        _ncSearch.Visible = true;
        _ncSearchEdit.GrabFocus();
    }

    private void SubmitNationSearch()
    {
        string word = _ncSearchEdit.Text.Trim();
        if (word.Length == 0) { NationNotice(NationChatTexts.NoTitle, "Room name was not entered."); return; }
        Net.I.SendNationSearch(word);
    }

    private void BuildNationMembers()
    {
        _ncMembers = new HudWindow("nation_chat_members", "Members", bodyMinWidth: NationMembersWidth) { Visible = false };
        _ncMembers.Closed += () => _ncMembers.Visible = false;
        _ncLayer.AddChild(_ncMembers);
        var head = new HBoxContainer();
        _ncMembers.Body.AddChild(head);
        _ncMembersTitle = UiTheme.Text("", 13, UiTheme.GoldBright);
        _ncMembersTitle.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _ncMembersTitle.ClipText = true;
        head.AddChild(_ncMembersTitle);
        _ncMembersCount = UiTheme.Text("", 12, UiTheme.TextLo);
        head.AddChild(_ncMembersCount);
        _ncMemberRows = NationRowBox(_ncMembers.Body, NationChatRules.MemberRows);
        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 8);
        _ncMembers.Body.AddChild(buttons);
        NationButton(buttons, "Change room leader", HandOverNationRoom);
        NationButton(buttons, "Kick out", KickNationMember);
    }

    private void OpenNationMembers()
    {
        if (!Net.I.NationChat.InPrivate) return;
        _ncMembers.Visible = true;
        Net.I.SendNationMembers();
    }

    private void OnNationMembers(NationRoomMembers list)
    {
        _ncMemberList = list;
        _ncMembersTitle.Text = list.Title;
        _ncMembersCount.Text = $"({list.Members.Count}/{list.MaxMembers})";
        if (!list.Members.Any(m => m.CharacterId == _ncSelectedMember)) _ncSelectedMember = NationChatRules.NoRoom;
        foreach (var child in _ncMemberRows.GetChildren()) child.QueueFree();
        foreach (var member in list.Members)
        {
            int id = member.CharacterId;
            bool owner = id == list.OwnerId;
            _ncMemberRows.AddChild(NationRow(owner ? $"{member.Name}  ★" : member.Name, id == _myId, id == _ncSelectedMember,
                false, null, null, () => { _ncSelectedMember = id; OnNationMembers(list); }, () => { }));
        }
    }

    private void HandOverNationRoom()
    {
        if (_ncSelectedMember == NationChatRules.NoRoom || _ncSelectedMember == _myId) return;
        Net.I.SendNationHandOver(_ncSelectedMember);
    }

    private void KickNationMember()
    {
        if (_ncSelectedMember == NationChatRules.NoRoom) return;
        Net.I.SendNationKick(_ncSelectedMember);
    }

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

        bool entered = (sub == NationChatReplies.PublicEnter || sub == NationChatReplies.PrivateEnter) && code == 0;
        bool created = sub == NationChatReplies.Create && code == NationChatReplies.Created;
        bool left = (sub is NationChatReplies.PublicLeave or NationChatReplies.PrivateLeave && code == 0)
            || (sub == NationChatReplies.Kick && code == NationChatRules.KickedCode);
        if (entered || created) Chat?.UseRoomChannel(true);
        if (left) Chat?.UseRoomChannel(false);
        if (created || (sub == NationChatReplies.Options && code == 0)) _ncCreate.Visible = false;
        if (sub == NationChatReplies.Search && code != 0) _ncSearchEdit.Text = "";
        if ((sub == NationChatReplies.HandOver && code == 0) && _ncMembers.Visible) Net.I.SendNationMembers();

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
        CombatLogAdd(line.Replace("%s", name).Replace("%d", members.ToString()), CombatLogKind.Status, UiTheme.GoldBright);
        if (_ncMembers.Visible) Net.I.SendNationMembers();
    }
}
