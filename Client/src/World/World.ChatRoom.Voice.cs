using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const double ChatRoomNameRequestSeconds = 2.0;
    private const float ChatRoomSettingsWidth = 196f;
    private const float ChatRoomPeerWidth = 176f;
    private const float ChatRoomColumnWidth = 136f;
    private const int ChatRoomStackGap = 6;
    private const float ChatRoomDisabledIconAlpha = 0.35f;
    private const float ChatRoomColumnGap = 6f;
    private const float ChatRoomColumnEdgeAlpha = 0.25f;
    private const float ChatRoomColumnFillAlpha = 0.35f;
    private const float ChatRoomVolumeWidth = 96f;
    private const int ChatRoomJoinMargin = 10;
    private const int ChatRoomMenuWhisper = 1;
    private const int ChatRoomMenuVolume = 2;
    private const int ChatRoomMenuPlayer = 3;
    private const float ChatRoomMicMarkSize = 12f;
    private const string ChatRoomOwnerMark = "★";
    private static readonly Color ChatRoomSpeaking = new("7fd98b");
    private static readonly Color ChatRoomIdleDot = new(0.45f, 0.45f, 0.48f, 0.9f);

    private sealed class RosterRow
    {
        public PanelContainer Panel = null!;
        public Label Dot = null!;
        public Label Name = null!;
        public Label Owner = null!;
        public TextureRect Mark = null!;
        public TextureRect Speaker = null!;
        public StyleBoxFlat Style = null!;
        public bool InVoice;
    }

    private VBoxContainer _roomMembersColumn = null!;
    private VBoxContainer _roomRosterRows = null!;
    private Label _roomRosterEmpty = null!;
    private VBoxContainer _roomLeaderBox = null!;
    private Button _roomLeaderHandOver = null!;
    private Button _roomLeaderKick = null!;
    private HBoxContainer _roomVoiceRow = null!;
    private Button _roomMembersToggle = null!;
    private Button _roomConfigButton = null!;
    private PopupPanel? _roomConfig;
    private LineEdit _roomConfigName = null!;
    private OptionButton _roomConfigOption = null!;
    private Label _roomConfigPasswordTitle = null!;
    private LineEdit _roomConfigPassword = null!;
    private OptionButton _roomConfigSize = null!;
    private CheckBox _roomConfigKeep = null!;
    private Label _roomConfigError = null!;
    private Button _roomJoin = null!;
    private Button _roomMic = null!;
    private Button _roomVoiceGear = null!;
    private Button _roomVoiceLeave = null!;
    private Button _roomDeafen = null!;
    private HSlider _roomVolume = null!;
    private PopupMenu? _roomMemberMenu;
    private int _roomMenuTarget;
    private int _roomSelectedMember = NationChatRules.NoRoom;
    private readonly Dictionary<int, RosterRow> _roomRoster = new();
    private double _roomNamesAskedAt = double.NegativeInfinity;
    private PopupPanel? _roomVoiceSettings;
    private OptionButton _roomDeviceList = null!;
    private ProgressBar _roomSettingsLevel = null!;
    private HSlider _roomThreshold = null!;
    private CheckBox _roomPushToTalk = null!;
    private PopupPanel? _roomPeerPopup;
    private Label _roomPeerName = null!;
    private HSlider _roomPeerVolume = null!;
    private CheckBox _roomPeerMute = null!;
    private int _roomPeerId;

    private void BuildChatRoomMembersColumn(HBoxContainer main)
    {
        _roomMembersColumn = new VBoxContainer { CustomMinimumSize = new Vector2(ChatRoomColumnWidth, 0) };
        _roomMembersColumn.AddThemeConstantOverride("separation", ChatRoomStackGap);
        main.AddChild(_roomMembersColumn);

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0, 0, 0, ChatRoomColumnFillAlpha),
            BorderColor = new Color(UiTheme.Edge, ChatRoomColumnEdgeAlpha),
        };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(6);
        style.SetContentMarginAll(6);
        var panel = new PanelContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", style);
        _roomMembersColumn.AddChild(panel);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        UiTheme.ThinScrollbar(scroll.GetVScrollBar());
        UiTheme.ThinScrollbarThumb(scroll.GetVScrollBar(), true);
        panel.AddChild(scroll);
        var listColumn = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        listColumn.AddThemeConstantOverride("separation", 1);
        scroll.AddChild(listColumn);
        _roomRosterEmpty = UiTheme.Text("Loading members...", 11, UiTheme.TextDim, HorizontalAlignment.Center);
        _roomRosterEmpty.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        listColumn.AddChild(_roomRosterEmpty);
        _roomRosterRows = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _roomRosterRows.AddThemeConstantOverride("separation", 1);
        listColumn.AddChild(_roomRosterRows);

        _roomLeaderBox = new VBoxContainer();
        _roomLeaderBox.AddThemeConstantOverride("separation", ChatRoomStackGap);
        _roomMembersColumn.AddChild(_roomLeaderBox);
        _roomLeaderHandOver = UiTheme.SmallButton("Change leader", "");
        _roomLeaderHandOver.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _roomLeaderHandOver.Pressed += () => { if (_roomSelectedMember > 0) Net.I.SendNationHandOver(_roomSelectedMember); };
        _roomLeaderBox.AddChild(_roomLeaderHandOver);
        _roomLeaderKick = UiTheme.SmallButton("Kick out", "");
        _roomLeaderKick.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _roomLeaderKick.Pressed += () => { if (_roomSelectedMember > 0) Net.I.SendNationKick(_roomSelectedMember); };
        _roomLeaderBox.AddChild(_roomLeaderKick);
    }

    private void AlignChatRoomColumns()
    {
        void Match() => _roomLeaderBox.CustomMinimumSize = new Vector2(0, _roomVoiceRow.Size.Y + ChatRoomStackGap + _roomComposer.Size.Y);
        _roomVoiceRow.Resized += Match;
        _roomComposer.Resized += Match;
    }

    private void BuildChatRoomVoiceRow(VBoxContainer right)
    {
        _roomVoiceRow = new HBoxContainer();
        _roomVoiceRow.AddThemeConstantOverride("separation", 6);
        right.AddChild(_roomVoiceRow);

        _roomMembersToggle = UiTheme.IconButton(UiIcons.Get("system/users-three"), "Show or hide the members");
        _roomMembersToggle.Pressed += ToggleChatRoomMembers;
        _roomVoiceRow.AddChild(_roomMembersToggle);
        _roomConfigButton = UiTheme.IconButton(UiIcons.Get("system/sliders"), "Room settings");
        _roomConfigButton.Pressed += OpenChatRoomConfig;
        TintIcon(_roomConfigButton, UiTheme.TextHi);
        _roomConfigButton.AddThemeColorOverride("icon_disabled_color", new Color(UiTheme.TextLo, ChatRoomDisabledIconAlpha));
        _roomConfigButton.AddThemeStyleboxOverride("disabled", _roomConfigButton.GetThemeStylebox("normal"));
        _roomVoiceRow.AddChild(_roomConfigButton);
        _roomVoiceRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore });

        _roomJoin = UiTheme.ActionButton("Join voice", "Talk with everyone in this room");
        _roomJoin.Icon = UiIcons.Get("system/phone");
        _roomJoin.AddThemeConstantOverride("icon_max_width", 14);
        _roomJoin.AddThemeColorOverride("icon_normal_color", UiTheme.Ink);
        _roomJoin.AddThemeColorOverride("icon_hover_color", UiTheme.Ink);
        foreach (string state in new[] { "normal", "hover", "pressed" })
            if (_roomJoin.GetThemeStylebox(state) is StyleBoxFlat box)
                box.ContentMarginLeft = box.ContentMarginRight = ChatRoomJoinMargin;
        _roomJoin.Pressed += () => VoiceChat.I?.Join();
        _roomVoiceRow.AddChild(_roomJoin);

        _roomDeafen = UiTheme.IconButton(UiIcons.Get("system/speaker-high"), "");
        _roomDeafen.Pressed += () => VoiceChat.I?.ToggleDeafen();
        _roomVoiceRow.AddChild(_roomDeafen);
        _roomVolume = new HSlider
        {
            MinValue = 0,
            MaxValue = VoiceLevels.MaxVolume,
            Step = 0.05,
            CustomMinimumSize = new Vector2(ChatRoomVolumeWidth, 0),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            TooltipText = "Voice volume (up to 200%)",
            FocusMode = Control.FocusModeEnum.None,
        };
        _roomVolume.ValueChanged += value =>
            Config.SetVoiceChat((float)value, Config.VoiceThreshold, Config.VoicePushToTalk, Config.VoiceInputDevice);
        _roomVoiceRow.AddChild(_roomVolume);
        _roomMic = UiTheme.IconButton(UiIcons.Get("system/microphone"), "");
        _roomMic.Pressed += () => VoiceChat.I?.ToggleMute();
        _roomVoiceRow.AddChild(_roomMic);
        _roomVoiceGear = UiTheme.IconButton(UiIcons.Get("system/gear"), "Voice settings");
        _roomVoiceGear.Pressed += OpenChatRoomVoiceSettings;
        _roomVoiceRow.AddChild(_roomVoiceGear);
        _roomVoiceLeave = UiTheme.IconButton(UiIcons.Get("system/phone-x"), "Leave voice");
        _roomVoiceLeave.AddThemeColorOverride("icon_normal_color", UiTheme.Bad);
        _roomVoiceLeave.AddThemeColorOverride("icon_hover_color", UiTheme.Bad.Lightened(0.25f));
        _roomVoiceLeave.Pressed += () => VoiceChat.I?.Leave();
        _roomVoiceRow.AddChild(_roomVoiceLeave);
    }

    private void ToggleChatRoomMembers()
    {
        Net.I.ChatRoomMembersShown = !Net.I.ChatRoomMembersShown;
        RefreshChatRoomVoice();
        _roomWindow.ResetSize();
    }

    private void RefreshChatRoomVoice()
    {
        if (!IsInstanceValid(_roomVoiceRow)) return;
        var voice = VoiceChat.I;
        var state = Net.I.NationChat;
        bool live = voice?.InVoice == true;
        _roomVoiceRow.Visible = state.InPrivate;
        _roomMembersColumn.Visible = state.InPrivate && Net.I.ChatRoomMembersShown;
        TintIcon(_roomMembersToggle, Net.I.ChatRoomMembersShown ? UiTheme.TextHi : UiTheme.TextLo);
        _roomConfigButton.Disabled = !state.Owner;
        if (!state.Owner) _roomConfig?.Hide();
        _roomJoin.Visible = !live;
        _roomDeafen.Visible = live;
        _roomVolume.Visible = live;
        _roomMic.Visible = live;
        _roomVoiceGear.Visible = live;
        _roomVoiceLeave.Visible = live;
        if (live && voice != null)
        {
            _roomVolume.SetValueNoSignal(Config.VoiceChatVolume);
            bool micOff = voice.MicMuted || !voice.CanSpeak;
            _roomMic.Icon = UiIcons.Get(micOff ? "system/microphone-slash" : "system/microphone");
            TintIcon(_roomMic, voice.SilencedByLeader ? UiTheme.Warning : micOff ? UiTheme.Bad : UiTheme.Good);
            _roomMic.TooltipText = voice.SilencedByLeader ? "The room leader silenced your microphone"
                : !voice.CanSpeak ? RoomMediaResults.Text(RoomMediaResults.Silenced)
                : voice.MicMuted ? "Unmute microphone"
                : Config.VoicePushToTalk ? $"Mute microphone (push to talk: hold {KeyBinds.Get(KeyAction.VoicePushToTalk).Text})"
                : "Mute microphone";
            _roomDeafen.Icon = UiIcons.Get(voice.Deafened ? "system/speaker-slash" : "system/speaker-high");
            TintIcon(_roomDeafen, voice.Deafened ? UiTheme.Bad : UiTheme.Gold);
            _roomDeafen.TooltipText = voice.Deafened ? "Hear the room again" : "Stop hearing the room";
            _roomVoiceLeave.TooltipText = voice.CurrentTransport switch
            {
                VoiceChat.Transport.Probing => "Leave voice (connecting...)",
                VoiceChat.Transport.Tcp => "Leave voice (using the game connection)",
                _ => "Leave voice",
            };
        }
        SyncChatRoomRoster(voice, live);
    }

    private static void TintIcon(Button button, Color colour)
    {
        button.AddThemeColorOverride("icon_normal_color", colour);
        button.AddThemeColorOverride("icon_hover_color", colour.Lightened(0.25f));
        button.AddThemeColorOverride("icon_pressed_color", colour.Darkened(0.15f));
    }

    private int ChatRoomOwnerId() =>
        Net.I.NationChat.Owner ? _myId
        : _ncMemberList is { } list && list.RoomId == Net.I.NationChat.RoomId ? list.OwnerId
        : 0;

    private List<RosterEntry> ChatRoomRosterEntries(bool live)
    {
        var members = _ncMemberList is { } list && list.RoomId == Net.I.NationChat.RoomId
            ? list.Members.Select(m => (m.CharacterId, m.Name))
            : [];
        var inVoice = new HashSet<int>(Net.I.VoicePeers.Keys.Where(id => id != _myId));
        if (live) inVoice.Add(_myId);
        return ChatRoomRoster.Arrange(members, inVoice, ChatRoomNameOf);
    }

    private void SyncChatRoomRoster(VoiceChat? voice, bool live)
    {
        if (!Net.I.NationChat.InPrivate)
        {
            ClearChatRoomRoster();
            return;
        }
        var entries = ChatRoomRosterEntries(live);
        var wanted = entries.Select(e => e.CharacterId).ToHashSet();
        foreach (int gone in _roomRoster.Keys.Where(id => !wanted.Contains(id)).ToList())
        {
            _roomRoster[gone].Panel.QueueFree();
            _roomRoster.Remove(gone);
        }
        if (!wanted.Contains(_roomSelectedMember)) _roomSelectedMember = NationChatRules.NoRoom;
        int owner = ChatRoomOwnerId();
        bool unknown = false;
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (!_roomRoster.TryGetValue(entry.CharacterId, out var row))
            {
                row = BuildRosterRow(entry.CharacterId);
                _roomRoster[entry.CharacterId] = row;
                _roomRosterRows.AddChild(row.Panel);
            }
            _roomRosterRows.MoveChild(row.Panel, i);
            unknown |= entry.Name.Length == 0;
            row.Name.Text = entry.Name.Length > 0 ? entry.Name : "...";
            row.Owner.Visible = entry.CharacterId == owner;
            row.InVoice = entry.InVoice;
            row.Panel.TooltipText = entry.CharacterId == _myId ? "" : $"{entry.Name}: double-click to whisper, right-click for more";
            if (voice != null) RefreshRosterRow(entry.CharacterId, row, voice);
        }
        _roomRosterEmpty.Visible = entries.Count == 0;
        if (unknown || entries.Count == 0) AskChatRoomNames();
        RefreshChatRoomLeader();
    }

    private void ClearChatRoomRoster()
    {
        foreach (var row in _roomRoster.Values) row.Panel.QueueFree();
        _roomRoster.Clear();
        _roomSelectedMember = NationChatRules.NoRoom;
    }

    private RosterRow BuildRosterRow(int id)
    {
        var style = UiTheme.Row();
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        panel.AddThemeStyleboxOverride("panel", style);
        var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        line.AddThemeConstantOverride("separation", 4);
        panel.AddChild(line);
        var dot = UiTheme.Text("●", 11, ChatRoomIdleDot);
        dot.MouseFilter = Control.MouseFilterEnum.Ignore;
        line.AddChild(dot);
        var name = UiTheme.Text("", 12, new Color(RoomNameColours.Of(id)));
        name.MouseFilter = Control.MouseFilterEnum.Ignore;
        name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        name.ClipText = true;
        name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        line.AddChild(name);
        var owner = UiTheme.Text(ChatRoomOwnerMark, 11, UiTheme.GoldBright);
        owner.MouseFilter = Control.MouseFilterEnum.Ignore;
        owner.TooltipText = "Room leader";
        line.AddChild(owner);
        var speaker = UiIcons.Image("system/speaker-slash", new Vector2(11, 11), UiTheme.Bad);
        speaker.Visible = false;
        speaker.MouseFilter = Control.MouseFilterEnum.Pass;
        line.AddChild(speaker);
        var mark = UiIcons.Image("system/microphone-slash", new Vector2(ChatRoomMicMarkSize, ChatRoomMicMarkSize), UiTheme.Bad);
        mark.Visible = false;
        mark.MouseFilter = Control.MouseFilterEnum.Stop;
        mark.GuiInput += ev =>
        {
            if (ev is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } || !ChatRoomCanSilence(id)) return;
            Net.I.SendVoiceSilence(id, !ChatRoomPeerSilenced(id));
            mark.AcceptEvent();
        };
        line.AddChild(mark);
        panel.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        panel.GuiInput += ev =>
        {
            if (ev is not InputEventMouseButton { Pressed: true } click) return;
            if (click.ButtonIndex == MouseButton.Left)
            {
                SelectChatRoomMember(id);
                if (click.DoubleClick && id != _myId) OpenWhisperWith(ChatRoomNameOf(id));
            }
            else if (click.ButtonIndex == MouseButton.Right && id != _myId)
            {
                SelectChatRoomMember(id);
                OpenChatRoomMemberMenu(id, click.GlobalPosition);
            }
        };
        return new RosterRow { Panel = panel, Dot = dot, Name = name, Owner = owner, Mark = mark, Speaker = speaker, Style = style };
    }

    private void SelectChatRoomMember(int id)
    {
        _roomSelectedMember = id;
        if (VoiceChat.I is { } voice)
            foreach (var (rowId, row) in _roomRoster) RefreshRosterRow(rowId, row, voice);
        RefreshChatRoomLeader();
    }

    private void RefreshRosterRow(int id, RosterRow row, VoiceChat voice)
    {
        bool selected = id == _roomSelectedMember;
        var baseStyle = UiTheme.Row(selected: selected);
        row.Style.BgColor = baseStyle.BgColor;
        row.Dot.Modulate = row.InVoice ? Colors.White : Colors.Transparent;
        if (!row.InVoice)
        {
            row.Style.BorderColor = baseStyle.BorderColor;
            row.Mark.Visible = false;
            row.Speaker.Visible = false;
            return;
        }
        bool self = id == _myId;
        byte flags = self
            ? (byte)((voice.MicMuted ? VoiceFlags.MicMuted : 0) | (voice.Deafened ? VoiceFlags.Deafened : 0)
                | (Net.I.Voice?.SelfFlags ?? 0) & (VoiceFlags.CannotSpeak | VoiceFlags.Silenced))
            : Net.I.VoicePeers.GetValueOrDefault(id);
        bool speaking = self ? voice.SelfSpeaking : voice.IsSpeaking(id);
        var mix = self ? new VoiceChat.PeerMix(1f, false) : voice.MixOf(id);
        bool mutedForMe = mix.Muted || mix.Volume <= 0f;
        bool lit = speaking && !mutedForMe;
        row.Dot.AddThemeColorOverride("font_color", lit ? ChatRoomSpeaking : ChatRoomIdleDot);
        row.Style.BorderColor = lit ? ChatRoomSpeaking : baseStyle.BorderColor;
        bool silenced = (flags & VoiceFlags.Silenced) != 0;
        bool micOff = (flags & VoiceFlags.NoMicrophone) != 0;
        bool deaf = (flags & VoiceFlags.Deafened) != 0;
        bool control = ChatRoomCanSilence(id);
        row.Speaker.Visible = deaf || mutedForMe;
        row.Speaker.TooltipText = deaf ? "Not hearing the room" : "Muted for you";
        row.Mark.Visible = micOff || control;
        row.Mark.Texture = UiIcons.Get(micOff ? "system/microphone-slash" : "system/microphone");
        row.Mark.SelfModulate = silenced ? UiTheme.Warning : micOff ? UiTheme.Bad : UiTheme.TextLo;
        row.Mark.MouseDefaultCursorShape = control ? Control.CursorShape.PointingHand : Control.CursorShape.Arrow;
        string name = ChatRoomNameOf(id);
        row.Mark.TooltipText = control
            ? silenced ? $"Let {name} speak again" : $"Silence {name}'s microphone"
            : silenced ? "Silenced by the room leader" : "Microphone off";
    }

    private void RefreshChatRoomLeader()
    {
        bool leader = Net.I.NationChat.InPrivate && Net.I.NationChat.Owner;
        int target = _roomSelectedMember;
        bool picked = target > 0 && target != _myId && _roomRoster.ContainsKey(target);
        _roomLeaderHandOver.Disabled = !leader || !picked;
        _roomLeaderKick.Disabled = !leader || !picked;
    }

    private bool ChatRoomPeerSilenced(int id) => (Net.I.VoicePeers.GetValueOrDefault(id) & VoiceFlags.Silenced) != 0;

    private bool ChatRoomCanSilence(int id) =>
        Net.I.NationChat.InPrivate && Net.I.NationChat.Owner && id != _myId && _roomRoster.TryGetValue(id, out var row) && row.InVoice;

    private void OpenChatRoomMemberMenu(int id, Vector2 at)
    {
        if (_roomMemberMenu == null)
        {
            _roomMemberMenu = new PopupMenu();
            _roomMemberMenu.IdPressed += pick =>
            {
                int who = _roomMenuTarget;
                switch ((int)pick)
                {
                    case ChatRoomMenuWhisper: OpenWhisperWith(ChatRoomNameOf(who)); break;
                    case ChatRoomMenuVolume: OpenChatRoomPeer(who); break;
                    case ChatRoomMenuPlayer: ShowPlayerMenuByName(ChatRoomNameOf(who), GetViewport().GetMousePosition()); break;
                }
            };
            _roomLayer.AddChild(_roomMemberMenu);
        }
        _roomMenuTarget = id;
        _roomMemberMenu.Clear();
        _roomMemberMenu.AddItem("Whisper", ChatRoomMenuWhisper);
        bool inCall = _roomRoster.TryGetValue(id, out var row) && row.InVoice;
        if (inCall && VoiceChat.I?.InVoice == true) _roomMemberMenu.AddItem("Volume and mute...", ChatRoomMenuVolume);
        _roomMemberMenu.AddItem("Player menu...", ChatRoomMenuPlayer);
        PopupAtCursor(_roomMemberMenu, at);
    }

    private void TickChatRoomVoice(VoiceChat voice)
    {
        foreach (var (id, row) in _roomRoster) RefreshRosterRow(id, row, voice);
        if (_roomVoiceSettings is { Visible: true })
            _roomSettingsLevel.Value = voice.MicLevel;
    }

    private string ChatRoomNameOf(int id)
    {
        if (id == _myId) return Net.I.LastEnter.Name ?? "";
        if (_ncMemberList?.Members.FirstOrDefault(m => m.CharacterId == id) is { Name.Length: > 0 } member) return member.Name;
        var posts = Net.I.RoomTimeline.Posts;
        for (int i = posts.Count - 1; i >= 0; i--)
            if (posts[i].SenderId == id && posts[i].Name.Length > 0) return posts[i].Name;
        return "";
    }

    private void AskChatRoomNames()
    {
        double now = Time.GetTicksMsec() / 1000.0;
        if (!Net.I.NationChat.InPrivate || now - _roomNamesAskedAt < ChatRoomNameRequestSeconds) return;
        _roomNamesAskedAt = now;
        Net.I.SendNationMembers();
    }

    private void OpenChatRoomVoiceSettings()
    {
        FillChatRoomVoiceSettings();
        PopupAtCursor(_roomVoiceSettings!, GetViewport().GetMousePosition());
    }

    private void FillChatRoomVoiceSettings()
    {
        if (_roomVoiceSettings == null) BuildChatRoomVoiceSettings();
        _roomDeviceList.Clear();
        string[] devices = VoiceChat.InputDevices();
        int selected = 0;
        for (int i = 0; i < devices.Length; i++)
        {
            _roomDeviceList.AddItem(devices[i]);
            if (devices[i] == Config.VoiceInputDevice) selected = i;
        }
        if (devices.Length > 0) _roomDeviceList.Select(selected);
        _roomThreshold.SetValueNoSignal(Config.VoiceThreshold);
        _roomPushToTalk.SetPressedNoSignal(Config.VoicePushToTalk);
        _roomPushToTalk.Text = $"Push to talk (hold {KeyBinds.Get(KeyAction.VoicePushToTalk).Text})";
        _roomSettingsLevel.Value = 0;
    }

    private void BuildChatRoomVoiceSettings()
    {
        _roomVoiceSettings = new PopupPanel();
        _roomVoiceSettings.AddThemeStyleboxOverride("panel", ChatRoomPopupStyle());
        _roomLayer.AddChild(_roomVoiceSettings);
        var column = new VBoxContainer { CustomMinimumSize = new Vector2(ChatRoomSettingsWidth, 0) };
        column.AddThemeConstantOverride("separation", 6);
        _roomVoiceSettings.AddChild(column);
        column.AddChild(UiTheme.SectionTitle("Voice settings"));
        column.AddChild(UiTheme.Text("Microphone", 12, UiTheme.TextLo));
        _roomDeviceList = new OptionButton
        {
            FocusMode = Control.FocusModeEnum.None,
            FitToLongestItem = false,
            ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            CustomMinimumSize = new Vector2(ChatRoomSettingsWidth, 0),
        };
        _roomDeviceList.ItemSelected += index => SaveChatRoomVoiceSettings();
        column.AddChild(_roomDeviceList);
        column.AddChild(UiTheme.Text("Microphone threshold", 12, UiTheme.TextLo));
        _roomSettingsLevel = ChatRoomMeter(ChatRoomSpeaking);
        column.AddChild(_roomSettingsLevel);
        _roomThreshold = new HSlider { MinValue = 0, MaxValue = 1, Step = 0.01, FocusMode = Control.FocusModeEnum.None };
        _roomThreshold.ValueChanged += _ => SaveChatRoomVoiceSettings();
        column.AddChild(_roomThreshold);
        var hint = UiTheme.Text("Your voice is sent while the green bar passes the marker. Raise it if noise gets through.", 10, UiTheme.TextDim);
        hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        hint.CustomMinimumSize = new Vector2(ChatRoomSettingsWidth, 0);
        column.AddChild(hint);
        _roomPushToTalk = UiTheme.FlatCheck("Push to talk", Config.VoicePushToTalk);
        _roomPushToTalk.Toggled += _ => SaveChatRoomVoiceSettings();
        column.AddChild(_roomPushToTalk);
    }

    private static StyleBoxFlat ChatRoomPopupStyle()
    {
        var style = UiTheme.WindowPanel(radius: 3, alpha: 1f);
        style.ContentMarginLeft = style.ContentMarginRight = 10;
        style.ContentMarginTop = style.ContentMarginBottom = 8;
        return style;
    }

    private void SaveChatRoomVoiceSettings()
    {
        string device = _roomDeviceList.Selected >= 0 ? _roomDeviceList.GetItemText(_roomDeviceList.Selected) : Config.DefaultAudioDevice;
        Config.SetVoiceChat(Config.VoiceChatVolume, (float)_roomThreshold.Value, _roomPushToTalk.ButtonPressed, device);
        RefreshChatRoomVoice();
    }

    private void OnChatRoomSilence(RoomMediaReplies.SilenceNotice notice)
    {
        string target = notice.Target.Length > 0 ? notice.Target : ChatRoomNameOf(notice.TargetId);
        ChatRoomNotice((notice with { Target = target }).Text);
    }

    private void OpenChatRoomConfig()
    {
        if (!Net.I.NationChat.InPrivate || !Net.I.NationChat.Owner) return;
        FillChatRoomConfig();
        PopupAtCursor(_roomConfig!, GetViewport().GetMousePosition());
    }

    private void FillChatRoomConfig()
    {
        var state = Net.I.NationChat;
        if (_roomConfig == null) BuildChatRoomConfig();
        _roomConfigName.Text = state.RoomName;
        _roomConfigOption.Selected = state.RoomPrivate ? 1 : 0;
        _roomConfigPassword.Text = state.RoomPassword;
        _roomConfigSize.Selected = Math.Max(0, Array.IndexOf(NationChatRules.RoomSizes, state.RoomMax));
        _roomConfigKeep.SetPressedNoSignal(state.Kept);
        _roomConfigError.Visible = false;
        ApplyChatRoomConfigOption();
    }

    private void BuildChatRoomConfig()
    {
        _roomConfig = new PopupPanel();
        _roomConfig.AddThemeStyleboxOverride("panel", ChatRoomPopupStyle());
        _roomLayer.AddChild(_roomConfig);
        var column = new VBoxContainer { CustomMinimumSize = new Vector2(ChatRoomSettingsWidth, 0) };
        column.AddThemeConstantOverride("separation", 6);
        _roomConfig.AddChild(column);
        column.AddChild(UiTheme.SectionTitle("Room settings"));
        column.AddChild(UiTheme.Text("Room name", 12, UiTheme.TextLo));
        _roomConfigName = new LineEdit { MaxLength = NationChatRules.TitleMaxLength };
        _roomConfigName.TextSubmitted += _ => SaveChatRoomConfig();
        column.AddChild(_roomConfigName);
        column.AddChild(UiTheme.Text("Options", 12, UiTheme.TextLo));
        _roomConfigOption = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        _roomConfigOption.AddItem(NcText(NationChatTexts.Public, "Public"));
        _roomConfigOption.AddItem(NcText(NationChatTexts.Private, "Private"));
        _roomConfigOption.ItemSelected += _ => ApplyChatRoomConfigOption();
        column.AddChild(_roomConfigOption);
        _roomConfigPasswordTitle = UiTheme.Text("Password", 12, UiTheme.TextLo);
        column.AddChild(_roomConfigPasswordTitle);
        _roomConfigPassword = new LineEdit { MaxLength = NationChatRules.PasswordMaxLength, Secret = true };
        _roomConfigPassword.TextSubmitted += _ => SaveChatRoomConfig();
        column.AddChild(_roomConfigPassword);
        column.AddChild(UiTheme.Text("Max admissions", 12, UiTheme.TextLo));
        _roomConfigSize = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        foreach (int size in NationChatRules.RoomSizes) _roomConfigSize.AddItem(size.ToString());
        column.AddChild(_roomConfigSize);
        _roomConfigKeep = UiTheme.FlatCheck("Keep the room when everyone leaves", false);
        _roomConfigKeep.TooltipText = "The room stays in the list while empty, so members can come back to it later";
        _roomConfigKeep.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _roomConfigKeep.CustomMinimumSize = new Vector2(ChatRoomSettingsWidth, 0);
        column.AddChild(_roomConfigKeep);
        _roomConfigError = UiTheme.Text("", 11, UiTheme.Bad);
        _roomConfigError.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _roomConfigError.CustomMinimumSize = new Vector2(ChatRoomSettingsWidth, 0);
        _roomConfigError.Visible = false;
        column.AddChild(_roomConfigError);
        var save = UiTheme.ActionButton("Save", "");
        save.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        save.Pressed += SaveChatRoomConfig;
        column.AddChild(save);
    }

    private void ApplyChatRoomConfigOption()
    {
        bool isPrivate = _roomConfigOption.Selected == 1;
        _roomConfigPassword.Editable = isPrivate;
        _roomConfigPasswordTitle.AddThemeColorOverride("font_color", isPrivate ? UiTheme.TextLo : UiTheme.TextDim);
        if (!isPrivate) _roomConfigPassword.Text = "";
    }

    private void SaveChatRoomConfig()
    {
        string title = _roomConfigName.Text.Trim();
        bool isPrivate = _roomConfigOption.Selected == 1;
        string password = _roomConfigPassword.Text;
        string refusal = NationChatRules.Validate(title, isPrivate, password, NcText);
        _roomConfigError.Text = refusal;
        _roomConfigError.Visible = refusal.Length > 0;
        if (refusal.Length > 0) return;
        if (_roomConfigKeep.ButtonPressed != Net.I.NationChat.Kept) Net.I.SendRoomKept(_roomConfigKeep.ButtonPressed);
        Net.I.SendNationOptions(title, isPrivate, password, NationChatRules.RoomSizes[Math.Max(0, _roomConfigSize.Selected)]);
    }

    private void ChatRoomConfigReplied(byte code, int text)
    {
        if (_roomConfig is not { Visible: true }) return;
        if (code == 0)
        {
            _roomConfig.Hide();
            return;
        }
        _roomConfigError.Text = text != 0 ? NcText(text, "") : "";
        _roomConfigError.Visible = _roomConfigError.Text.Length > 0;
    }

    private void OpenChatRoomPeer(int id)
    {
        if (VoiceChat.I is not { } voice) return;
        if (_roomPeerPopup == null) BuildChatRoomPeerPopup();
        _roomPeerId = id;
        var mix = voice.MixOf(id);
        _roomPeerName.Text = ChatRoomNameOf(id);
        _roomPeerName.AddThemeColorOverride("font_color", new Color(RoomNameColours.Of(id)));
        _roomPeerVolume.SetValueNoSignal(mix.Volume);
        _roomPeerMute.SetPressedNoSignal(mix.Muted);
        PopupAtCursor(_roomPeerPopup!, GetViewport().GetMousePosition());
    }

    private void BuildChatRoomPeerPopup()
    {
        _roomPeerPopup = new PopupPanel();
        _roomPeerPopup.AddThemeStyleboxOverride("panel", ChatRoomPopupStyle());
        _roomLayer.AddChild(_roomPeerPopup);
        var column = new VBoxContainer { CustomMinimumSize = new Vector2(ChatRoomPeerWidth, 0) };
        column.AddThemeConstantOverride("separation", 6);
        _roomPeerPopup.AddChild(column);
        _roomPeerName = UiTheme.Text("", 13, UiTheme.TextHi);
        _roomPeerName.AddThemeConstantOverride("font_embolden", 1);
        column.AddChild(_roomPeerName);
        column.AddChild(UiTheme.Text("Volume", 12, UiTheme.TextLo));
        _roomPeerVolume = new HSlider { MinValue = 0, MaxValue = VoiceLevels.MaxVolume, Step = 0.05, FocusMode = Control.FocusModeEnum.None };
        _roomPeerVolume.ValueChanged += value => VoiceChat.I?.SetMix(_roomPeerId, new VoiceChat.PeerMix((float)value, _roomPeerMute.ButtonPressed));
        column.AddChild(_roomPeerVolume);
        _roomPeerMute = UiTheme.FlatCheck("Mute for me", false);
        _roomPeerMute.Toggled += muted => VoiceChat.I?.SetMix(_roomPeerId, new VoiceChat.PeerMix((float)_roomPeerVolume.Value, muted));
        column.AddChild(_roomPeerMute);
    }
}
