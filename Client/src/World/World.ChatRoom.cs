using System;
using System.Collections.Generic;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const int ChatRoomWidth = 380;
    private const float ChatRoomLogHeight = 330f;
    private const int ChatRoomFontSize = 13;
    private const int ChatRoomNameFontSize = 12;
    private const int ChatRoomSmallFontSize = 10;
    private const float ChatRoomBubbleMax = 268f;
    private const float ChatRoomBottomSlack = 8f;
    private const double ChatRoomTickSeconds = 0.1;
    private const double ChatRoomHoldSeconds = 0.4;
    private const int ChatRoomEmoticonButtonSize = 20;
    private const float ChatRoomEmoticonInset = 4f;
    private static readonly Vector2 ChatRoomDefaultPos = new(1110, 170);
    private static readonly Color ChatRoomMineBubble = new(0.0f, 0.36f, 0.29f, 0.94f);
    private static readonly Color ChatRoomMineEdge = new(0.16f, 0.52f, 0.43f, 0.9f);
    private static readonly Color ChatRoomTheirBubble = new(0.165f, 0.165f, 0.185f, 0.95f);
    private static readonly Color ChatRoomTheirEdge = new(0.32f, 0.31f, 0.34f, 0.8f);
    private static readonly Color ChatRoomMineText = new("e9f5f1");
    private static readonly Color ChatRoomTime = new(0.72f, 0.74f, 0.74f, 0.78f);
    private static readonly Color ChatRoomRecording = new("e0574a");
    private static readonly Color ChatRoomLoadingTint = new(1f, 1f, 1f, 0.45f);
    private const float ChatRoomNoteWidth = 214f;
    private const float ChatRoomWaveHeight = 26f;
    private const int ChatRoomNoteGap = 8;
    private const float ChatRoomMineRestAlpha = 0.35f;
    private const float ChatRoomTheirRestAlpha = 0.45f;

    private sealed class NoteRow
    {
        public Button Play = null!;
        public NoteDownloadRing Ring = null!;
        public VoiceWaveformView Wave = null!;
        public Label Clock = null!;
        public RoomPost Post = null!;
    }

    private CanvasLayer _roomLayer = null!;
    private HudWindow _roomWindow = null!;
    private Button? _roomChannelsButton;
    private Tween? _roomBlink;
    private RoomPost? _roomNewest;
    private ScrollContainer _roomScroll = null!;
    private VBoxContainer _roomLog = null!;
    private Button _roomOlder = null!;
    private HBoxContainer _roomComposerRow = null!;
    private HBoxContainer _roomComposer = null!;
    private LineEdit _roomInput = null!;
    private Button _roomRecord = null!;
    private Button _roomSend = null!;
    private HBoxContainer _roomRecordStrip = null!;
    private Label _roomRecordClock = null!;
    private ProgressBar _roomRecordLevel = null!;
    private Godot.Timer _roomTick = null!;
    private bool _roomStickBottom = true;
    private bool _roomHoldRecord;
    private double _roomRecordPressedAt;
    private string _roomLastText = "";
    private readonly ChatLinkDraft _roomDraft = new();
    private readonly Dictionary<long, NoteRow> _roomNotes = new();
    private RoomPost? _roomFirstRendered;
    private int _roomRendered;
    private EmoticonPicker? _roomEmoticons;

    private void ChatRoomInit()
    {
        _roomLayer = new CanvasLayer { Layer = 69 };
        _roomLayer.SetMeta(Audio.QuietMeta, true);
        AddChild(_roomLayer);
        BuildChatRoomWindow();
        _roomTick = new Godot.Timer { WaitTime = ChatRoomTickSeconds, ProcessCallback = Godot.Timer.TimerProcessCallback.Idle };
        _roomTick.Timeout += ChatRoomTick;
        _roomLayer.AddChild(_roomTick);

        Net.I.RoomTimelineEvent += RenderChatRoom;
        Net.I.RoomChatEvent += OnChatRoomLine;
        Net.I.RoomPostedEvent += OnChatRoomPosted;
        Net.I.RoomMediaResultEvent += OnRoomMediaResult;
        Net.I.NationChatStateEvent += SyncChatRoom;
        Net.I.NationMembersEvent += OnChatRoomMembers;
        Net.I.VoiceRosterEvent += RefreshChatRoomVoice;
        Net.I.VoiceSilenceEvent += OnChatRoomSilence;
        if (VoiceChat.I is { } voice)
        {
            voice.StateChanged += RefreshChatRoomVoice;
            voice.NoteRecorded += SendRecordedNote;
            voice.NoteNotice += ChatRoomNotice;
        }
        SyncChatRoom();
    }

    private void ChatRoomDispose()
    {
        Net.I.RoomTimelineEvent -= RenderChatRoom;
        Net.I.RoomChatEvent -= OnChatRoomLine;
        Net.I.RoomPostedEvent -= OnChatRoomPosted;
        Net.I.RoomMediaResultEvent -= OnRoomMediaResult;
        Net.I.NationChatStateEvent -= SyncChatRoom;
        Net.I.NationMembersEvent -= OnChatRoomMembers;
        Net.I.VoiceRosterEvent -= RefreshChatRoomVoice;
        Net.I.VoiceSilenceEvent -= OnChatRoomSilence;
        if (VoiceChat.I is { } voice)
        {
            voice.StateChanged -= RefreshChatRoomVoice;
            voice.NoteRecorded -= SendRecordedNote;
            voice.NoteNotice -= ChatRoomNotice;
            if (voice.Recording) voice.StopRecording(send: false);
        }
    }

    private bool ChatRoomShown => IsInstanceValid(_roomWindow) && _roomWindow.Visible;

    private bool ChatRoomExpanded => ChatRoomShown && !_roomWindow.Minimized;

    private void BuildChatRoomWindow()
    {
        _roomWindow = new HudWindow("chat_room", "Chat room", ChatRoomDefaultPos, ChatRoomWidth, minimizable: true) { Visible = false };
        _roomWindow.Closed += HideChatRoom;
        _roomWindow.MinimizedChanged += minimized =>
        {
            RefreshChatRoomHeader();
            if (minimized) return;
            StopChatRoomBlink();
            _roomStickBottom = true;
            ScrollChatRoomToEnd();
        };
        _roomWindow.AddHeaderIcon("system/door", "Leave the room", LeaveChatRoomFromPanel);
        _roomChannelsButton = _roomWindow.AddHeaderIcon("system/chat-circle", "Channel list", OpenChannelListFromRoom);
        _roomLayer.AddChild(_roomWindow);

        var body = _roomWindow.Body;
        body.AddThemeConstantOverride("separation", 6);

        var main = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        main.AddThemeConstantOverride("separation", (int)ChatRoomColumnGap);
        body.AddChild(main);
        BuildChatRoomMembersColumn(main);
        var right = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        right.AddThemeConstantOverride("separation", ChatRoomStackGap);
        main.AddChild(right);
        _roomScroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(ChatRoomWidth, ChatRoomLogHeight),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        _roomScroll.AddThemeStyleboxOverride("panel", UiTheme.Inset());
        UiTheme.ThinScrollbar(_roomScroll.GetVScrollBar());
        UiTheme.ThinScrollbarThumb(_roomScroll.GetVScrollBar(), true);
        right.AddChild(_roomScroll);
        var margin = new MarginContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        UiTheme.Margins(margin, 6, 4, 8, 4);
        _roomScroll.AddChild(margin);
        var content = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 2);
        margin.AddChild(content);
        var olderRow = new CenterContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        content.AddChild(olderRow);
        _roomOlder = UiTheme.SmallButton("Load older messages", "");
        _roomOlder.Visible = false;
        _roomOlder.Pressed += () => Net.I.SendRoomHistory(Net.I.RoomTimeline.OldestId);
        olderRow.AddChild(_roomOlder);
        _roomLog = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _roomLog.AddThemeConstantOverride("separation", 2);
        content.AddChild(_roomLog);
        _roomScroll.GetVScrollBar().Changed += () => { if (_roomStickBottom) ScrollChatRoomToEnd(); };
        _roomScroll.GetVScrollBar().ValueChanged += _ => _roomStickBottom = ChatRoomAtBottom();

        BuildChatRoomVoiceRow(right);
        BuildChatRoomComposer(right);
        AlignChatRoomColumns();
    }

    private void BuildChatRoomComposer(VBoxContainer body)
    {
        var row = _roomComposerRow = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        body.AddChild(row);

        _roomComposer = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddChild(_roomComposer);
        _roomInput = new LineEdit
        {
            PlaceholderText = "Message",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MaxLength = ChatInputText.MaxLength,
            KeepEditingOnTextSubmit = true,
        };
        _roomInput.TextSubmitted += _ => SendChatRoomText();
        _roomInput.TextChanged += text =>
        {
            _roomDraft.TextChanged(_roomLastText, text);
            _roomLastText = text;
            LimitChatRoomInput();
        };
        var emoticons = EmoticonPicker.InputButton(ChatRoomEmoticonButtonSize, ChatRoomEmoticonInset);
        emoticons.Pressed += () => OpenChatRoomEmoticons(emoticons.GlobalPosition);
        _roomInput.AddChild(emoticons);
        _roomInput.TreeEntered += () =>
        {
            foreach (string state in new[] { "normal", "focus" })
            {
                var roomy = (StyleBox)_roomInput.GetThemeStylebox(state).Duplicate();
                roomy.ContentMarginRight += ChatRoomEmoticonButtonSize + ChatRoomEmoticonInset * 2;
                _roomInput.AddThemeStyleboxOverride(state, roomy);
            }
        };
        _roomComposer.AddChild(_roomInput);

        _roomRecordStrip = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Visible = false };
        _roomRecordStrip.AddThemeConstantOverride("separation", 6);
        row.AddChild(_roomRecordStrip);
        _roomRecordStrip.AddChild(UiTheme.Text("●", 14, ChatRoomRecording));
        _roomRecordClock = UiTheme.Text("0:00", 13, UiTheme.TextHi);
        _roomRecordStrip.AddChild(_roomRecordClock);
        _roomRecordLevel = ChatRoomMeter(ChatRoomRecording);
        _roomRecordStrip.AddChild(_roomRecordLevel);
        var cancel = UiTheme.IconButton(UiIcons.Get("system/close"), "Cancel the voice note");
        cancel.Pressed += () => VoiceChat.I?.StopRecording(send: false);
        _roomRecordStrip.AddChild(cancel);

        _roomRecord = UiTheme.IconButton(UiIcons.Get("system/waveform"), "Hold to record a voice note, or click to start and click again to send");
        _roomRecord.ButtonDown += OnChatRoomRecordDown;
        _roomRecord.ButtonUp += OnChatRoomRecordUp;
        row.AddChild(_roomRecord);

        _roomSend = UiTheme.IconButton(UiIcons.Get("system/send"), "Send");
        _roomSend.Pressed += SendChatRoomText;
        row.AddChild(_roomSend);
    }

    private static ProgressBar ChatRoomMeter(Color tint)
    {
        var bar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            Step = 0.01,
            ShowPercentage = false,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(0, 6),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        bar.AddThemeStyleboxOverride("background", UiTheme.MeterTrack());
        bar.AddThemeStyleboxOverride("fill", UiTheme.MeterFill(tint));
        return bar;
    }

    private void SyncChatRoom()
    {
        if (!IsInstanceValid(_roomWindow)) return;
        var state = Net.I.NationChat;
        if (!state.InRoom)
        {
            if (_roomWindow.Visible) _roomWindow.Visible = false;
            _roomTick.Stop();
            ClearChatRoomLog();
            _roomNewest = null;
            StopChatRoomBlink();
            return;
        }
        if (Net.I.ChatRoomPanelShown && !_roomWindow.Visible) ShowChatRoom();
        RefreshChatRoomHeader();
        RefreshChatRoomVoice();
    }

    internal void ShowChatRoom()
    {
        if (!Net.I.NationChat.InRoom) return;
        Net.I.ChatRoomPanelShown = true;
        _roomWindow.Visible = true;
        _roomWindow.SetMinimized(false);
        _roomLayer.MoveChild(_roomWindow, _roomLayer.GetChildCount() - 1);
        StopChatRoomBlink();
        if (_roomTick.IsInsideTree()) _roomTick.Start();
        RefreshChatRoomHeader();
        RefreshChatRoomVoice();
        RenderChatRoom();
        _roomStickBottom = true;
        ScrollChatRoomToEnd();
        AskChatRoomNames();
    }

    private void HideChatRoom()
    {
        Net.I.ChatRoomPanelShown = false;
        StopChatRoomBlink();
        _roomWindow.Visible = false;
        _roomTick.Stop();
        if (VoiceChat.I is { Recording: true } voice) voice.StopRecording(send: false);
    }

    private void LeaveChatRoomFromPanel()
    {
        if (Net.I.NationChat.InPublic) Net.I.SendNationPublicLeave();
        else if (Net.I.NationChat.InPrivate) Net.I.SendNationPrivateLeave();
    }

    private void OpenChannelListFromRoom()
    {
        if (!_nationChatShown) ToggleNationChat();
    }

    private void RefreshChatRoomHeader()
    {
        if (!IsInstanceValid(_roomWindow)) return;
        var state = Net.I.NationChat;
        string name = state.RoomName.Length > 0 ? state.RoomName : "Chat room";
        _roomWindow.Title = NationChatRules.Title(name, state.RoomMembers, state.RoomMax);
        if (_roomChannelsButton != null) _roomChannelsButton.Visible = !_roomWindow.Minimized;
    }

    private void StartChatRoomBlink()
    {
        if (!ChatRoomShown || !_roomWindow.Minimized || (_roomBlink != null && _roomBlink.IsValid())) return;
        var tween = _roomWindow.CreateTween().SetLoops();
        tween.TweenProperty(_roomWindow, "modulate:a", WhisperBlinkDim, WhisperBlinkStep);
        tween.TweenProperty(_roomWindow, "modulate:a", 1f, WhisperBlinkStep);
        _roomBlink = tween;
    }

    private void StopChatRoomBlink()
    {
        if (_roomBlink != null && _roomBlink.IsValid()) _roomBlink.Kill();
        _roomBlink = null;
        if (IsInstanceValid(_roomWindow)) _roomWindow.Modulate = Colors.White;
    }

    private void ClearChatRoomLog()
    {
        foreach (var child in _roomLog.GetChildren())
        {
            _roomLog.RemoveChild(child);
            child.QueueFree();
        }
        _roomNotes.Clear();
        _roomFirstRendered = null;
        _roomRendered = 0;
    }

    private void OnChatRoomLine(RoomChatLine line) => RenderChatRoom();

    private void OnChatRoomMembers(NationRoomMembers members)
    {
        Net.I.NationChat.Counted(members.Members.Count);
        RefreshChatRoomHeader();
        RefreshChatRoomVoice();
    }

    private void OnChatRoomPosted(RoomPost post) => RenderChatRoom();

    private void ChatRoomNotice(string text)
    {
        if (text.Length == 0 || !Net.I.NationChat.InRoom) return;
        Net.I.RoomTimeline.Add(RoomPost.Notice(text, DateTime.Now));
        RenderChatRoom();
    }

    private void RenderChatRoom()
    {
        if (!IsInstanceValid(_roomWindow)) return;
        var posts = Net.I.RoomTimeline.Posts;
        if (posts.Count == 0 || (_roomFirstRendered != null && !ReferenceEquals(posts[0], _roomFirstRendered)) || _roomRendered > posts.Count)
            ClearChatRoomLog();
        _roomOlder.Visible = Net.I.RoomTimeline.MoreHistory;
        bool fromMe = false;
        for (int i = _roomRendered; i < posts.Count; i++)
        {
            var post = posts[i];
            if (i == 0 || posts[i - 1].Time.Date != post.Time.Date) _roomLog.AddChild(ChatRoomDayChip(post.Time));
            bool mine = post.SenderId != 0 && post.SenderId == _myId;
            fromMe |= mine;
            _roomLog.AddChild(BuildChatRoomPost(post, Net.I.RoomTimeline.StartsGroup(i), mine));
        }
        _roomFirstRendered = posts.Count > 0 ? posts[0] : null;
        _roomRendered = posts.Count;
        if (fromMe) _roomStickBottom = true;
        if (_roomStickBottom) ScrollChatRoomToEnd();
        var newest = posts.Count > 0 ? posts[^1] : null;
        if (newest != null && _roomNewest != null && !ReferenceEquals(newest, _roomNewest)
            && newest.SenderId != _myId && newest.Kind != RoomPostKind.Notice)
            StartChatRoomBlink();
        _roomNewest = newest;
    }

    private static Control ChatRoomDayChip(DateTime time)
    {
        var today = DateTime.Now.Date;
        string label = time.Date == today ? "Today" : time.Date == today.AddDays(-1) ? "Yesterday" : time.ToString("ddd d MMM");
        var row = new CenterContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var pill = UiTheme.Pill(label, UiTheme.TextLo, ChatRoomSmallFontSize);
        pill.CustomMinimumSize = new Vector2(0, 18);
        row.AddChild(pill);
        var spaced = new MarginContainer();
        UiTheme.Margins(spaced, 0, 4, 0, 4);
        spaced.AddChild(row);
        return spaced;
    }

    private Control BuildChatRoomPost(RoomPost post, bool groupStart, bool mine)
    {
        if (post.Kind == RoomPostKind.Notice)
        {
            var center = new CenterContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            var chip = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            chip.AddThemeStyleboxOverride("panel", UiTheme.Chip());
            var notice = UiTheme.Text(post.Text, ChatRoomSmallFontSize + 1, UiTheme.TextLo, HorizontalAlignment.Center);
            float width = _roomLog.GetThemeDefaultFont().GetStringSize(post.Text, HorizontalAlignment.Left, -1, ChatRoomSmallFontSize + 1).X;
            if (width > ChatRoomBubbleMax)
            {
                notice.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                notice.CustomMinimumSize = new Vector2(ChatRoomBubbleMax, 0);
            }
            chip.AddChild(notice);
            var spacedNotice = new MarginContainer();
            UiTheme.Margins(spacedNotice, 0, 3, 0, 3);
            spacedNotice.AddChild(chip);
            center.AddChild(spacedNotice);
            return center;
        }

        var row = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Alignment = mine ? BoxContainer.AlignmentMode.End : BoxContainer.AlignmentMode.Begin,
        };
        var spaced = new MarginContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        UiTheme.Margins(spaced, 0, groupStart ? 4 : 0, 0, 0);
        spaced.AddChild(row);

        var bubble = new PanelContainer();
        bubble.AddThemeStyleboxOverride("panel", ChatRoomBubbleStyle(mine, groupStart));
        row.AddChild(bubble);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 1);
        bubble.AddChild(column);

        if (!mine && groupStart)
        {
            var name = UiTheme.Text(post.Name, ChatRoomNameFontSize, new Color(RoomNameColours.Of(post.SenderId)));
            name.AddThemeConstantOverride("font_embolden", 1);
            name.MouseFilter = Control.MouseFilterEnum.Stop;
            name.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
            string who = post.Name;
            name.GuiInput += ev =>
            {
                if (ev is InputEventMouseButton { Pressed: true } click && click.ButtonIndex is MouseButton.Left or MouseButton.Right)
                    ShowPlayerMenuByName(who, GetViewport().GetMousePosition());
            };
            column.AddChild(name);
        }

        if (post.Kind == RoomPostKind.VoiceNote)
        {
            column.AddChild(BuildChatRoomNote(post, mine));
            return spaced;
        }
        column.AddChild(BuildChatRoomText(post.Text, mine));
        column.AddChild(ChatRoomTimeLabel(post));
        return spaced;
    }

    private static StyleBoxFlat ChatRoomBubbleStyle(bool mine, bool groupStart)
    {
        var sb = new StyleBoxFlat
        {
            BgColor = mine ? ChatRoomMineBubble : ChatRoomTheirBubble,
            BorderColor = mine ? ChatRoomMineEdge : ChatRoomTheirEdge,
        };
        sb.SetBorderWidthAll(1);
        sb.SetCornerRadiusAll(8);
        if (groupStart)
        {
            if (mine) sb.CornerRadiusTopRight = 2;
            else sb.CornerRadiusTopLeft = 2;
        }
        sb.ContentMarginLeft = sb.ContentMarginRight = 9;
        sb.ContentMarginTop = 5;
        sb.ContentMarginBottom = 3;
        return sb;
    }

    private Control BuildChatRoomText(string text, bool mine)
    {
        var colour = mine ? ChatRoomMineText : UiTheme.TextHi;
        string plain = ChatSystem.PlainText(text);
        float width = _roomLog.GetThemeDefaultFont().GetStringSize(plain, HorizontalAlignment.Left, -1, ChatRoomFontSize).X;
        bool wraps = width > ChatRoomBubbleMax;
        var rich = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            MetaUnderlined = false,
            AutowrapMode = wraps ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off,
            CustomMinimumSize = wraps ? new Vector2(ChatRoomBubbleMax, 0) : Vector2.Zero,
            Text = ChatSystem.RenderText(text, colour.ToHtml(false)),
        };
        rich.AddThemeFontSizeOverride("normal_font_size", ChatRoomFontSize);
        rich.MetaClicked += meta =>
        {
            if (ChatSystem.ItemOf(meta) is int itemId and > 0) ShowChatItemTip(itemId);
        };
        rich.MetaHoverEnded += _ => HideItemTooltip();
        return rich;
    }

    private static Label ChatRoomTimeLabel(RoomPost post) =>
        UiTheme.Text(post.Time.ToString("HH:mm"), ChatRoomSmallFontSize, ChatRoomTime, HorizontalAlignment.Right);

    private Control BuildChatRoomNote(RoomPost post, bool mine)
    {
        long id = post.Id;
        var note = new VBoxContainer { CustomMinimumSize = new Vector2(ChatRoomNoteWidth, 0) };
        note.AddThemeConstantOverride("separation", 1);

        var line = new HBoxContainer();
        line.AddThemeConstantOverride("separation", ChatRoomNoteGap);
        note.AddChild(line);
        var play = UiTheme.IconButton(UiIcons.Get("system/play"), "Play the voice note");
        play.Pressed += () => VoiceChat.I?.ToggleNote(id);
        line.AddChild(play);
        var ring = new NoteDownloadRing { Visible = false };
        ring.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        play.AddChild(ring);
        var wave = new VoiceWaveformView
        {
            Samples = post.Waveform,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, ChatRoomWaveHeight),
            Played = mine ? ChatRoomMineText : UiTheme.Gold,
            Rest = mine ? new Color(ChatRoomMineText, ChatRoomMineRestAlpha) : new Color(UiTheme.TextLo, ChatRoomTheirRestAlpha),
        };
        wave.Seeked += fraction => VoiceChat.I?.SeekNote(id, fraction);
        line.AddChild(wave);

        var foot = new HBoxContainer();
        note.AddChild(foot);
        foot.AddChild(new Control { CustomMinimumSize = new Vector2(play.CustomMinimumSize.X + ChatRoomNoteGap, 0) });
        var clock = UiTheme.Text(VoiceNoteRules.Clock(post.Seconds), ChatRoomSmallFontSize, mine ? ChatRoomMineText : UiTheme.TextLo);
        clock.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        foot.AddChild(clock);
        foot.AddChild(ChatRoomTimeLabel(post));

        _roomNotes[id] = new NoteRow { Play = play, Ring = ring, Wave = wave, Clock = clock, Post = post };
        RefreshChatRoomNote(_roomNotes[id]);
        return note;
    }

    private void RefreshChatRoomNote(NoteRow row)
    {
        var voice = VoiceChat.I;
        long id = row.Post.Id;
        var state = voice?.StateOf(id) ?? VoiceChat.NoteState.Idle;
        bool loading = state == VoiceChat.NoteState.Loading;
        row.Play.Icon = UiIcons.Get(state == VoiceChat.NoteState.Playing ? "system/pause" : "system/play");
        row.Play.Disabled = state == VoiceChat.NoteState.Missing;
        row.Play.SelfModulate = loading ? ChatRoomLoadingTint : Colors.White;
        row.Play.TooltipText = state switch
        {
            VoiceChat.NoteState.Loading => "Downloading...",
            VoiceChat.NoteState.Missing => RoomMediaResults.Text(RoomMediaResults.NotFound),
            VoiceChat.NoteState.Playing => "Pause",
            _ => "Play the voice note",
        };
        row.Ring.Visible = loading;
        if (loading) row.Ring.Fraction = voice?.LoadProgress(id) ?? 0f;
        float progress = voice?.NoteProgress(id) ?? 0f;
        row.Wave.Progress = progress;
        row.Clock.Text = state == VoiceChat.NoteState.Playing
            ? VoiceNoteRules.Clock(row.Post.Seconds * progress)
            : VoiceNoteRules.Clock(row.Post.Seconds);
    }

    private void ChatRoomTick()
    {
        if (!ChatRoomShown) return;
        foreach (var row in _roomNotes.Values) RefreshChatRoomNote(row);
        if (VoiceChat.I is { } voice)
        {
            if (voice.Recording)
            {
                _roomRecordClock.Text = $"{VoiceNoteRules.Clock(voice.RecordingSeconds)} / {VoiceNoteRules.Clock(VoiceNoteRules.MaxSeconds)}";
                _roomRecordLevel.Value = voice.MicLevel;
            }
            TickChatRoomVoice(voice);
        }
    }

    private bool ChatRoomAtBottom()
    {
        var bar = _roomScroll.GetVScrollBar();
        if (bar.MaxValue <= bar.Page) return true;
        return bar.Value + bar.Page >= bar.MaxValue - ChatRoomBottomSlack;
    }

    private void ScrollChatRoomToEnd()
    {
        if (!IsInstanceValid(_roomScroll)) return;
        _roomScroll.ScrollVertical = (int)_roomScroll.GetVScrollBar().MaxValue;
    }

    private void SendChatRoomText()
    {
        string message = _roomInput.Text.Trim();
        if (message.Length == 0 || !Net.I.NationChat.InRoom) return;
        if (Chat?.SendRefusal?.Invoke() is { } refusal)
        {
            ChatRoomNotice(refusal);
            return;
        }
        Net.I.SendRoomChat(_roomDraft.ToWire(message));
        _roomDraft.Clear();
        _roomLastText = "";
        _roomInput.Clear();
        LimitChatRoomInput();
        _roomStickBottom = true;
        if (!_roomInput.HasFocus()) _roomInput.CallDeferred(Control.MethodName.GrabFocus);
    }

    private void LimitChatRoomInput()
    {
        int limit = ChatInputText.MaxLength - _roomDraft.WireExtra;
        if (_roomInput.MaxLength == limit) return;
        int caret = _roomInput.CaretColumn;
        _roomInput.MaxLength = limit;
        _roomInput.CaretColumn = caret;
    }

    private bool InsertChatRoomItemLink(int itemId)
    {
        if (!ChatRoomExpanded || !_roomInput.HasFocus() || ItemData.Get(itemId) == null) return false;
        var (text, caret) = _roomDraft.Insert(_roomInput.Text, TextRunes.Utf16Index(_roomInput.Text, _roomInput.CaretColumn), itemId, ItemData.DisplayName(itemId));
        if (TextRunes.Count(text) + _roomDraft.WireExtra > ChatInputText.MaxLength)
        {
            _roomDraft.Clear();
            return true;
        }
        _roomInput.Text = text;
        _roomInput.CaretColumn = TextRunes.Column(text, caret);
        _roomLastText = text;
        LimitChatRoomInput();
        return true;
    }

    private void OpenChatRoomEmoticons(Vector2 at)
    {
        if (_roomEmoticons == null)
        {
            _roomEmoticons = new EmoticonPicker();
            _roomLayer.AddChild(_roomEmoticons);
            _roomEmoticons.Picked += code =>
            {
                if (EmoticonPicker.Insert(_roomInput, code) is { } refusal) ChatRoomNotice(refusal);
                _roomInput.CallDeferred(Control.MethodName.GrabFocus);
            };
        }
        _roomEmoticons.ShowAt(at);
    }

    private void OnChatRoomRecordDown()
    {
        if (VoiceChat.I is not { } voice) return;
        if (voice.Recording)
        {
            _roomHoldRecord = false;
            voice.StopRecording(send: true);
            return;
        }
        if (!voice.StartRecording()) return;
        _roomHoldRecord = true;
        _roomRecordPressedAt = Time.GetTicksMsec() / 1000.0;
    }

    private void OnChatRoomRecordUp()
    {
        if (VoiceChat.I is not { Recording: true } voice || !_roomHoldRecord) return;
        _roomHoldRecord = false;
        if (Time.GetTicksMsec() / 1000.0 - _roomRecordPressedAt >= ChatRoomHoldSeconds) voice.StopRecording(send: true);
    }

    private void SendRecordedNote(byte[] file, double seconds, byte[] waveform)
    {
        if (!Net.I.SendVoiceNote(file, seconds, waveform)) ChatRoomNotice("A voice note is still being sent.");
    }

    private void OnRoomMediaResult(byte sub, byte result)
    {
        if (result == RoomMediaResults.Ok) return;
        if (sub is RoomMediaSubs.NoteBegin or RoomMediaSubs.NoteEnd or RoomMediaSubs.VoiceJoin)
            ChatRoomNotice(RoomMediaResults.Text(result));
    }

    private void RefreshChatRoomRecording(bool recording)
    {
        _roomComposer.Visible = !recording;
        _roomRecordStrip.Visible = recording;
        _roomSend.Visible = !recording;
        _roomRecord.Icon = UiIcons.Get(recording ? "system/send" : "system/waveform");
        _roomRecord.TooltipText = recording ? "Send the voice note" : "Hold to record a voice note, or click to start and click again to send";
        _roomRecord.AddThemeColorOverride("icon_normal_color", recording ? ChatRoomRecording : UiTheme.Gold);
        if (!recording) _roomRecordLevel.Value = 0;
    }
}
