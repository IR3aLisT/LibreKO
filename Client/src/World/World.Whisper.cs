using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const float WhisperBlinkDim = 0.42f;
    private const float WhisperBlinkStep = 0.32f;
    private const float WhisperAlphaStep = 0.05f;
    private PopupPanel? _whisperOptions;
    private HSlider _whisperAlphaSlider = null!;
    private readonly Button[] _whisperSizeButtons = new Button[3];
    private static readonly Color WhisperBlue = new("2f80d4");
    private static readonly Color WhisperBlueEdge = new("5aa6e8");
    private static readonly Color WhisperBlueText = new("eaf3fd");
    private static readonly Vector2 WhisperFirstPos = new(430, 300);
    private static readonly Vector2 WhisperCascade = new(28, 26);

    private sealed class WhisperChat
    {
        public string Name = "";
        public HudWindow Window = null!;
        public ChatThreadView Thread = null!;
        public ChatComposer Composer = null!;
        public readonly List<Button> HeaderIcons = [];
        public Button? Block;
        public Tween? Blink;
        public bool StartCollapsed;

        public LineEdit Input => Composer.Input;
    }

    private CanvasLayer _whisperLayer = null!;
    private readonly Dictionary<string, WhisperChat> _whispers = new(StringComparer.OrdinalIgnoreCase);
    private int _whisperOpened;
    private string _whisperTyping = "";

    private void WhisperInit()
    {
        _whisperLayer = new CanvasLayer { Layer = 79 };
        _whisperLayer.SetMeta(Audio.QuietMeta, true);
        AddChild(_whisperLayer);

        Net.I.ChatEvent += OnWhisperChat;
        Net.I.DirectTimelineEvent += OnDirectTimeline;
        Net.I.DirectPostedEvent += OnDirectPosted;
        Chat.WhisperEcho += OnWhisperEcho;
        Chat.WhisperNotice += OnWhisperNotice;
        Chat.WhisperOpened += OnWhisperOpened;
        Config.WhisperLookChanged += OnWhisperLookChanged;

        foreach (var open in Net.I.OpenWhispers)
        {
            var chat = GetOrCreateWhisper(open.Name, open.Minimized, open.Position);
            if (open.Unread) StartWhisperBlink(chat);
        }
    }

    private void WhisperDispose()
    {
        Net.I.KeepOpenWhispers(_whispers.Values
            .Where(chat => GodotObject.IsInstanceValid(chat.Window) && chat.Window.Visible)
            .Select(chat => new Net.OpenWhisper(chat.Name, chat.Window.Position, chat.Window.Minimized,
                chat.Blink != null && chat.Blink.IsValid())));
        Net.I.ChatEvent -= OnWhisperChat;
        Net.I.DirectTimelineEvent -= OnDirectTimeline;
        Net.I.DirectPostedEvent -= OnDirectPosted;
        Chat.WhisperEcho -= OnWhisperEcho;
        Chat.WhisperNotice -= OnWhisperNotice;
        Chat.WhisperOpened -= OnWhisperOpened;
        Config.WhisperLookChanged -= OnWhisperLookChanged;
        if (VoiceChat.I is { } voice && voice.Recording && voice.RecordingTarget.Length > 0) voice.StopRecording(send: false);
    }

    private static StyleBoxFlat WhisperPanelStyle()
    {
        var sb = new StyleBoxFlat
        {
            BgColor = new Color(0.105f, 0.108f, 0.116f, 0.78f),
            BorderColor = new Color(0.62f, 0.58f, 0.46f, 0.72f),
            ShadowColor = new Color(0, 0, 0, 0.42f),
            ShadowSize = 6,
        };
        sb.SetBorderWidthAll(1);
        sb.SetCornerRadiusAll(2);
        sb.SetContentMarginAll(0);
        return sb;
    }

    private WhisperChat GetOrCreateWhisper(string name, bool minimized, Vector2? at = null)
    {
        if (_whispers.TryGetValue(name, out var existing)) return existing;

        var chat = new WhisperChat { Name = name, StartCollapsed = minimized };
        var box = WhisperLook.BoxOf(Config.WhisperSize);
        var offset = at ?? WhisperFirstPos + WhisperCascade * (_whisperOpened++ % 6);

        chat.Window = new HudWindow($"whisper_{name}", name, offset, box.Width, persistLayout: false, minimizable: true)
        {
            Visible = true,
        };
        chat.Window.AddThemeStyleboxOverride("panel", WhisperPanelStyle());
        chat.Window.SetHeaderAccent(WhisperBlue, WhisperBlueEdge, WhisperBlueText);
        AddWhisperHeaderIcon(chat, "system/user", "Player menu",
            () => ShowPlayerMenuByName(name, GetViewport().GetMousePosition()));
        AddWhisperHeaderIcon(chat, "system/arrange", "Window size and transparency", OpenWhisperOptions);
        chat.Block = AddWhisperHeaderIcon(chat, "system/lock", "", ToggleWhisperBlock);
        RefreshWhisperBlock(chat);
        chat.Window.Closed += () => CloseWhisper(name);
        chat.Window.MinimizedChanged += isMinimized =>
        {
            foreach (var icon in chat.HeaderIcons) icon.Visible = !isMinimized;
            if (isMinimized) return;
            StopWhisperBlink(chat);
            chat.Thread.StickBottom = true;
            chat.Thread.ScrollToEnd();
            chat.Input.CallDeferred(Control.MethodName.GrabFocus);
        };
        _whisperLayer.AddChild(chat.Window);

        var root = chat.Window.Body;
        root.AddThemeConstantOverride("separation", 6);

        var timeline = Net.I.DirectTimeline(name);
        chat.Thread = new ChatThreadView(() => _myId)
        {
            ShowNames = false,
            BubbleMax = box.Wrap,
            Timeline = timeline,
            CustomMinimumSize = WhisperLogSize(),
        };
        chat.Thread.OlderRequested += () => Net.I.SendDirectHistory(name, timeline.OldestId);
        chat.Thread.OthersPosted += () => { if (chat.Window.Minimized) StartWhisperBlink(chat); };
        WireChatThread(chat.Thread);
        root.AddChild(chat.Thread);

        chat.Composer = new ChatComposer(name);
        chat.Composer.Submitted += wire => Chat.SendWhisper(name, wire);
        chat.Composer.Notice += text => AddWhisperPost(chat, RoomPost.Notice(text, DateTime.Now));
        chat.Input.FocusEntered += () => _whisperTyping = name;
        root.AddChild(chat.Composer);

        _whispers[name] = chat;
        chat.Thread.Render();
        chat.Thread.ScrollToEnd();
        if (!timeline.HistoryLoaded) Net.I.SendDirectHistory(name, 0);
        if (minimized) chat.Window.SetMinimized(true);
        chat.Window.Modulate = WhisperRestModulate();
        PluginNotifyWhispers();
        return chat;
    }

    private Button? AddWhisperHeaderIcon(WhisperChat chat, string icon, string tooltip, Action pressed)
    {
        var button = chat.Window.AddHeaderIcon(icon, tooltip, pressed);
        if (button != null) chat.HeaderIcons.Add(button);
        return button;
    }

    private static Vector2 WhisperLogSize()
    {
        var box = WhisperLook.BoxOf(Config.WhisperSize);
        return new Vector2(box.Width, box.Height);
    }

    private static Color WhisperRestModulate() => new(1f, 1f, 1f, Config.WhisperAlpha);

    private void OnWhisperLookChanged()
    {
        var box = WhisperLook.BoxOf(Config.WhisperSize);
        foreach (var chat in _whispers.Values)
        {
            chat.Window.Body.CustomMinimumSize = new Vector2(box.Width, 0);
            chat.Thread.CustomMinimumSize = WhisperLogSize();
            chat.Thread.BubbleMax = box.Wrap;
            chat.Thread.Reset();
            chat.Thread.Render();
            chat.Window.ResetSize();
            if (chat.Blink == null) chat.Window.Modulate = WhisperRestModulate();
            chat.Thread.StickBottom = true;
            chat.Thread.ScrollToEnd();
        }
        RefreshWhisperOptions();
        PluginNotifyWhispers();
    }

    private void OpenWhisperOptions()
    {
        if (_whisperOptions == null) BuildWhisperOptions();
        RefreshWhisperOptions();
        PopupAtCursor(_whisperOptions!, GetViewport().GetMousePosition());
    }

    private void BuildWhisperOptions()
    {
        _whisperOptions = new PopupPanel();
        _whisperLayer.AddChild(_whisperOptions);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 6);
        _whisperOptions.AddChild(column);
        column.AddChild(UiTheme.Text("Window size", 12, UiTheme.TextLo));
        var sizes = new HBoxContainer();
        sizes.AddThemeConstantOverride("separation", 4);
        column.AddChild(sizes);
        var group = new ButtonGroup();
        string[] labels = { "Small", "Medium", "Large" };
        for (int i = 0; i < _whisperSizeButtons.Length; i++)
        {
            var size = (WhisperSize)i;
            var button = new Button { Text = labels[i], ToggleMode = true, ButtonGroup = group, FocusMode = Control.FocusModeEnum.None };
            button.Pressed += () => Config.SetWhisperLook(size, Config.WhisperAlpha);
            _whisperSizeButtons[i] = button;
            sizes.AddChild(button);
        }
        column.AddChild(UiTheme.Text("Transparency", 12, UiTheme.TextLo));
        _whisperAlphaSlider = new HSlider
        {
            MinValue = WhisperLook.MinAlpha, MaxValue = WhisperLook.MaxAlpha, Step = WhisperAlphaStep,
            CustomMinimumSize = new Vector2(180, 18),
        };
        _whisperAlphaSlider.ValueChanged += value => Config.SetWhisperLook(Config.WhisperSize, (float)value);
        column.AddChild(_whisperAlphaSlider);
    }

    private void RefreshWhisperOptions()
    {
        if (_whisperOptions == null) return;
        for (int i = 0; i < _whisperSizeButtons.Length; i++)
            _whisperSizeButtons[i].SetPressedNoSignal((int)Config.WhisperSize == i);
        _whisperAlphaSlider.SetValueNoSignal(Config.WhisperAlpha);
    }

    private void ToggleWhisperBlock()
    {
        Net.I.SendChatBlock(!Net.I.WhispersBlocked);
        foreach (var chat in _whispers.Values) RefreshWhisperBlock(chat);
        PluginNotifyWhispers();
    }

    private const string WhisperLockedIcon = "system/lock";
    private const string WhisperUnlockedIcon = "system/unlock";

    private static void RefreshWhisperBlock(WhisperChat chat)
    {
        if (chat.Block == null) return;
        bool blocked = Net.I.WhispersBlocked;
        var colour = blocked ? UiTheme.Bad : UiTheme.Good;
        chat.Block.Icon = UiIcons.Get(blocked ? WhisperLockedIcon : WhisperUnlockedIcon);
        chat.Block.AddThemeColorOverride("icon_normal_color", colour);
        chat.Block.AddThemeColorOverride("icon_hover_color", colour.Lightened(0.25f));
        chat.Block.AddThemeColorOverride("icon_pressed_color", colour.Darkened(0.15f));
        chat.Block.TooltipText = blocked ? "Private messages are blocked - click to allow them" : "Block private messages";
    }

    private void OpenWhisperWith(string name)
    {
        if (name.Length == 0) return;
        var chat = GetOrCreateWhisper(name, minimized: false);
        chat.Window.Visible = true;
        chat.Window.SetMinimized(false);
        RaiseWhisper(chat);
        StopWhisperBlink(chat);
        chat.Thread.StickBottom = true;
        chat.Thread.ScrollToEnd();
        if (chat.Input.IsVisibleInTree()) chat.Input.GrabFocus();
    }

    private void CloseWhisper(string name)
    {
        if (!_whispers.Remove(name, out var chat)) return;
        StopWhisperBlink(chat);
        if (VoiceChat.I is { } voice && voice.RecordingFor(name)) voice.StopRecording(send: false);
        chat.Window.QueueFree();
        PluginNotifyWhispers();
    }

    private void FocusWhisperAt(Vector2 screenPos)
    {
        var shown = _whispers.Values
            .Where(chat => GodotObject.IsInstanceValid(chat.Window) && chat.Window.Visible)
            .OrderBy(chat => chat.Window.GetIndex())
            .ToList();
        int hit = WindowStack.TopmostAt(shown.Select(chat => chat.Window.GetGlobalRect()).ToList(), screenPos);
        if (hit == WindowStack.None || shown[hit].Window.Minimized) return;
        RaiseWhisper(shown[hit]);
        if (!shown[hit].Input.HasFocus()) shown[hit].Input.CallDeferred(Control.MethodName.GrabFocus);
    }

    private void RaiseWhisper(WhisperChat chat) =>
        _whisperLayer.MoveChild(chat.Window, _whisperLayer.GetChildCount() - 1);

    private int FocusedWhisperIndex()
    {
        if (GetViewport().GuiGetFocusOwner() is not LineEdit focused) return WindowStack.None;
        foreach (var chat in _whispers.Values)
            if (chat.Input == focused && GodotObject.IsInstanceValid(chat.Window)) return chat.Window.GetIndex();
        return WindowStack.None;
    }

    private void OnWhisperChat(ChatLine line)
    {
        if (line.Type != ChatSystem.WhisperChannel || line.Name.Length == 0) return;
        if (string.Equals(line.Name, Net.I.LastEnter.Name, StringComparison.OrdinalIgnoreCase)) return;

        var chat = ShowIncomingWhisper(line.Name);
        AddWhisperPost(chat, RoomPost.Line(line.CharId, line.Name, ChatSystem.ReadableText(line), DateTime.Now));
        if (chat.Window.Minimized) StartWhisperBlink(chat);
    }

    private WhisperChat ShowIncomingWhisper(string name)
    {
        bool isNew = !_whispers.ContainsKey(name);
        var chat = GetOrCreateWhisper(name, minimized: isNew);
        chat.Window.Visible = true;
        if (WindowStack.RaisesIncoming(FocusedWhisperIndex(), chat.Window.GetIndex())) RaiseWhisper(chat);
        return chat;
    }

    private void OnDirectTimeline(string partner)
    {
        if (_whispers.TryGetValue(partner, out var chat)) chat.Thread.Render();
    }

    private void OnDirectPosted(string partner, RoomPost post)
    {
        bool theirs = post.SenderId != _myId;
        if (!_whispers.TryGetValue(partner, out var chat))
        {
            if (!theirs || post.Kind != RoomPostKind.VoiceNote) return;
            chat = ShowIncomingWhisper(partner);
        }
        PluginNotifyWhisperLine(partner, post);
        chat.Thread.Render();
        if (theirs && chat.Window.Minimized) StartWhisperBlink(chat);
    }

    private void OnWhisperEcho(string name, string message)
    {
        var chat = GetOrCreateWhisper(name, minimized: false);
        chat.Window.Visible = true;
        chat.Thread.StickBottom = true;
        AddWhisperPost(chat, RoomPost.Line(_myId, Net.I.LastEnter.Name ?? "", message, DateTime.Now));
    }

    private void OnWhisperOpened(string name) => OpenWhisperWith(name);

    private void OnWhisperNotice(string name, string text) => WhisperNoticeTo(name, text);

    private void WhisperNoticeTo(string name, string text)
    {
        if (name.Length == 0 || !_whispers.TryGetValue(name, out var chat))
        {
            CombatNotice(text);
            return;
        }
        AddWhisperPost(chat, RoomPost.Notice(text, DateTime.Now));
    }

    private void AddWhisperPost(WhisperChat chat, RoomPost post)
    {
        Net.I.RecordWhisper(chat.Name, post);
        PluginNotifyWhisperLine(chat.Name, post);
        chat.Thread.Render();
    }

    private bool InsertWhisperItemLink(int itemId)
    {
        if (!_whispers.TryGetValue(_whisperTyping, out var chat)) return false;
        if (!GodotObject.IsInstanceValid(chat.Window) || !chat.Window.Visible || chat.Window.Minimized) return false;
        return chat.Composer.InsertItemLink(itemId);
    }

    private void StartWhisperBlink(WhisperChat chat)
    {
        if (chat.Blink != null && chat.Blink.IsValid()) return;
        var tween = chat.Window.CreateTween().SetLoops();
        tween.TweenProperty(chat.Window, "modulate:a", WhisperBlinkDim * Config.WhisperAlpha, WhisperBlinkStep);
        tween.TweenProperty(chat.Window, "modulate:a", Config.WhisperAlpha, WhisperBlinkStep);
        chat.Blink = tween;
    }

    private static void StopWhisperBlink(WhisperChat chat)
    {
        if (chat.Blink != null && chat.Blink.IsValid()) chat.Blink.Kill();
        chat.Blink = null;
        chat.Window.Modulate = WhisperRestModulate();
    }

    private bool AnyWhisperExpanded()
    {
        foreach (var chat in _whispers.Values)
            if (IsWhisperExpanded(chat)) return true;
        return false;
    }

    private void MinimizeAllWhispers()
    {
        foreach (var chat in _whispers.Values)
            if (IsWhisperExpanded(chat)) chat.Window.SetMinimized(true);
    }

    private static bool IsWhisperExpanded(WhisperChat chat) =>
        GodotObject.IsInstanceValid(chat.Window) && chat.Window.Visible && !chat.Window.Minimized;

    private bool WhisperInputHasFocus()
    {
        if (GetViewport().GuiGetFocusOwner() is not LineEdit focused) return false;
        foreach (var chat in _whispers.Values)
            if (chat.Input == focused) return true;
        return false;
    }

    private bool TryMinimizeFocusedWhisper()
    {
        var focus = GetViewport().GuiGetFocusOwner();
        for (Node? n = focus; n != null; n = n.GetParent())
        {
            if (n is not HudWindow window) continue;
            foreach (var chat in _whispers.Values)
            {
                if (chat.Window != window || window.Minimized) continue;
                window.SetMinimized(true);
                focus?.ReleaseFocus();
                return true;
            }
            return false;
        }
        return false;
    }
}
