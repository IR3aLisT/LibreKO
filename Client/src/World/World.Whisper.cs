using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const int WhisperWrapThreshold = 34;
    private const float WhisperBlinkDim = 0.42f;
    private const float WhisperBlinkStep = 0.32f;
    private const float WhisperBottomSlack = 8f;
    private const int WhisperFontSize = 12;
    private const int WhisperNoticeFontSize = 11;
    private const float WhisperAlphaStep = 0.05f;
    private static int WhisperBubbleWrapWidth => WhisperLook.BoxOf(Config.WhisperSize).Wrap;
    private PopupPanel? _whisperOptions;
    private HSlider _whisperAlphaSlider = null!;
    private readonly Button[] _whisperSizeButtons = new Button[3];
    private static readonly Color WhisperMineText = new("fff0fa");
    private static readonly Color WhisperBlue = new("2f80d4");
    private static readonly Color WhisperBlueEdge = new("5aa6e8");
    private static readonly Color WhisperBlueText = new("eaf3fd");
    private static readonly Vector2 WhisperFirstPos = new(430, 300);
    private static readonly Vector2 WhisperCascade = new(28, 26);

    private sealed class WhisperChat
    {
        public string Name = "";
        public HudWindow Window = null!;
        public VBoxContainer Log = null!;
        public ScrollContainer Scroll = null!;
        public LineEdit Input = null!;
        public Button? Block;
        public bool StickBottom = true;
        public Tween? Blink;
        public readonly ChatLinkDraft Draft = new();
        public string LastText = "";
        public bool StartCollapsed;
    }

    private CanvasLayer _whisperLayer = null!;
    private readonly Dictionary<string, WhisperChat> _whispers = new(StringComparer.OrdinalIgnoreCase);
    private int _whisperOpened;
    private string _whisperTyping = "";

    private void WhisperInit()
    {
        _whisperLayer = new CanvasLayer { Layer = 79 };
        AddChild(_whisperLayer);

        Net.I.ChatEvent += OnWhisperChat;
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
        Chat.WhisperEcho -= OnWhisperEcho;
        Chat.WhisperNotice -= OnWhisperNotice;
        Chat.WhisperOpened -= OnWhisperOpened;
        Config.WhisperLookChanged -= OnWhisperLookChanged;
    }

    private static StyleBoxFlat WhisperButtonStyle(Color fill, Color edge)
    {
        var sb = new StyleBoxFlat { BgColor = fill, BorderColor = edge };
        sb.SetBorderWidthAll(1);
        sb.SetCornerRadiusAll(3);
        sb.ContentMarginLeft = sb.ContentMarginRight = 12;
        sb.ContentMarginTop = sb.ContentMarginBottom = 4;
        return sb;
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

        var offset = at ?? WhisperFirstPos + WhisperCascade * (_whisperOpened++ % 6);

        chat.Window = new HudWindow(
            $"whisper_{name}", name, offset, WhisperLook.BoxOf(Config.WhisperSize).Width,
            persistLayout: false, minimizable: true)
        {
            Visible = true,
        };
        chat.Window.AddThemeStyleboxOverride("panel", WhisperPanelStyle());
        chat.Window.SetHeaderAccent(WhisperBlue, WhisperBlueEdge, WhisperBlueText);
        chat.Window.AddHeaderIcon("system/user", "Player menu",
            () => ShowPlayerMenuByName(name, GetViewport().GetMousePosition()));
        chat.Window.AddHeaderIcon("system/arrange", "Window size and transparency", OpenWhisperOptions);
        chat.Block = chat.Window.AddHeaderIcon("system/lock", "", ToggleWhisperBlock);
        RefreshWhisperBlock(chat);
        chat.Window.Closed += () => CloseWhisper(name);
        chat.Window.MinimizedChanged += isMinimized =>
        {
            if (isMinimized || !_whispers.TryGetValue(name, out var restored)) return;
            StopWhisperBlink(restored);
            restored.StickBottom = true;
            ScrollWhisperToEnd(restored);
            restored.Input.CallDeferred(Control.MethodName.GrabFocus);
        };
        _whisperLayer.AddChild(chat.Window);

        var root = chat.Window.Body;
        root.AddThemeConstantOverride("separation", 6);

        chat.Scroll = new ScrollContainer
        {
            CustomMinimumSize = WhisperLogSize(),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        chat.Scroll.AddThemeStyleboxOverride("panel", UiTheme.Inset());
        root.AddChild(chat.Scroll);

        chat.Log = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        chat.Log.AddThemeConstantOverride("separation", 4);
        chat.Scroll.AddChild(chat.Log);
        chat.Scroll.GetVScrollBar().Changed += () => { if (chat.StickBottom) ScrollWhisperToEnd(chat); };

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        root.AddChild(row);

        chat.Input = new LineEdit
        {
            PlaceholderText = "Message",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MaxLength = 128,
            KeepEditingOnTextSubmit = true,
        };
        chat.Input.TextSubmitted += _ => SendWhisperFrom(name);
        chat.Input.FocusEntered += () => _whisperTyping = name;
        var emoticons = EmoticonPicker.InputButton(WhisperEmoticonButtonSize, WhisperEmoticonInset);
        emoticons.Visible = false;
        emoticons.Pressed += () => OpenWhisperEmoticons(name, emoticons.GlobalPosition);
        chat.Input.AddChild(emoticons);
        chat.Input.FocusEntered += () => emoticons.Visible = true;
        chat.Input.FocusExited += () => emoticons.Visible = false;
        chat.Input.TreeEntered += () =>
        {
            var roomy = (StyleBox)chat.Input.GetThemeStylebox("normal").Duplicate();
            roomy.ContentMarginRight += WhisperEmoticonButtonSize + WhisperEmoticonInset * 2;
            chat.Input.AddThemeStyleboxOverride("normal", roomy);
            var roomyFocus = (StyleBox)chat.Input.GetThemeStylebox("focus").Duplicate();
            roomyFocus.ContentMarginRight += WhisperEmoticonButtonSize + WhisperEmoticonInset * 2;
            chat.Input.AddThemeStyleboxOverride("focus", roomyFocus);
        };
        chat.Input.TextChanged += text =>
        {
            chat.Draft.TextChanged(chat.LastText, text);
            chat.LastText = text;
            LimitWhisperInput(chat);
        };
        row.AddChild(chat.Input);

        var send = new Button { Text = "Send", FocusMode = Control.FocusModeEnum.None };
        send.AddThemeColorOverride("font_color", WhisperBlueText);
        send.AddThemeColorOverride("font_hover_color", Colors.White);
        send.AddThemeColorOverride("font_pressed_color", Colors.White);
        send.AddThemeStyleboxOverride("normal", WhisperButtonStyle(WhisperBlue, WhisperBlueEdge));
        send.AddThemeStyleboxOverride("hover", WhisperButtonStyle(WhisperBlue.Lightened(0.12f), WhisperBlueEdge));
        send.AddThemeStyleboxOverride("pressed", WhisperButtonStyle(WhisperBlue.Darkened(0.14f), WhisperBlueEdge));
        send.Pressed += () => SendWhisperFrom(name);
        row.AddChild(send);

        _whispers[name] = chat;
        foreach (var line in Net.I.WhisperHistory(name))
            chat.Log.AddChild(BuildWhisperRow(name, line.Mine, line.Notice, line.Text));
        ScrollWhisperToEnd(chat);
        if (minimized) chat.Window.SetMinimized(true);
        chat.Window.Modulate = WhisperRestModulate();
        PluginNotifyWhispers();
        return chat;
    }

    private static Vector2 WhisperLogSize()
    {
        var box = WhisperLook.BoxOf(Config.WhisperSize);
        return new Vector2(box.Width, box.Height);
    }

    private static Color WhisperRestModulate() => new(1f, 1f, 1f, Config.WhisperAlpha);

    private void OnWhisperLookChanged()
    {
        foreach (var chat in _whispers.Values)
        {
            var size = WhisperLogSize();
            chat.Window.Body.CustomMinimumSize = new Vector2(size.X, 0);
            chat.Scroll.CustomMinimumSize = size;
            foreach (var child in chat.Log.GetChildren()) child.QueueFree();
            foreach (var line in Net.I.WhisperHistory(chat.Name))
                chat.Log.AddChild(BuildWhisperRow(chat.Name, line.Mine, line.Notice, line.Text));
            chat.Window.ResetSize();
            if (chat.Blink == null) chat.Window.Modulate = WhisperRestModulate();
            chat.StickBottom = true;
            ScrollWhisperToEnd(chat);
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
        chat.StickBottom = true;
        ScrollWhisperToEnd(chat);
        if (chat.Input.IsVisibleInTree()) chat.Input.GrabFocus();
    }

    private void CloseWhisper(string name)
    {
        if (!_whispers.Remove(name, out var chat)) return;
        StopWhisperBlink(chat);
        chat.Window.QueueFree();
        PluginNotifyWhispers();
    }

    private void SendWhisperFrom(string name)
    {
        if (!_whispers.TryGetValue(name, out var chat)) return;
        string message = chat.Input.Text.Trim();
        if (message.Length == 0) return;

        Chat.SendWhisper(name, chat.Draft.ToWire(message));
        chat.Draft.Clear();
        chat.LastText = "";
        chat.Input.Clear();
        LimitWhisperInput(chat);
        if (!chat.Input.HasFocus()) chat.Input.CallDeferred(Control.MethodName.GrabFocus);
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

    private const int WhisperEmoticonButtonSize = 20;
    private const float WhisperEmoticonInset = 4f;
    private EmoticonPicker? _whisperEmoticons;
    private string _whisperEmoticonTarget = "";

    private void OpenWhisperEmoticons(string name, Vector2 at)
    {
        _whisperEmoticonTarget = name;
        if (_whisperEmoticons == null)
        {
            _whisperEmoticons = new EmoticonPicker();
            _whisperLayer.AddChild(_whisperEmoticons);
            _whisperEmoticons.Picked += code =>
            {
                if (!_whispers.TryGetValue(_whisperEmoticonTarget, out var chat)) return;
                if (EmoticonPicker.Insert(chat.Input, code) is { } refusal) AppendWhisper(chat, mine: false, notice: true, refusal);
                chat.Input.CallDeferred(Control.MethodName.GrabFocus);
            };
        }
        _whisperEmoticons.ShowAt(at);
    }

    private void OnWhisperChat(ChatLine line)
    {
        if (line.Type != ChatSystem.WhisperChannel || line.Name.Length == 0) return;
        if (string.Equals(line.Name, Net.I.LastEnter.Name, StringComparison.OrdinalIgnoreCase)) return;

        bool isNew = !_whispers.ContainsKey(line.Name);
        var chat = GetOrCreateWhisper(line.Name, minimized: isNew);
        chat.Window.Visible = true;
        if (WindowStack.RaisesIncoming(FocusedWhisperIndex(), chat.Window.GetIndex())) RaiseWhisper(chat);
        AppendWhisper(chat, mine: false, notice: false, ChatSystem.ReadableText(line));

        if (chat.Window.Minimized) StartWhisperBlink(chat);
    }

    private void OnWhisperEcho(string name, string message)
    {
        var chat = GetOrCreateWhisper(name, minimized: false);
        chat.Window.Visible = true;
        AppendWhisper(chat, mine: true, notice: false, message);
    }

    private void OnWhisperOpened(string name) => OpenWhisperWith(name);

    private void OnWhisperNotice(string name, string text)
    {
        if (name.Length == 0 || !_whispers.TryGetValue(name, out var chat))
        {
            CombatNotice(text);
            return;
        }
        AppendWhisper(chat, mine: false, notice: true, text);
    }

    private void AppendWhisper(WhisperChat chat, bool mine, bool notice, string text)
    {
        Net.I.RecordWhisper(chat.Name, mine, notice, text);
        PluginNotifyWhisperLine(chat.Name, mine, notice, text);
        chat.StickBottom = mine || WhisperAtBottom(chat);

        chat.Log.AddChild(BuildWhisperRow(chat.Name, mine, notice, text));
        while (chat.Log.GetChildCount() > Net.WhisperLogMax)
        {
            var first = chat.Log.GetChild(0);
            chat.Log.RemoveChild(first);
            first.QueueFree();
        }

        ScrollWhisperToEnd(chat);
    }

    private static bool WhisperAtBottom(WhisperChat chat)
    {
        if (!GodotObject.IsInstanceValid(chat.Scroll)) return true;
        var bar = chat.Scroll.GetVScrollBar();
        if (bar.MaxValue <= bar.Page) return true;
        return bar.Value + bar.Page >= bar.MaxValue - WhisperBottomSlack;
    }

    private static void ScrollWhisperToEnd(WhisperChat chat)
    {
        if (!GodotObject.IsInstanceValid(chat.Scroll)) return;
        chat.Scroll.ScrollVertical = (int)chat.Scroll.GetVScrollBar().MaxValue;
    }

    private bool InsertWhisperItemLink(int itemId)
    {
        if (!_whispers.TryGetValue(_whisperTyping, out var chat) || ItemData.Get(itemId) == null) return false;
        if (!GodotObject.IsInstanceValid(chat.Window) || !chat.Window.Visible || chat.Window.Minimized) return false;
        var (text, caret) = chat.Draft.Insert(chat.Input.Text, TextRunes.Utf16Index(chat.Input.Text, chat.Input.CaretColumn), itemId, ItemData.DisplayName(itemId));
        if (TextRunes.Count(text) + chat.Draft.WireExtra > ChatInputText.MaxLength)
        {
            chat.Draft.Clear();
            return true;
        }
        chat.Input.Text = text;
        chat.Input.CaretColumn = TextRunes.Column(text, caret);
        chat.LastText = text;
        LimitWhisperInput(chat);
        chat.Input.GrabFocus();
        return true;
    }

    private static void LimitWhisperInput(WhisperChat chat)
    {
        int limit = ChatInputText.MaxLength - chat.Draft.WireExtra;
        if (chat.Input.MaxLength == limit) return;
        int caret = chat.Input.CaretColumn;
        chat.Input.MaxLength = limit;
        chat.Input.CaretColumn = caret;
    }

    private Control BuildWhisperRow(string partner, bool mine, bool notice, string text)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.Alignment = notice
            ? BoxContainer.AlignmentMode.Center
            : mine ? BoxContainer.AlignmentMode.End : BoxContainer.AlignmentMode.Begin;
        row.AddChild(notice ? WhisperNoticeText(text) : WhisperLineText((mine ? Net.I.LastEnter.Name : partner) ?? "", mine, text));
        return row;
    }

    private static Control WhisperNoticeText(string text)
    {
        var rich = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(WhisperBubbleWrapWidth, 0),
            Text = $"[center][color=#{UiTheme.Warning.ToHtml(false)}]{WhisperLinks.Linkify(text)}[/color][/center]",
        };
        rich.AddThemeFontSizeOverride("normal_font_size", WhisperNoticeFontSize);
        rich.MetaClicked += meta =>
        {
            string link = meta.AsString();
            if (WhisperLinks.IsWebLink(link)) OS.ShellOpen(link);
        };
        return rich;
    }

    private Control WhisperLineText(string speaker, bool mine, string text)
    {
        var colour = mine ? WhisperMineText : UiTheme.TextHi;
        string plain = ChatSystem.PlainText(text);
        bool wraps = speaker.Length + plain.Length > WhisperWrapThreshold;
        var rich = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            MetaUnderlined = false,
            AutowrapMode = wraps ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off,
            CustomMinimumSize = wraps ? new Vector2(WhisperBubbleWrapWidth, 0) : Vector2.Zero,
            Text = $"[color=#{colour.ToHtml(false)}]{BbCode.Esc(speaker)} : [/color]" + ChatSystem.RenderText(text, colour.ToHtml(false)),
        };
        if (mine) rich.HorizontalAlignment = HorizontalAlignment.Right;
        rich.AddThemeFontSizeOverride("normal_font_size", WhisperFontSize);
        rich.MetaClicked += meta =>
        {
            if (ChatSystem.ItemOf(meta) is int itemId and > 0) ShowChatItemTip(itemId);
        };
        rich.MetaHoverEnded += _ => HideItemTooltip();
        return rich;
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
