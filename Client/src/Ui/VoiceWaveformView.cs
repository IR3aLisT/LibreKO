using System;
using Godot;
using LibreKO.Domain;

namespace LibreKO;

public sealed partial class VoiceWaveformView : Control
{
    private const float BarGap = 1.5f;
    private const float MinBarWidth = 1.5f;
    private const float MinBarHeight = 2f;
    private const float DotRadius = 5f;
    private const int FlatSample = 40;

    private byte[] _samples = [];
    private float _progress;

    public event Action<float>? Seeked;

    public Color Played { get; set; } = UiTheme.Gold;
    public Color Rest { get; set; } = new(UiTheme.TextLo, 0.45f);
    public Color Dot { get; set; } = UiTheme.Premium;

    public VoiceWaveformView()
    {
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.PointingHand;
    }

    public byte[] Samples
    {
        get => _samples;
        set
        {
            _samples = value;
            QueueRedraw();
        }
    }

    public float Progress
    {
        get => _progress;
        set
        {
            float clamped = Math.Clamp(value, 0f, 1f);
            if (Mathf.IsEqualApprox(clamped, _progress)) return;
            _progress = clamped;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        int count = _samples.Length > 0 ? _samples.Length : VoiceWaveform.Samples;
        float left = DotRadius;
        float width = Math.Max(1f, Size.X - DotRadius * 2);
        float step = width / count;
        float bar = Math.Max(MinBarWidth, step - BarGap);
        float middle = Size.Y / 2;
        float tallest = Size.Y - 2;
        for (int i = 0; i < count; i++)
        {
            byte sample = _samples.Length > 0 ? _samples[i] : (byte)FlatSample;
            float height = Math.Max(MinBarHeight, sample / (float)VoiceWaveform.Peak * tallest);
            float x = left + i * step;
            var colour = (i + 0.5f) / count <= _progress ? Played : Rest;
            DrawRect(new Rect2(x, middle - height / 2, bar, height), colour);
        }
        DrawCircle(new Vector2(left + _progress * width, middle), DotRadius, Dot);
    }

    public override void _GuiInput(InputEvent ev)
    {
        if (ev is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click) return;
        float width = Math.Max(1f, Size.X - DotRadius * 2);
        Seeked?.Invoke(Math.Clamp((click.Position.X - DotRadius) / width, 0f, 1f));
        AcceptEvent();
    }
}

public sealed partial class NoteDownloadRing : Control
{
    private const float Thickness = 2f;
    private const int Points = 32;
    private const float StartAngle = -Mathf.Pi / 2;

    private float _fraction;

    public Color Fill { get; set; } = UiTheme.Premium;
    public Color Track { get; set; } = new(UiTheme.TextLo, 0.3f);

    public NoteDownloadRing() => MouseFilter = MouseFilterEnum.Ignore;

    public float Fraction
    {
        get => _fraction;
        set
        {
            float clamped = Math.Clamp(value, 0f, 1f);
            if (Mathf.IsEqualApprox(clamped, _fraction)) return;
            _fraction = clamped;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        var centre = Size / 2;
        float radius = Math.Min(Size.X, Size.Y) / 2 - Thickness;
        DrawArc(centre, radius, 0, Mathf.Tau, Points, Track, Thickness, true);
        if (_fraction > 0f)
            DrawArc(centre, radius, StartAngle, StartAngle + Mathf.Tau * _fraction, Points, Fill, Thickness, true);
    }
}
