using System;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

internal sealed partial class ChatSystem
{
    private const int CounterFontSize = 11;
    private const float CounterInset = 6f;
    private const float IdleInputAlpha = 0.85f;
    private const string IdleHint = "Press Enter to chat";
    private const string TypingHint = "@name whisper · ! shout · # party · $ clan · & alliance · ↑ last lines";
    private static readonly Color CounterColour = new("a8a298");
    private static readonly Color CounterFullColour = new("e06666");
    private static readonly string[] ReplyPrefixes = { "/r ", "/reply " };

    private Label _counter = null!;
    private StyleBox? _inputRoomy;
    private StyleBox? _inputActive;
    private TextureButton _emoticonButton = null!;
    private StyleBox? _inputBare;
    private readonly ChatInputHistory _history = new();
    private readonly ChatLinkDraft _linkDraft = new();
    private string _lastInputText = "";

    private void BuildInputRow()
    {
        _inputRow = new HBoxContainer();
        _inputRow.AddThemeConstantOverride("separation", 6);
        if (Platform.TouchUi)
        {
            _root.AddChild(_inputRow);
        }
        else
        {
            _logBody.AddChild(_inputRow);
            _inputRow.AnchorLeft = 0;
            _inputRow.AnchorRight = 1;
            _inputRow.AnchorTop = 1;
            _inputRow.AnchorBottom = 1;
            _inputRow.OffsetRight = -HudLogText.ScrollbarGutter;
            _inputRow.GrowVertical = Control.GrowDirection.Begin;
            _inputRow.Resized += ReserveInputRoom;
        }

        _input = new LineEdit
        {
            MaxLength = ChatInputText.MaxLength,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            PlaceholderText = TypingHint,
        };
        _input.TextSubmitted += Submit;
        _input.TextChanged += OnInputChanged;
        _input.GuiInput += OnInputKey;
        _input.FocusExited += () => Callable.From(Suspend).CallDeferred();
        _input.TreeEntered += () =>
        {
            _inputBare = null;
            _inputRoomy = null;
            _inputActive = null;
            ApplyInputStyle();
        };
        _inputRow.AddChild(_input);

        _emoticonButton = EmoticonPicker.InputButton(EmoticonButtonSize, EmoticonInset);
        _emoticonButton.Pressed += OpenEmoticons;
        _input.AddChild(_emoticonButton);

        _counter = UiTheme.Text("", CounterFontSize, CounterColour);
        _counter.Visible = false;
        _counter.MouseFilter = Control.MouseFilterEnum.Ignore;
        _counter.SetAnchorsPreset(Control.LayoutPreset.CenterRight);
        _counter.GrowHorizontal = Control.GrowDirection.Begin;
        _counter.GrowVertical = Control.GrowDirection.Both;
        _counter.OffsetRight = -(CounterInset + EmoticonRoom);
        _input.AddChild(_counter);
    }

    private static bool GameMasterMode => Net.I.GmFxVisible(Net.I.LastEnter.CharId, Net.I.IsGm);

    private const int EmoticonButtonSize = 18;
    private const float EmoticonInset = 4f;
    private const float EmoticonRoom = EmoticonButtonSize + EmoticonInset * 2;
    private EmoticonPicker? _emoticons;

    private bool EmoticonsOpen => _emoticons != null && _emoticons.Visible;

    private void OpenEmoticons()
    {
        if (_emoticons == null)
        {
            _emoticons = new EmoticonPicker();
            _ctx.Root.AddChild(_emoticons);
            _emoticons.Picked += code =>
            {
                if (EmoticonPicker.Insert(_input, code) is { } refusal) Info(refusal);
                Open();
            };
            _emoticons.PopupHide += () => Callable.From(() => { if (!_input.HasFocus()) Suspend(); }).CallDeferred();
        }
        _emoticons.ShowAt(_inputRow.GlobalPosition);
    }

    private void UpdateInputColour()
    {
        byte channel = ChatPrefixes.ChannelFor(_input.Text, _sendChannel);
        _input.AddThemeColorOverride("font_color", _colors.ForInput(channel, GameMasterMode, Net.I.LastEnter.Nation));
    }

    private void OnInputKey(InputEvent ev)
    {
        if (!_active && ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            Open();
            _input.AcceptEvent();
            return;
        }
        if (ev is not InputEventKey { Pressed: true } key) return;
        if (IsPaste(key))
        {
            PasteClipboard();
            _input.AcceptEvent();
            return;
        }
        if (key.CtrlPressed || key.AltPressed || key.MetaPressed) return;
        switch (key.Keycode)
        {
            case Key.Up:
                if (_history.Older(_input.Text) is { } older) ShowTyped(older);
                break;
            case Key.Down:
                if (_history.Newer() is { } newer) ShowTyped(newer);
                break;
            case Key.Pageup:
                ScrollLog(-1);
                break;
            case Key.Pagedown:
                ScrollLog(1);
                break;
            default:
                return;
        }
        _input.AcceptEvent();
    }

    private static bool IsPaste(InputEventKey key) =>
        (key.Keycode == Key.V && (key.CtrlPressed || key.MetaPressed) && !key.AltPressed)
        || (key.Keycode == Key.Insert && key.ShiftPressed);

    private void PasteClipboard()
    {
        string clean = ChatInputText.Paste(DisplayServer.ClipboardGet());
        if (clean.Length == 0) return;
        string text = _input.Text;
        int fromColumn = _input.HasSelection() ? _input.GetSelectionFromColumn() : _input.CaretColumn;
        int toColumn = _input.HasSelection() ? _input.GetSelectionToColumn() : _input.CaretColumn;
        int from = TextRunes.Utf16Index(text, fromColumn);
        int to = TextRunes.Utf16Index(text, toColumn);
        string merged = text[..from] + clean + text[to..];
        if (TextRunes.Count(merged) > _input.MaxLength)
        {
            int room = Math.Max(0, _input.MaxLength - TextRunes.Count(text[..from] + text[to..]));
            clean = TextRunes.Truncate(clean, room);
            merged = text[..from] + clean + text[to..];
        }
        _input.Text = merged;
        _input.CaretColumn = fromColumn + TextRunes.Count(clean);
        OnInputChanged(merged);
    }

    private void ShowTyped(string text)
    {
        _input.Text = text;
        _input.CaretColumn = text.Length;
        OnInputChanged(text);
    }

    private void OnInputChanged(string text)
    {
        if (_lastWhisperFrom.Length > 0 && Array.Exists(ReplyPrefixes, p => string.Equals(text, p, StringComparison.OrdinalIgnoreCase)))
        {
            ShowTyped($"@{_lastWhisperFrom} ");
            return;
        }
        _linkDraft.TextChanged(_lastInputText, text);
        _lastInputText = text;
        ApplyInputLimit();
        UpdateCounter();
        UpdateInputColour();
    }

    private void ApplyInputLimit()
    {
        int limit = ChatInputText.MaxLength - _linkDraft.WireExtra;
        if (_input.MaxLength == limit) return;
        int caret = _input.CaretColumn;
        _input.MaxLength = limit;
        _input.CaretColumn = caret;
    }

    private void UpdateCounter()
    {
        int length = TextRunes.Count(_input.Text) + _linkDraft.WireExtra;
        string? counter = _active ? ChatInputText.Counter(length, ChatInputText.MaxLength) : null;
        if (_counter.Visible != (counter != null))
        {
            _counter.Visible = counter != null;
            ApplyInputStyle();
        }
        if (counter == null) return;
        _counter.Text = counter;
        _counter.AddThemeColorOverride("font_color", length >= ChatInputText.MaxLength ? CounterFullColour : CounterColour);
    }

    private void ApplyInputStyle()
    {
        bool idle = !_active && !Platform.TouchUi;
        _input.PlaceholderText = idle ? IdleHint : TypingHint;
        _input.RemoveThemeStyleboxOverride("normal");
        _input.RemoveThemeStyleboxOverride("read_only");
        var normal = _input.GetThemeStylebox("normal");
        if (idle)
        {
            var bare = _inputBare ?? BareStyle(normal);
            if (_input.IsInsideTree()) _inputBare = bare;
            _input.AddThemeStyleboxOverride("normal", bare);
            _input.AddThemeStyleboxOverride("read_only", bare);
            return;
        }
        if (!_counter.Visible)
        {
            if (_inputActive == null)
            {
                _inputActive = (StyleBox)normal.Duplicate();
                _inputActive.ContentMarginRight = normal.GetMargin(Side.Right) + EmoticonRoom;
            }
            _input.AddThemeStyleboxOverride("normal", _inputActive);
            return;
        }
        if (_inputRoomy == null)
        {
            _inputRoomy = (StyleBox)normal.Duplicate();
            string shown = _counter.Text;
            _counter.Text = ChatInputText.Counter(ChatInputText.MaxLength, ChatInputText.MaxLength);
            _inputRoomy.ContentMarginRight = normal.GetMargin(Side.Right) + _counter.GetCombinedMinimumSize().X + CounterInset + EmoticonRoom;
            _counter.Text = shown;
        }
        _input.AddThemeStyleboxOverride("normal", _inputRoomy);
    }

    private static StyleBox BareStyle(StyleBox normal)
    {
        var bare = new StyleBoxEmpty();
        foreach (Side side in new[] { Side.Left, Side.Top, Side.Right, Side.Bottom })
            bare.SetContentMargin(side, normal.GetMargin(side));
        return bare;
    }

    private void ReserveInputRoom()
    {
        float room = _inputRow.Size.Y + LogBodyGap;
        _scroll.ReserveBottom(room);
        _jumpPill.OffsetBottom = -(room + PillLift);
    }

    internal bool InsertItemLink(int itemId)
    {
        if (!_active || ItemData.Get(itemId) == null) return false;
        var (text, caret) = _linkDraft.Insert(_input.Text, TextRunes.Utf16Index(_input.Text, _input.CaretColumn), itemId, ItemData.DisplayName(itemId));
        if (text == _input.Text) return true;
        if (TextRunes.Count(text) + _linkDraft.WireExtra > ChatInputText.MaxLength)
        {
            _linkDraft.Clear();
            return true;
        }
        ApplyInputLimit();
        _input.Text = text;
        _input.CaretColumn = TextRunes.Column(text, caret);
        _lastInputText = text;
        UpdateCounter();
        _input.GrabFocus();
        return true;
    }
}
