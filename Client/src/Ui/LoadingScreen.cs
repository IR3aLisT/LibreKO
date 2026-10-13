using Godot;
using LibreKO.Domain;

namespace LibreKO;

public partial class LoadingScreen : CanvasLayer
{
    private ProgressBar _bar = null!;
    private Label _status = null!;
    private Label _detail = null!;
    private Label _percent = null!;
    private readonly LoadingGlide _glide = new();
    private const float PercentWidth = 56f;

    public override void _Ready()
    {
        Layer = 120;

        Ui.Background(this, Backdrop.LoadingArt, Backdrop.Fit.Cover, bottomScrim: true);

        var host = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        host.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(host);
        Ui.AutoScale(host, baseHeight: 1080f / Platform.TouchMenuScale, min: 0.7f,
                     max: Platform.Pick(2.0f, Platform.MenuScaleMax), pivot: new Vector2(0.5f, 1f));

        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AnchorLeft = 0.5f; box.AnchorRight = 0.5f; box.AnchorTop = 1f; box.AnchorBottom = 1f;
        box.OffsetLeft = -340; box.OffsetRight = 340; box.OffsetTop = -104; box.OffsetBottom = -44;
        box.AddThemeConstantOverride("separation", 8);
        host.AddChild(box);

        _status = Line("Loading…", 19, UiTheme.TextHi, 5);
        box.AddChild(_status);

        var barRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        barRow.AddThemeConstantOverride("separation", 8);
        box.AddChild(barRow);
        _bar = new ProgressBar
        {
            MinValue = 0, MaxValue = 1, Value = 0,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, 16),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        var barBg = new StyleBoxFlat { BgColor = new Color(0.02f, 0.02f, 0.03f, 0.85f), BorderColor = new Color(UiTheme.Gold, 0.45f) };
        barBg.SetBorderWidthAll(1);
        barBg.SetCornerRadiusAll(3);
        var barFill = new StyleBoxFlat { BgColor = UiTheme.Gold };
        barFill.SetCornerRadiusAll(2);
        _bar.AddThemeStyleboxOverride("background", barBg);
        _bar.AddThemeStyleboxOverride("fill", barFill);
        barRow.AddChild(_bar);
        _percent = Line("0 %", 15, UiTheme.GoldBright, 4);
        _percent.HorizontalAlignment = HorizontalAlignment.Right;
        _percent.CustomMinimumSize = new Vector2(PercentWidth, 0);
        barRow.AddChild(_percent);

        _detail = Line("", 12, new Color(UiTheme.TextLo, 0.94f), 4);
        box.AddChild(_detail);
    }

    public void Set(string status, double progress, string detail = "")
    {
        if (_status != null) _status.Text = status;
        if (_detail != null) _detail.Text = detail;
        _glide.Set(progress, Now());
        ShowProgress();
    }

    public override void _Process(double delta) => ShowProgress();

    private void ShowProgress()
    {
        if (_bar == null) return;
        double shown = _glide.At(Now());
        _bar.Value = shown;
        _percent.Text = $"{LoadingGlide.Percent(shown)} %";
    }

    private static double Now() => Time.GetTicksMsec() / 1000.0;

    private static Label Line(string text, int size, Color color, int outline)
    {
        var l = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", color);
        l.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.85f));
        l.AddThemeConstantOverride("outline_size", outline);
        return l;
    }
}
