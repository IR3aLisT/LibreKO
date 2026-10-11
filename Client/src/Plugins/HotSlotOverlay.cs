using Godot;
using LibreKO.Domain;

namespace LibreKO.Plugins;

public partial class HotSlotOverlay : Control
{
    private static readonly StringName RemainParam = "remain";
    private static readonly Texture2D?[] ReadyFrames = new Texture2D?[ReadyFlash.FrameCount];
    private const float RemainEpsilon = 0.002f;

    private readonly ColorRect _sweep;
    private readonly ShaderMaterial _sweepMaterial;
    private readonly TextureRect _ready;
    private readonly TextureRect _selectedLoop;
    private float _remain;
    private float _shown = -1f;
    private int _readyFrame = -1;
    private bool _selected;
    private double _selectedFor;

    public HotSlotOverlay()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
        _sweepMaterial = new ShaderMaterial { Shader = Shaders.Get("cooldown") };
        _sweep = new ColorRect { Material = _sweepMaterial, MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        _sweep.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_sweep);
        _ready = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
            Modulate = new Color(1f, 1f, 1f, ReadyFlash.Alpha),
            Visible = false,
        };
        _ready.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_ready);
        _selectedLoop = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false,
        };
        _selectedLoop.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_selectedLoop);
        SetProcess(false);
    }

    public float Remain
    {
        get => _remain;
        set
        {
            float clamped = Mathf.Clamp(value, 0f, 1f);
            bool on = clamped > RemainEpsilon;
            if (_sweep.Visible != on) _sweep.Visible = on;
            if (on && Mathf.Abs(clamped - _shown) > RemainEpsilon)
            {
                _sweepMaterial.SetShaderParameter(RemainParam, clamped);
                _shown = clamped;
            }
            _remain = on ? clamped : 0f;
        }
    }

    public bool Selected
    {
        get => _selected;
        set
        {
            if (_selected == value) return;
            _selected = value;
            _selectedFor = 0;
            _selectedLoop.Visible = value;
            if (value) _selectedLoop.Texture = Frame(ReadyFlash.SelectedFrameAt(0));
            UpdateProcessing();
        }
    }

    public void Flash()
    {
        _readyFrame = 0;
        ShowReadyFrame();
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        if (_selected)
        {
            _selectedFor += delta;
            _selectedLoop.Texture = Frame(ReadyFlash.SelectedFrameAt(_selectedFor));
        }
        if (_readyFrame < 0) return;
        if (++_readyFrame < ReadyFlash.FrameCount)
        {
            ShowReadyFrame();
            return;
        }
        _readyFrame = -1;
        _ready.Visible = false;
        UpdateProcessing();
    }

    private void UpdateProcessing() => SetProcess(_selected || _readyFrame >= 0);

    private static Texture2D? Frame(int index) =>
        ReadyFrames[index] ??= ResourceLoader.Load<Texture2D>(ReadyFlash.FramePath(index));

    private void ShowReadyFrame()
    {
        _ready.Texture = Frame(_readyFrame);
        _ready.Visible = _ready.Texture != null;
    }
}
