using System;
using Godot;
using LibreKO.Domain;

namespace LibreKO;

public sealed partial class ChatComposer : HBoxContainer
{
    private const int EmoticonButtonSize = 20;
    private const float EmoticonInset = 4f;
    private const double HoldSeconds = 0.4;
    private const double TickSeconds = 0.1;
    private const int Gap = 6;
    private const string RecordTip = "Hold to record a voice note, or click to start and click again to send";
    private static readonly Color RecordingColour = new("e0574a");

    private readonly string _target;
    private readonly ChatLinkDraft _draft = new();
    private readonly HBoxContainer _field;
    private readonly HBoxContainer _strip;
    private readonly Label _clock;
    private readonly ProgressBar _level;
    private readonly Button _record;
    private readonly Button _send;
    private EmoticonPicker? _emoticons;
    private string _lastText = "";
    private bool _hold;
    private double _pressedAt;
    private double _tick;
    private VoiceChat? _voice;

    public LineEdit Input { get; }

    public event Action<string>? Submitted;
    public event Action<string>? Notice;

    public ChatComposer(string target)
    {
        _target = target;
        AddThemeConstantOverride("separation", Gap);

        _field = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        AddChild(_field);
        Input = new LineEdit
        {
            PlaceholderText = "Message",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MaxLength = ChatInputText.MaxLength,
            KeepEditingOnTextSubmit = true,
        };
        Input.TextSubmitted += _ => Submit();
        Input.TextChanged += text =>
        {
            _draft.TextChanged(_lastText, text);
            _lastText = text;
            Limit();
        };
        var emoticons = EmoticonPicker.InputButton(EmoticonButtonSize, EmoticonInset);
        emoticons.Pressed += () => OpenEmoticons(emoticons.GlobalPosition);
        Input.AddChild(emoticons);
        Input.TreeEntered += () =>
        {
            foreach (string state in new[] { "normal", "focus" })
            {
                var roomy = (StyleBox)Input.GetThemeStylebox(state).Duplicate();
                roomy.ContentMarginRight += EmoticonButtonSize + EmoticonInset * 2;
                Input.AddThemeStyleboxOverride(state, roomy);
            }
        };
        _field.AddChild(Input);

        _strip = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Visible = false };
        _strip.AddThemeConstantOverride("separation", Gap);
        AddChild(_strip);
        _strip.AddChild(UiTheme.Text("●", 14, RecordingColour));
        _clock = UiTheme.Text("0:00", 13, UiTheme.TextHi);
        _strip.AddChild(_clock);
        _level = Meter(RecordingColour);
        _strip.AddChild(_level);
        var cancel = UiTheme.IconButton(UiIcons.Get("system/close"), "Cancel the voice note");
        cancel.Pressed += () => VoiceChat.I?.StopRecording(send: false);
        _strip.AddChild(cancel);

        _record = UiTheme.IconButton(UiIcons.Get("system/waveform"), RecordTip);
        _record.ButtonDown += OnRecordDown;
        _record.ButtonUp += OnRecordUp;
        AddChild(_record);

        _send = UiTheme.IconButton(UiIcons.Get("system/send"), "Send");
        _send.Pressed += Submit;
        AddChild(_send);
    }

    public static ProgressBar Meter(Color tint)
    {
        var bar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            Step = 0.01,
            ShowPercentage = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(0, 6),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        bar.AddThemeStyleboxOverride("background", UiTheme.MeterTrack());
        bar.AddThemeStyleboxOverride("fill", UiTheme.MeterFill(tint));
        return bar;
    }

    public override void _EnterTree()
    {
        _voice = VoiceChat.I;
        if (_voice != null) _voice.StateChanged += RefreshRecording;
        RefreshRecording();
    }

    public override void _ExitTree()
    {
        if (_voice != null) _voice.StateChanged -= RefreshRecording;
        _voice = null;
    }

    public override void _Process(double delta)
    {
        if (VoiceChat.I is not { } voice || !voice.RecordingFor(_target)) return;
        _tick += delta;
        if (_tick < TickSeconds) return;
        _tick = 0;
        _clock.Text = $"{VoiceNoteRules.Clock(voice.RecordingSeconds)} / {VoiceNoteRules.Clock(VoiceNoteRules.MaxSeconds)}";
        _level.Value = voice.MicLevel;
    }

    public void RefreshRecording()
    {
        bool recording = VoiceChat.I?.RecordingFor(_target) == true;
        _field.Visible = !recording;
        _strip.Visible = recording;
        _send.Visible = !recording;
        _record.Icon = UiIcons.Get(recording ? "system/send" : "system/waveform");
        _record.TooltipText = recording ? "Send the voice note" : RecordTip;
        _record.AddThemeColorOverride("icon_normal_color", recording ? RecordingColour : UiTheme.Gold);
        if (!recording) _level.Value = 0;
    }

    public void ShowRecording(double seconds, float level)
    {
        _field.Visible = false;
        _strip.Visible = true;
        _send.Visible = false;
        _record.Icon = UiIcons.Get("system/send");
        _record.AddThemeColorOverride("icon_normal_color", RecordingColour);
        _clock.Text = $"{VoiceNoteRules.Clock(seconds)} / {VoiceNoteRules.Clock(VoiceNoteRules.MaxSeconds)}";
        _level.Value = level;
    }

    public bool InsertItemLink(int itemId)
    {
        if (ItemData.Get(itemId) == null) return false;
        var (text, caret) = _draft.Insert(Input.Text, TextRunes.Utf16Index(Input.Text, Input.CaretColumn), itemId, ItemData.DisplayName(itemId));
        if (TextRunes.Count(text) + _draft.WireExtra > ChatInputText.MaxLength)
        {
            _draft.Clear();
            return true;
        }
        Input.Text = text;
        Input.CaretColumn = TextRunes.Column(text, caret);
        _lastText = text;
        Limit();
        Input.GrabFocus();
        return true;
    }

    private void Submit()
    {
        string message = Input.Text.Trim();
        if (message.Length == 0) return;
        string wire = _draft.ToWire(message);
        _draft.Clear();
        _lastText = "";
        Input.Clear();
        Limit();
        if (!Input.HasFocus()) Input.CallDeferred(Control.MethodName.GrabFocus);
        Submitted?.Invoke(wire);
    }

    private void Limit()
    {
        int limit = ChatInputText.MaxLength - _draft.WireExtra;
        if (Input.MaxLength == limit) return;
        int caret = Input.CaretColumn;
        Input.MaxLength = limit;
        Input.CaretColumn = caret;
    }

    private void OpenEmoticons(Vector2 at)
    {
        if (_emoticons == null)
        {
            _emoticons = new EmoticonPicker();
            AddChild(_emoticons);
            _emoticons.Picked += code =>
            {
                if (EmoticonPicker.Insert(Input, code) is { } refusal) Notice?.Invoke(refusal);
                Input.CallDeferred(Control.MethodName.GrabFocus);
            };
        }
        _emoticons.ShowAt(at);
    }

    private void OnRecordDown()
    {
        if (VoiceChat.I is not { } voice) return;
        if (voice.RecordingFor(_target))
        {
            _hold = false;
            voice.StopRecording(send: true);
            return;
        }
        if (!voice.StartRecording(_target)) return;
        _hold = true;
        _pressedAt = Time.GetTicksMsec() / 1000.0;
    }

    private void OnRecordUp()
    {
        if (VoiceChat.I is not { } voice || !voice.RecordingFor(_target) || !_hold) return;
        _hold = false;
        if (Time.GetTicksMsec() / 1000.0 - _pressedAt >= HoldSeconds) voice.StopRecording(send: true);
    }
}
