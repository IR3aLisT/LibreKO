using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const int ChatRoomWidth = 380;
    private const float ChatRoomLogHeight = 330f;
    private const float ChatRoomBubbleMax = 268f;
    private const double ChatRoomTickSeconds = 0.1;
    private static readonly Vector2 ChatRoomDefaultPos = new(1110, 170);

    private CanvasLayer _roomLayer = null!;
    private HudWindow _roomWindow = null!;
    private Button? _roomChannelsButton;
    private Tween? _roomBlink;
    private ChatThreadView _roomThread = null!;
    private ChatComposer _roomComposer = null!;
    private Godot.Timer _roomTick = null!;

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
            voice.NoteNotice += ChatNoticeFor;
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
            voice.NoteNotice -= ChatNoticeFor;
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
            _roomThread.StickBottom = true;
            _roomThread.ScrollToEnd();
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
        _roomThread = new ChatThreadView(() => _myId)
        {
            ShowNames = true,
            BubbleMax = ChatRoomBubbleMax,
            Timeline = Net.I.RoomTimeline,
            CustomMinimumSize = new Vector2(ChatRoomWidth, ChatRoomLogHeight),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        _roomThread.OlderRequested += () => Net.I.SendRoomHistory(Net.I.RoomTimeline.OldestId);
        _roomThread.OthersPosted += StartChatRoomBlink;
        WireChatThread(_roomThread);
        right.AddChild(_roomThread);

        BuildChatRoomVoiceRow(right);
        _roomComposer = new ChatComposer("");
        _roomComposer.Submitted += SendChatRoomText;
        _roomComposer.Notice += ChatRoomNotice;
        right.AddChild(_roomComposer);
        AlignChatRoomColumns();
    }

    private static ProgressBar ChatRoomMeter(Color tint) => ChatComposer.Meter(tint);

    private void SyncChatRoom()
    {
        if (!IsInstanceValid(_roomWindow)) return;
        var state = Net.I.NationChat;
        if (!state.InRoom)
        {
            if (_roomWindow.Visible) _roomWindow.Visible = false;
            _roomTick.Stop();
            _roomThread.Reset();
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
        _roomThread.StickBottom = true;
        _roomThread.ScrollToEnd();
        AskChatRoomNames();
    }

    private void HideChatRoom()
    {
        Net.I.ChatRoomPanelShown = false;
        StopChatRoomBlink();
        _roomWindow.Visible = false;
        _roomTick.Stop();
        if (VoiceChat.I is { } voice && voice.RecordingFor("")) voice.StopRecording(send: false);
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
        Net.I.RoomTimeline.Add(RoomPost.Notice(text, System.DateTime.Now));
        RenderChatRoom();
    }

    private void ChatNoticeFor(string target, string text)
    {
        if (target.Length == 0) ChatRoomNotice(text);
        else WhisperNoticeTo(target, text);
    }

    private void RenderChatRoom()
    {
        if (IsInstanceValid(_roomThread)) _roomThread.Render();
    }

    private void ChatRoomTick()
    {
        if (!ChatRoomShown || VoiceChat.I is not { } voice) return;
        TickChatRoomVoice(voice);
    }

    private void SendChatRoomText(string wire)
    {
        if (!Net.I.NationChat.InRoom) return;
        if (Chat?.SendRefusal?.Invoke() is { } refusal)
        {
            ChatRoomNotice(refusal);
            return;
        }
        Net.I.SendRoomChat(wire);
        _roomThread.StickBottom = true;
    }

    private bool InsertChatRoomItemLink(int itemId) =>
        ChatRoomExpanded && _roomComposer.Input.HasFocus() && _roomComposer.InsertItemLink(itemId);

    private void SendRecordedNote(byte[] file, double seconds, byte[] waveform, string target)
    {
        if (!Net.I.SendVoiceNote(file, seconds, waveform, target)) ChatNoticeFor(target, "A voice note is still being sent.");
    }

    private void OnRoomMediaResult(byte sub, byte result)
    {
        if (result == RoomMediaResults.Ok) return;
        if (sub is RoomMediaSubs.NoteBegin or RoomMediaSubs.NoteEnd) ChatNoticeFor(Net.I.NoteTarget, RoomMediaResults.Text(result));
        else if (sub is RoomMediaSubs.VoiceJoin or RoomMediaSubs.Kept) ChatRoomNotice(RoomMediaResults.Text(result));
    }
}
