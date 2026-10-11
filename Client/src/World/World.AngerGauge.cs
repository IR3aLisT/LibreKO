using Godot;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const string AngerSheetPath = "res://assets/ui/anger/anger.png";
    private static readonly Vector2 AngerGaugeDefault = new(330f, 14f);
    private const float AngerGaugeGap = 6f;

    private Control? _angerGauge;
    private TextureRect _angerImage = null!;
    private AtlasTexture _angerFrame = null!;
    private int _angerShownImage;
    private int _angerFullFrame;
    private double _angerFrameAt;

    private void AngerGaugeInit()
    {
        var layer = new CanvasLayer { Layer = 64 };
        AddChild(layer);
        var gauge = new Control
        {
            CustomMinimumSize = new Vector2(AngerGauge.FrameWidth, AngerGauge.FrameHeight),
            Size = new Vector2(AngerGauge.FrameWidth, AngerGauge.FrameHeight),
            MouseFilter = Control.MouseFilterEnum.Stop,
            Visible = false,
        };
        layer.AddChild(gauge);
        _angerFrame = new AtlasTexture { Atlas = ResourceLoader.Load<Texture2D>(AngerSheetPath) };
        _angerImage = new TextureRect
        {
            Texture = _angerFrame,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _angerImage.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        gauge.AddChild(_angerImage);
        _angerGauge = gauge;
        HudLayout.Attach(gauge, "hud_anger", gauge, AngerGaugeHome);

        Net.I.PvpAngerEvent += OnAngerGauge;
        OnAngerGauge(Net.I.AngerGauge, Net.I.AngerFull);
    }

    private void AngerGaugeDispose() => Net.I.PvpAngerEvent -= OnAngerGauge;

    private void OnAngerGauge(int gauge, bool full)
    {
        _angerShownImage = full ? 0 : AngerGauge.ImageFor(gauge, _angerShownImage);
        if (!full) ShowAngerFrame(AngerGauge.GageFrame(_angerShownImage));
    }

    private void AngerGaugeTick(double now)
    {
        if (_angerGauge == null) return;
        bool shown = AngerGauge.ShownIn(_zone, Sheet.Level);
        if (_angerGauge.Visible != shown) _angerGauge.Visible = shown;
        if (!shown || !Net.I.AngerFull || now < _angerFrameAt) return;
        _angerFrameAt = now + AngerGauge.FullFrameSeconds;
        ShowAngerFrame(AngerGauge.FullFrame(_angerFullFrame++));
    }

    private void ShowAngerFrame((int X, int Y) at) =>
        _angerFrame.Region = new Rect2(at.X, at.Y, AngerGauge.FrameWidth, AngerGauge.FrameHeight);

    private Vector2 AngerGaugeHome()
    {
        if (_statusRoot == null || !IsInstanceValid(_statusRoot) || !_statusRoot.IsVisibleInTree()) return AngerGaugeDefault;
        var status = _statusRoot.GetGlobalRect();
        return new Vector2(status.End.X + AngerGaugeGap, status.Position.Y);
    }
}
