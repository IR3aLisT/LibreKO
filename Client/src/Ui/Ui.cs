using Godot;

namespace LibreKO;

public static class Ui
{
    public static Control? Background(Node parent, string resPath,
        Backdrop.Fit fit = Backdrop.Fit.Cover, bool bottomScrim = false)
        => Backdrop.Build(parent, resPath, fit, bottomScrim);

    public static void PreGameBackdrop(Control scene)
    {
        Background(scene, Backdrop.LoginArt, Backdrop.LoginFit);
        if (scene.GetNodeOrNull<Panel>("Panel") is { } p)
            p.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());

        if (scene.GetNodeOrNull<Control>("Panel/Center") is { } center) AutoScale(center);
        if (scene.GetNodeOrNull<Control>("Panel/Center/VBox") is { } form)
        {
            form.AddThemeConstantOverride("separation", 10);
            FormCard(form);
        }
    }

    public static void FormCard(Control form, Vector2? padding = null)
    {
        Node? host = form.GetParent();
        while (host is Container) host = host.GetParent();
        if (host is not Control hostCtrl) return;

        Node branch = form;
        while (branch.GetParent() != null && branch.GetParent() != host) branch = branch.GetParent()!;

        var card = new FormCardPanel { Target = form, Pad = padding ?? new Vector2(30, 26) };
        hostCtrl.AddChild(card);
        hostCtrl.MoveChild(card, branch.GetIndex());
    }

    public static void AutoScale(Control container, float baseHeight = 1080f,
        float min = 0.6f, float max = 2.0f, Vector2? pivot = null)
        => container.AddChild(new FormScaler
        {
            Target = container, BaseHeight = baseHeight / Platform.MenuScale, Min = min, Max = max,
            Pivot = pivot ?? new Vector2(0.5f, 0.5f),
        });

    public static void MenuScale(bool on)
    {
        Platform.MenuScreens = on;
        Config.ApplyUiScale();
    }

    public static ImageTexture FadeTexture(Color centre, float hold, bool vertical = false)
    {
        const int n = 256;
        var img = Image.CreateEmpty(vertical ? 1 : n, vertical ? n : 1, false, Image.Format.Rgba8);
        for (int i = 0; i < n; i++)
        {
            float t = Mathf.Abs(i / (n - 1f) * 2f - 1f);
            float a = t <= hold ? 1f : 1f - Mathf.SmoothStep(hold, 1f, t);
            var c = new Color(centre, centre.A * a);
            if (vertical) img.SetPixel(0, i, c); else img.SetPixel(i, 0, c);
        }
        return ImageTexture.CreateFromImage(img);
    }

    public static StyleBoxTexture EdgeFade(Color centre, float hold = 0.55f)
        => new()
        {
            Texture = FadeTexture(centre, hold),
            ContentMarginLeft = 14, ContentMarginRight = 14,
            ContentMarginTop = 8, ContentMarginBottom = 8,
        };

    public static ImageTexture BoxFadeTexture(Color centre, float holdX, float holdY)
    {
        const int n = 128;
        var img = Image.CreateEmpty(n, n, false, Image.Format.Rgba8);
        for (int y = 0; y < n; y++)
        {
            float ty = Mathf.Abs(y / (n - 1f) * 2f - 1f);
            float ay = ty <= holdY ? 1f : 1f - Mathf.SmoothStep(holdY, 1f, ty);
            for (int x = 0; x < n; x++)
            {
                float tx = Mathf.Abs(x / (n - 1f) * 2f - 1f);
                float ax = tx <= holdX ? 1f : 1f - Mathf.SmoothStep(holdX, 1f, tx);
                img.SetPixel(x, y, new Color(centre, centre.A * ax * ay));
            }
        }
        return ImageTexture.CreateFromImage(img);
    }

    public static void SoftScrim(Control form, Vector2? padding = null)
    {
        Node? host = form.GetParent();
        while (host is Container) host = host.GetParent();
        if (host is not Control hostCtrl) return;

        Node branch = form;
        while (branch.GetParent() != null && branch.GetParent() != host) branch = branch.GetParent()!;

        var card = new FormCardPanel
        {
            Target = form,
            Pad = padding ?? new Vector2(64, 46),
            Style = new StyleBoxTexture
            {
                Texture = BoxFadeTexture(
                    new Color(0.015f, 0.015f, 0.02f, Platform.Pick(0.74f, 0.90f)),
                    Platform.Pick(0.30f, 0.52f), Platform.Pick(0.55f, 0.68f)),
            },
        };
        hostCtrl.AddChild(card);
        hostCtrl.MoveChild(card, branch.GetIndex());
    }

    public const int TouchButtonHeight = 46;

    public static Button MenuButton(string text, int height = 42, int fontSize = 19)
    {
        var b = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(0, Platform.TouchUi ? Mathf.Max(height, TouchButtonHeight) : height),
            FocusMode = Control.FocusModeEnum.None,
        };
        b.AddThemeFontSizeOverride("font_size", fontSize);
        b.AddThemeColorOverride("font_color", UiTheme.TextHi);
        b.AddThemeColorOverride("font_hover_color", UiTheme.GoldBright);
        b.AddThemeColorOverride("font_pressed_color", UiTheme.Gold);
        b.AddThemeColorOverride("font_disabled_color", new Color(UiTheme.TextLo, 0.45f));
        b.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
        b.AddThemeConstantOverride("outline_size", 4);
        b.AddThemeStyleboxOverride("normal", EdgeFade(new Color(0.02f, 0.02f, 0.03f, 0.80f)));
        b.AddThemeStyleboxOverride("hover", EdgeFade(Platform.Pick(
            new Color(0.30f, 0.22f, 0.09f, 0.92f), new Color(0.02f, 0.02f, 0.03f, 0.80f))));
        b.AddThemeStyleboxOverride("pressed", EdgeFade(new Color(0.05f, 0.04f, 0.03f, 0.85f)));
        b.AddThemeStyleboxOverride("disabled", EdgeFade(new Color(0.02f, 0.02f, 0.03f, 0.35f)));
        b.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        return b;
    }

    public static Button[] ActionGroup(Node parent, params (string Text, System.Action Pressed)[] items)
    {
        Node host = parent;
        if (Platform.TouchUi)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            parent.AddChild(row);
            host = row;
        }

        var made = new Button[items.Length];
        for (int i = 0; i < items.Length; i++)
        {
            var b = MenuButton(items[i].Text, 36, 16);
            b.Pressed += items[i].Pressed;
            if (Platform.TouchUi) b.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            host.AddChild(b);
            made[i] = b;
        }
        return made;
    }

    public static void MarkSelected(Button b, bool selected)
    {
        var fill = selected
            ? EdgeFade(new Color(0.30f, 0.22f, 0.09f, 0.92f))
            : EdgeFade(new Color(0.02f, 0.02f, 0.03f, 0.80f));
        b.AddThemeStyleboxOverride("normal", fill);
        b.AddThemeStyleboxOverride("pressed", fill);
        if (Platform.TouchUi) b.AddThemeStyleboxOverride("hover", fill);
        b.AddThemeColorOverride("font_color", selected ? UiTheme.GoldBright : UiTheme.TextHi);
    }

    public static Label Legend(string text, int fontSize, Color colour)
    {
        var l = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
        l.AddThemeFontSizeOverride("font_size", fontSize);
        l.AddThemeColorOverride("font_color", colour);
        l.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
        l.AddThemeConstantOverride("outline_size", 5);
        return l;
    }

    public static void StyleField(LineEdit e)
    {
        var box = new StyleBoxFlat
        {
            BgColor = new Color(0.02f, 0.02f, 0.03f, 0.62f),
            BorderColor = new Color(UiTheme.Gold, 0.32f),
        };
        box.SetBorderWidthAll(1);
        box.SetCornerRadiusAll(3);
        box.SetContentMarginAll(8);
        var focused = (StyleBoxFlat)box.Duplicate();
        focused.BorderColor = new Color(UiTheme.Gold, 0.85f);
        focused.BgColor = new Color(0.04f, 0.035f, 0.03f, 0.78f);
        e.AddThemeStyleboxOverride("normal", box);
        e.AddThemeStyleboxOverride("focus", focused);
        e.AddThemeStyleboxOverride("read_only", box);
        e.AddThemeColorOverride("font_color", UiTheme.TextHi);
        e.AddThemeColorOverride("font_placeholder_color", new Color(UiTheme.TextLo, 0.5f));
        e.CustomMinimumSize = new Vector2(0, Platform.Pick(34, TouchButtonHeight));
    }
}

public partial class FormCardPanel : Panel
{
    public Control Target = null!;
    public Vector2 Pad = new(30, 26);
    public StyleBox? Style;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        if (Style != null) { AddThemeStyleboxOverride("panel", Style); return; }
        var sb = new StyleBoxFlat
        {
            BgColor = new Color(0.040f, 0.038f, 0.044f, 0.84f),
            BorderColor = new Color(UiTheme.Gold, 0.55f),
            ShadowColor = new Color(0, 0, 0, 0.60f),
            ShadowSize = 16,
        };
        sb.SetBorderWidthAll(1);
        sb.SetCornerRadiusAll(6);
        AddThemeStyleboxOverride("panel", sb);
    }

    public override void _Process(double delta)
    {
        if (!IsInstanceValid(Target)) { QueueFree(); return; }
        if (Visible != Target.Visible) Visible = Target.Visible;
        if (!Visible) return;
        var xf = Target.GetGlobalTransform();
        var scale = xf.Scale;
        var pad = Pad * scale;
        var pos = xf.Origin - pad;
        var size = Target.Size * scale + pad * 2f;
        if (GlobalPosition != pos) GlobalPosition = pos;
        if (Size != size) Size = size;
    }
}

public partial class FormScaler : Node
{
    public Control Target = null!;
    public float BaseHeight = 1080f, Min = 0.6f, Max = 2.0f;
    public Vector2 Pivot = new(0.5f, 0.5f);
    private bool _queued;

    public override void _Ready()
    {
        if (!IsInstanceValid(Target)) { QueueFree(); return; }
        Target.Resized += QueueArrange;
        Target.ChildEnteredTree += Watch;
        foreach (var child in Target.GetChildren()) Watch(child);
        QueueArrange();
    }

    private void Watch(Node child)
    {
        if (child is not Control control) return;
        control.Resized += QueueArrange;
        control.VisibilityChanged += QueueArrange;
    }

    private void QueueArrange()
    {
        if (_queued) return;
        _queued = true;
        Callable.From(Arrange).CallDeferred();
    }

    private void Arrange()
    {
        _queued = false;
        if (!IsInstanceValid(Target)) { QueueFree(); return; }
        var box = Target.Size;
        if (box.X < 1f || box.Y < 1f) return;

        float s = Mathf.Clamp(box.Y / BaseHeight, Min, Max);
        var content = ContentSize();
        if (content.X > 1f) s = Mathf.Min(s, box.X * 0.90f / content.X);
        if (content.Y > 1f) s = Mathf.Min(s, box.Y * 0.90f / content.Y);
        s = Mathf.Max(s, 0.35f);

        Target.PivotOffset = box * Pivot;
        if (!Target.Scale.IsEqualApprox(new Vector2(s, s))) Target.Scale = new Vector2(s, s);
    }

    private Vector2 ContentSize()
    {
        var max = Vector2.Zero;
        foreach (var child in Target.GetChildren())
            if (child is Control c && c.Visible)
                max = new Vector2(Mathf.Max(max.X, c.Size.X), Mathf.Max(max.Y, c.Size.Y));
        return max;
    }
}

public partial class SettingsPanel : CanvasLayer
{
    private static readonly Vector2I[] CommonResolutions =
    {
        new(1280, 720), new(1366, 768), new(1600, 900), new(1920, 1080), new(2560, 1440), new(3840, 2160),
    };

    private static readonly (QualityLevel Level, string Label)[] LevelChoices =
    {
        (QualityLevel.Off, "Off"), (QualityLevel.Low, "Low"), (QualityLevel.Medium, "Medium"), (QualityLevel.High, "High"),
    };

    private static readonly (CospreKinds Kind, string Label)[] CospreChoices =
    {
        (CospreKinds.Wings, "Wings"), (CospreKinds.Fairies, "Fairies"), (CospreKinds.Talismans, "Talismans"),
        (CospreKinds.Emblems, "Emblems"), (CospreKinds.PathosGlow, "Pathos Glow"),
    };

    private readonly System.Collections.Generic.List<Vector2I> _resolutions = new();
    private readonly System.Collections.Generic.Dictionary<CospreKinds, CheckButton> _cospreShown = new();
    private OptionButton _screen = null!;
    private OptionButton _preset = null!;
    private OptionButton _shadowLevel = null!, _ssaoLevel = null!;
    private OptionButton _renderer = null!;
    private CheckButton _showTitles = null!;
    private Button _applyButton = null!;
    private bool _dirty;
    private bool _presetApplying;

    private const int TabWidth = 660;
    private const int TabHeight = 660;
    private const int TouchScreenMargin = 10;

    private OptionButton _mode = null!;
    private OptionButton _language = null!;
    private OptionButton _res = null!;
    private CheckButton _volFog = null!, _bloom = null!, _clouds = null!;
    private CheckButton _fxLayer = null!;
    private CheckButton _capes = null!;
    private OptionButton _aa = null!;
    private OptionButton _upscale = null!;
    private OptionButton _upscaleQuality = null!;
    private CheckButton _vsync = null!;
    private OptionButton _fps = null!;
    private CheckButton _fxAmbient = null!, _fxNumbers = null!, _fxCombatLog = null!;
    private HSlider _fxDistance = null!;
    private HSlider _uiScale = null!;
    private HSlider _nameScale = null!;
    private HSlider _hotbarScale = null!;
    private HSlider _hotbarGap = null!;
    private HSlider _viewDistance = null!;
    private HSlider _brightness = null!, _contrast = null!, _nightBrightness = null!, _fog = null!;
    private HSlider _camTurnSpeed = null!, _camEdgeSpeed = null!;
    private HSlider? _moveStick, _lookStick;
    private CheckButton _camEdgePan = null!;
    private CheckButton _cameraShake = null!;
    private CheckButton _sndOn = null!;
    private CheckButton _sndOthers = null!;
    private HSlider _volMaster = null!, _volMusic = null!, _volSfx = null!, _volUi = null!, _volVoice = null!;

    public static void Open(Node parent) => parent.AddChild(new SettingsPanel { Layer = 200 });

    public int InitialTab { get; init; }

    public override void _ExitTree() => Audio.ApplyVolumes();

    public override void _Ready()
    {
        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.45f) };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(dim);

        Control host;
        if (Platform.TouchUi)
        {
            var full = new MarginContainer();
            foreach (var s in new[] { "left", "right", "top", "bottom" })
                full.AddThemeConstantOverride($"margin_{s}", TouchScreenMargin);
            host = full;
        }
        else
        {
            host = new CenterContainer();
        }
        host.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(host);

        var panel = new PanelContainer();
        var panelStyle = new StyleBoxFlat { BgColor = new Color(0.11f, 0.11f, 0.13f, 0.98f) };
        panelStyle.SetCornerRadiusAll(8);
        panelStyle.SetBorderWidthAll(1);
        panelStyle.BorderColor = new Color(0.30f, 0.30f, 0.36f);
        panel.AddThemeStyleboxOverride("panel", panelStyle);
        host.AddChild(panel);
        var margin = new MarginContainer();
        foreach (var s in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride($"margin_{s}", 18);
        panel.AddChild(margin);
        var outer = new VBoxContainer();
        outer.AddThemeConstantOverride("separation", 12);
        margin.AddChild(outer);

        var title = new Label { Text = "Settings", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 22);
        outer.AddChild(title);

        var tabs = new TabContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        if (Platform.PointerUi)
        {
            var room = GetViewport().GetVisibleRect().Size;
            tabs.CustomMinimumSize = new Vector2(
                Mathf.Min(TabWidth + 40, room.X - 80),
                Mathf.Min(TabHeight + 60, room.Y - 170));
        }
        outer.AddChild(tabs);

        BuildDisplayTab(Tab(tabs, "Display"));
        BuildGraphicsTab(Tab(tabs, "Graphics"));
        BuildEffectsTab(Tab(tabs, "Effects"));
        BuildControlsTab(Tab(tabs, "Controls"));
        BuildKeysTab(Tab(tabs, "Keys"));
        BuildPadTab(Tab(tabs, "Joypad"));
        BuildSoundTab(Tab(tabs, "Sound"));
        BuildPluginsTab(Tab(tabs, "Plugins"));
        tabs.CurrentTab = Mathf.Clamp(InitialTab, 0, tabs.GetTabCount() - 1);

        var btns = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        btns.AddThemeConstantOverride("separation", 12);
        outer.AddChild(btns);
        var ok = new Button { Text = "OK", CustomMinimumSize = ActionButtonSize };
        ok.Pressed += () =>
        {
            if (_dirty) ApplyAll();
            QueueFree();
        };
        btns.AddChild(ok);
        _applyButton = new Button { Text = "Apply", CustomMinimumSize = ActionButtonSize, Disabled = true };
        _applyButton.Pressed += ApplyAll;
        btns.AddChild(_applyButton);
        var cancel = new Button { Text = "Cancel", CustomMinimumSize = ActionButtonSize };
        cancel.Pressed += QueueFree;
        btns.AddChild(cancel);
        foreach (Node page in tabs.GetChildren())
            TrackDirty(page);
    }

    private void TrackDirty(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            switch (child)
            {
                case CheckButton check: check.Toggled += _ => MarkDirty(); break;
                case OptionButton option: option.ItemSelected += _ => MarkDirty(); break;
                case Godot.Range range: range.ValueChanged += _ => MarkDirty(); break;
            }
            TrackDirty(child);
        }
    }

    private void MarkDirty()
    {
        _dirty = true;
        _applyButton.Disabled = false;
    }

    private void ApplyAll()
    {
        {
            Config.SetScreen(_screen.GetSelectedId());
            var resolution = _resolutions.Count > 0 ? _resolutions[Mathf.Clamp(_res.Selected, 0, _resolutions.Count - 1)]
                : new Vector2I(Config.WinWidth, Config.WinHeight);
            Config.SetVideo((Config.VideoMode)_mode.Selected, resolution.X, resolution.Y, _vsync.ButtonPressed);
            Config.SetGraphics(LevelAt(_shadowLevel), LevelAt(_ssaoLevel), _volFog.ButtonPressed,
                               _bloom.ButtonPressed, _clouds.ButtonPressed, _capes.ButtonPressed,
                               (Config.AaMode)_aa.Selected, (Config.UpscaleMode)_upscale.Selected,
                               (Config.UpscaleLevel)_upscaleQuality.Selected, (Config.FpsCap)_fps.Selected);
            Config.SetShowTitles(_showTitles.ButtonPressed);
            var hidden = CospreKinds.None;
            foreach (var (kind, check) in _cospreShown)
                if (!check.ButtonPressed) hidden |= kind;
            Config.SetHiddenCospre(hidden);
            Config.SetEffects(_fxAmbient.ButtonPressed, (int)_fxDistance.Value, _fxNumbers.ButtonPressed,
                              _fxCombatLog.ButtonPressed);
            Config.SetFxLayer(_fxLayer.ButtonPressed);
            Config.SetControls((float)_camTurnSpeed.Value, _camEdgePan.ButtonPressed, (float)_camEdgeSpeed.Value);
            Config.SetCameraShake(_cameraShake.ButtonPressed);
            if (_moveStick != null && _lookStick != null)
                Config.SetStickSensitivity((float)_moveStick.Value, (float)_lookStick.Value);
            Config.SetUiScale((float)_uiScale.Value);
            Config.SetNamePlateScale((float)_nameScale.Value);
            Config.SetHotbarLook((float)_hotbarScale.Value, (int)_hotbarGap.Value);
            ApplyLanguage();
            Config.SetViewDistance((float)_viewDistance.Value);
            Config.SetLighting((int)_brightness.Value, (int)_contrast.Value, (int)_nightBrightness.Value, (int)_fog.Value);
            Config.SetOtherPlayersSounds(_sndOthers.ButtonPressed);
            Config.SetAudio(_sndOn.ButtonPressed, Config.AudioMuted, (float)_volMaster.Value,
                            (float)_volMusic.Value, (float)_volSfx.Value,
                            (float)_volUi.Value, (float)_volVoice.Value);
            var driver = (Config.RenderDriver)_renderer.Selected;
            if (Config.SetRenderer(driver) || !Config.RendererRunning(driver))
                Notice.Confirm(this, "The renderer changes after a restart. Restart now?", "Restart", "Later",
                    RestartClient, title: "Renderer");
        }
        _dirty = false;
        _applyButton.Disabled = true;
    }

    private void RestartClient()
    {
        OS.SetRestartOnExit(true, OS.GetCmdlineArgs());
        GetTree().Quit();
    }

    private static QualityLevel LevelAt(OptionButton option) =>
        LevelChoices[Mathf.Clamp(option.Selected, 0, LevelChoices.Length - 1)].Level;

    private static OptionButton LevelPicker(QualityLevel current)
    {
        var option = new OptionButton();
        for (int i = 0; i < LevelChoices.Length; i++)
            option.AddItem(LevelChoices[i].Label, i);
        option.Selected = System.Array.FindIndex(LevelChoices, c => c.Level == current);
        return option;
    }

    private static Button RestoreButton(VBoxContainer vb, System.Action restore)
    {
        var button = new Button
        {
            Text = "Restore defaults",
            FocusMode = Control.FocusModeEnum.None,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd,
        };
        button.Pressed += restore;
        vb.AddChild(new HSeparator());
        vb.AddChild(button);
        return button;
    }

    private GraphicsChoice CurrentGraphics() => new(
        LevelAt(_shadowLevel), LevelAt(_ssaoLevel), _volFog.ButtonPressed, _bloom.ButtonPressed, _clouds.ButtonPressed,
        _aa.Selected, (float)_viewDistance.Value);

    private void ApplyPreset(GraphicsPreset preset)
    {
        if (preset == GraphicsPreset.Custom) return;
        var choice = GraphicsPresets.Of(preset);
        _presetApplying = true;
        _shadowLevel.Selected = System.Array.FindIndex(LevelChoices, c => c.Level == choice.Shadows);
        _ssaoLevel.Selected = System.Array.FindIndex(LevelChoices, c => c.Level == choice.Ssao);
        _volFog.ButtonPressed = choice.VolumetricFog;
        _bloom.ButtonPressed = choice.Bloom;
        _clouds.ButtonPressed = choice.Clouds;
        _aa.Selected = choice.AntiAlias;
        _viewDistance.Value = choice.ViewDistance;
        _presetApplying = false;
        MarkDirty();
    }

    private void RefreshPreset()
    {
        if (_presetApplying) return;
        _preset.Selected = (int)GraphicsPresets.Match(CurrentGraphics());
    }

    private void FillResolutions(int screen)
    {
        var native = DisplayServer.ScreenGetSize(screen >= 0 ? screen : DisplayServer.WindowGetCurrentScreen());
        _resolutions.Clear();
        foreach (var size in CommonResolutions)
            if (size.X <= native.X && size.Y <= native.Y) _resolutions.Add(size);
        if (!_resolutions.Contains(native)) _resolutions.Add(native);
        _res.Clear();
        int selected = _resolutions.Count - 1;
        for (int i = 0; i < _resolutions.Count; i++)
        {
            _res.AddItem($"{_resolutions[i].X} x {_resolutions[i].Y}{(_resolutions[i] == native ? "  (native)" : "")}", i);
            if (_resolutions[i].X == Config.WinWidth && _resolutions[i].Y == Config.WinHeight) selected = i;
        }
        _res.Selected = selected;
    }

    private static Vector2 ActionButtonSize =>
        Platform.Pick(Vector2.Zero, new Vector2(140, Ui.TouchButtonHeight));

    private static readonly (GameLanguage Language, string Label)[] LanguageChoices =
    {
        (GameLanguage.English, "English"),
        (GameLanguage.Spanish, "Espanol"),
    };

    private void ApplyLanguage()
    {
        var index = Mathf.Clamp(_language.Selected, 0, LanguageChoices.Length - 1);
        var chosen = LanguageChoices[index].Language;
        if (chosen == Config.Language)
            return;

        Config.SetLanguage(chosen);
        Net.I?.SendLanguage(chosen);
    }

    private void BuildDisplayTab(VBoxContainer vb)
    {
        vb.AddChild(Row("Language", _language = new OptionButton()));
        foreach (var (language, label) in LanguageChoices)
            _language.AddItem(label, (int)language);
        _language.Selected = Array.FindIndex(LanguageChoices, c => c.Language == Config.Language);
        if (_language.Selected < 0)
            _language.Selected = 0;

        var screenRow = Row("Monitor", _screen = new OptionButton());
        screenRow.Visible = Platform.PointerUi;
        vb.AddChild(screenRow);
        _screen.AddItem("Current", Config.CurrentScreen);
        for (int s = 0; s < DisplayServer.GetScreenCount(); s++)
        {
            var size = DisplayServer.ScreenGetSize(s);
            _screen.AddItem($"Monitor {s + 1}  ({size.X} x {size.Y})", s);
        }
        _screen.Selected = Mathf.Max(0, _screen.GetItemIndex(Config.Screen));
        _screen.ItemSelected += _ => FillResolutions(_screen.GetSelectedId());

        var windowRow = Row("Window", _mode = new OptionButton());
        windowRow.Visible = Platform.PointerUi;
        vb.AddChild(windowRow);
        _mode.AddItem("Windowed", 0);
        _mode.AddItem("Borderless Fullscreen", 1);
        _mode.AddItem("Fullscreen", 2);
        _mode.Selected = (int)Config.WindowMode;
        _mode.ItemSelected += _ => _res.Disabled = _mode.Selected != 0;

        var resRow = Row("Resolution", _res = new OptionButton());
        resRow.Visible = Platform.PointerUi;
        vb.AddChild(resRow);
        FillResolutions(Config.Screen);
        _res.Disabled = Config.WindowMode != Config.VideoMode.Windowed;

        _uiScale = new HSlider
        {
            MinValue = Config.UiScaleMin, MaxValue = Config.UiScaleMax, Step = Config.UiScaleStep,
            Value = Config.EffectiveUiScale, CustomMinimumSize = new Vector2(0, 18),
        };
        var scaleValue = new Label
        {
            Text = UiScaleText(Config.EffectiveUiScale),
            CustomMinimumSize = new Vector2(52, 0),
        };
        _uiScale.ValueChanged += v => scaleValue.Text = UiScaleText((float)v);
        var scaleRow = Row("UI Size", _uiScale);
        scaleRow.AddChild(scaleValue);
        scaleRow.TooltipText = "Scales every menu, window and HUD element. Larger values suit small or touch screens.";
        vb.AddChild(scaleRow);

        _nameScale = new HSlider
        {
            MinValue = Config.NamePlateScaleMin, MaxValue = Config.NamePlateScaleMax, Step = Config.NamePlateScaleStep,
            Value = Config.NamePlateScale, CustomMinimumSize = new Vector2(0, 18),
        };
        var nameScaleValue = new Label
        {
            Text = UiScaleText(Config.NamePlateScale),
            CustomMinimumSize = new Vector2(52, 0),
        };
        _nameScale.ValueChanged += v => nameScaleValue.Text = UiScaleText((float)v);
        var nameScaleRow = Row("Name Size", _nameScale);
        nameScaleRow.AddChild(nameScaleValue);
        nameScaleRow.TooltipText = "Size of the names over characters, monsters and NPCs.";
        vb.AddChild(nameScaleRow);

        _hotbarScale = new HSlider
        {
            MinValue = HotbarLayout.ScaleMin, MaxValue = HotbarLayout.ScaleMax, Step = HotbarLayout.ScaleStep,
            Value = Config.HotbarScale, CustomMinimumSize = new Vector2(0, 18),
        };
        var hotbarScaleValue = new Label { Text = UiScaleText(Config.HotbarScale), CustomMinimumSize = new Vector2(52, 0) };
        _hotbarScale.ValueChanged += v => hotbarScaleValue.Text = UiScaleText((float)v);
        var hotbarScaleRow = Row("Skill Bar Size", _hotbarScale);
        hotbarScaleRow.AddChild(hotbarScaleValue);
        hotbarScaleRow.TooltipText = "Size of the skill bars.";
        vb.AddChild(hotbarScaleRow);

        _hotbarGap = new HSlider
        {
            MinValue = HotbarLayout.GapMin, MaxValue = HotbarLayout.GapMax, Step = 1,
            Value = Config.HotbarGap, CustomMinimumSize = new Vector2(0, 18),
        };
        var hotbarGapValue = new Label { Text = $"{Config.HotbarGap} px", CustomMinimumSize = new Vector2(52, 0) };
        _hotbarGap.ValueChanged += v => hotbarGapValue.Text = $"{(int)v} px";
        var hotbarGapRow = Row("Skill Bar Gap", _hotbarGap);
        hotbarGapRow.AddChild(hotbarGapValue);
        hotbarGapRow.TooltipText = "Space between the slots of the skill bars.";
        vb.AddChild(hotbarGapRow);

        var resetLayout = new Button { Text = "Reset window positions", FocusMode = Control.FocusModeEnum.None };
        resetLayout.Pressed += () => Notice.Confirm(this, "Move every window back to its default place?", "Reset", "Cancel",
            HudLayout.ResetAll, title: "Windows");
        var resetRow = Row("Windows", resetLayout);
        resetRow.TooltipText = "Forgets every moved or resized window and puts it back where it starts.";
        vb.AddChild(resetRow);

        var vsyncRow = Row("V-Sync", _vsync = new CheckButton { ButtonPressed = Config.VSync });
        vsyncRow.Visible = Platform.PointerUi;
        vsyncRow.TooltipText = "Caps the frame rate to your monitor's refresh rate. Turn it off to see "
                               + "what the other graphics settings are actually doing.";
        vb.AddChild(vsyncRow);

        vb.AddChild(Row("FPS Limit", _fps = new OptionButton()));
        foreach (var (label, id) in new[] { ("Unlimited", 0), ("30", 1), ("60", 2), ("120", 3), ("144", 4), ("240", 5) })
            _fps.AddItem(label, id);
        _fps.Selected = (int)Config.FpsLimit;

        var titlesRow = Row("Show Titles", _showTitles = new CheckButton { ButtonPressed = Config.ShowTitles });
        titlesRow.TooltipText = "Achievement titles under the names of characters.";
        vb.AddChild(titlesRow);

        RestoreButton(vb, () =>
        {
            _screen.Selected = 0;
            _mode.Selected = (int)Config.DefaultOf<Config.VideoMode>(nameof(Config.WindowMode), "video", "mode");
            _uiScale.Value = Config.DefaultOf<float>(nameof(Config.UiScale), "video", "ui_scale");
            _nameScale.Value = Config.DefaultOf<float>(nameof(Config.NamePlateScale), "video", "name_plate_scale");
            _hotbarScale.Value = Config.DefaultOf<float>(nameof(Config.HotbarScale), "hotbar", "scale");
            _hotbarGap.Value = Config.DefaultOf<int>(nameof(Config.HotbarGap), "hotbar", "gap");
            _vsync.ButtonPressed = Config.DefaultOf<bool>(nameof(Config.VSync), "video", "vsync");
            _fps.Selected = (int)Config.DefaultOf<Config.FpsCap>(nameof(Config.FpsLimit), "graphics", "fps_limit");
            _showTitles.ButtonPressed = Config.DefaultOf<bool>(nameof(Config.ShowTitles), "hud", "show_titles");
        });
    }

    private void BuildGraphicsTab(VBoxContainer vb)
    {
        var presetRow = Row("Quality", _preset = new OptionButton());
        foreach (var preset in GraphicsPresets.Named)
            _preset.AddItem(preset.ToString(), (int)preset);
        _preset.AddItem("Custom", (int)GraphicsPreset.Custom);
        _preset.ItemSelected += index => ApplyPreset((GraphicsPreset)_preset.GetItemId((int)index));
        presetRow.TooltipText = "Sets shadows, occlusion, fog, bloom, clouds, anti-aliasing and view distance together.";
        vb.AddChild(presetRow);
        _viewDistance = new HSlider
        {
            MinValue = Config.ViewDistanceMin, MaxValue = Config.ViewDistanceMax, Step = 0.05,
            Value = Config.ViewDistance, CustomMinimumSize = new Vector2(0, 18),
        };
        var viewValue = new Label
        {
            Text = ViewDistanceText(Config.ViewDistance),
            CustomMinimumSize = new Vector2(52, 0),
        };
        _viewDistance.ValueChanged += v => viewValue.Text = ViewDistanceText((float)v);
        var viewRow = Row("View Distance", _viewDistance);
        viewRow.AddChild(viewValue);
        viewRow.TooltipText = "How far terrain, objects and characters draw. Lower values run faster on phones.";
        vb.AddChild(viewRow);

        _brightness = LightRow(vb, "Brightness", LightSliders.Brightness, Config.Brightness);
        _contrast = LightRow(vb, "Contrast", LightSliders.Contrast, Config.Contrast);
        _nightBrightness = LightRow(vb, "Night Brightness", LightSliders.NightBrightness, Config.NightBrightness);
        _fog = LightRow(vb, "Fog", LightSliders.Fog, Config.Fog);

        vb.AddChild(Row("Shadows", _shadowLevel = LevelPicker(Config.ShadowQuality)));
        vb.AddChild(Row("Ambient Occlusion", _ssaoLevel = LevelPicker(Config.SsaoQuality)));
        vb.AddChild(Row("Volumetric Fog", _volFog = new CheckButton { ButtonPressed = Config.VolumetricFog }));
        vb.AddChild(Row("Bloom", _bloom = new CheckButton { ButtonPressed = Config.Bloom }));
        var fxLayerRow = Row("Accurate Effect Blending", _fxLayer = new CheckButton { ButtonPressed = Config.FxLayer });
        fxLayerRow.TooltipText = "Blends spells and effects the way the original client does, outside bloom and tone mapping.";
        vb.AddChild(fxLayerRow);
        vb.AddChild(Row("Clouds", _clouds = new CheckButton { ButtonPressed = Config.Clouds }));
        var capeRow = Row("Clan Capes", _capes = new CheckButton { ButtonPressed = Config.Capes });
        capeRow.TooltipText = "Simulated cloth on every player in range. Costs a lot of frame time.";
        vb.AddChild(capeRow);
        vb.AddChild(Row("Anti-Aliasing", _aa = new OptionButton()));
        foreach (var (label, id) in new[] { ("Off", 0), ("FXAA", 1), ("MSAA 2x", 2), ("MSAA 4x", 3) })
            _aa.AddItem(label, id);
        _aa.Selected = (int)Config.AntiAlias;

        var upscaleRow = Row("Upscaling", _upscale = new OptionButton());
        foreach (var (label, id) in new[] { ("Off", 0), ("FSR 1.0", 1), ("FSR 2.2", 2) })
            _upscale.AddItem(label, id);
        _upscale.Selected = (int)Config.Upscale;
        _upscale.Disabled = !Config.UpscaleSupported;
        upscaleRow.TooltipText = Config.UpscaleSupported
            ? "Renders the world below screen resolution and upscales it. The interface stays sharp. "
              + "FSR 2.2 is sharper but can smear fast-moving effects."
            : "Not available on this renderer.";
        vb.AddChild(upscaleRow);

        var qualityRow = Row("Upscale Quality", _upscaleQuality = new OptionButton());
        foreach (var (label, id) in new[]
                 {
                     ("Ultra Quality", 0), ("Quality", 1), ("Balanced", 2),
                     ("Performance", 3), ("Ultra Performance", 4),
                 })
            _upscaleQuality.AddItem(label, id);
        _upscaleQuality.Selected = (int)Config.UpscaleQuality;
        var internalRes = new Label { CustomMinimumSize = new Vector2(120, 0) };
        void ShowInternalRes()
        {
            var target = GetWindow().Size;
            float s = Config.ScaleFor((Config.UpscaleLevel)_upscaleQuality.Selected);
            internalRes.Text = _upscaleQuality.Disabled
                ? "—"
                : $"{target.X * s:0} x {target.Y * s:0}";
        }
        _upscaleQuality.ItemSelected += _ => ShowInternalRes();
        _upscale.ItemSelected += i =>
        {
            _upscaleQuality.Disabled = i == (long)Config.UpscaleMode.Off || !Config.UpscaleSupported;
            ShowInternalRes();
        };
        _upscaleQuality.Disabled = Config.Upscale == Config.UpscaleMode.Off || !Config.UpscaleSupported;
        ShowInternalRes();
        qualityRow.AddChild(internalRes);
        qualityRow.TooltipText = "How far below screen resolution the world is drawn before upscaling.";
        vb.AddChild(qualityRow);

        var rendererRow = Row("Renderer", _renderer = new OptionButton());
        _renderer.AddItem("Direct3D 12", (int)Config.RenderDriver.Direct3D12);
        _renderer.AddItem("Vulkan", (int)Config.RenderDriver.Vulkan);
        _renderer.Selected = (int)Config.Renderer;
        rendererRow.TooltipText = $"The graphics API. Changing it needs a restart. Running now: {Config.RunningDriver}.";
        rendererRow.Visible = OS.GetName() == "Windows";
        vb.AddChild(rendererRow);

        _preset.Selected = (int)GraphicsPresets.Match(CurrentGraphics());
        _shadowLevel.ItemSelected += _ => RefreshPreset();
        _ssaoLevel.ItemSelected += _ => RefreshPreset();
        _volFog.Toggled += _ => RefreshPreset();
        _bloom.Toggled += _ => RefreshPreset();
        _clouds.Toggled += _ => RefreshPreset();
        _aa.ItemSelected += _ => RefreshPreset();
        _viewDistance.ValueChanged += _ => RefreshPreset();

        RestoreButton(vb, () =>
        {
            ApplyPreset(GraphicsPreset.High);
            _fxLayer.ButtonPressed = Config.DefaultOf<bool>(nameof(Config.FxLayer), "graphics", "fx_layer");
            _capes.ButtonPressed = Config.DefaultOf<bool>(nameof(Config.Capes), "graphics", "capes");
            _upscale.Selected = (int)Config.DefaultOf<Config.UpscaleMode>(nameof(Config.Upscale), "graphics", "upscale");
            _upscaleQuality.Selected = (int)Config.DefaultOf<Config.UpscaleLevel>(nameof(Config.UpscaleQuality), "graphics", "upscale_quality");
            _brightness.Value = LightRange.Neutral;
            _contrast.Value = LightRange.Neutral;
            _nightBrightness.Value = LightRange.Neutral;
            _fog.Value = LightRange.Neutral;
            _renderer.Selected = (int)Config.RenderDriver.Direct3D12;
        });
    }

    private void BuildEffectsTab(VBoxContainer vb)
    {
        vb.AddChild(Row("World Effects", _fxAmbient = new CheckButton { ButtonPressed = Config.FxAmbient }));
        _fxDistance = new HSlider
        {
            MinValue = 40, MaxValue = 400, Step = 10, Value = Config.FxDistance,
            CustomMinimumSize = new Vector2(0, 18),
        };
        var dist = new Label { Text = $"{Config.FxDistance} m", CustomMinimumSize = new Vector2(52, 0) };
        _fxDistance.ValueChanged += v => dist.Text = $"{(int)v} m";
        var distRow = Row("Effect Distance", _fxDistance);
        distRow.AddChild(dist);
        vb.AddChild(distRow);
        vb.AddChild(Row("Damage Numbers", _fxNumbers = new CheckButton { ButtonPressed = Config.DamageNumbers }));
        vb.AddChild(Row("Combat Log", _fxCombatLog = new CheckButton { ButtonPressed = Config.CombatLog }));

        vb.AddChild(new HSeparator());
        vb.AddChild(new Label { Text = "Other players" });
        foreach (var (kind, label) in CospreChoices)
        {
            var shown = new CheckButton { ButtonPressed = (Config.HiddenCospre & kind) == 0 };
            _cospreShown[kind] = shown;
            var row = Row($"Show {label}", shown);
            row.TooltipText = "Hides this kind of costume piece on other players to save frame time; yours always shows.";
            vb.AddChild(row);
        }

        RestoreButton(vb, () =>
        {
            _fxAmbient.ButtonPressed = Config.DefaultOf<bool>(nameof(Config.FxAmbient), "effects", "ambient");
            _fxDistance.Value = Config.DefaultOf<int>(nameof(Config.FxDistance), "effects", "distance");
            _fxNumbers.ButtonPressed = Config.DefaultOf<bool>(nameof(Config.DamageNumbers), "effects", "damage_numbers");
            _fxCombatLog.ButtonPressed = Config.DefaultOf<bool>(nameof(Config.CombatLog), "hud", "combat_log");
            foreach (var check in _cospreShown.Values) check.ButtonPressed = true;
        });
    }

    private void BuildControlsTab(VBoxContainer vb)
    {
        _camTurnSpeed = new HSlider
        {
            MinValue = Config.CamTurnSpeedMin, MaxValue = Config.CamTurnSpeedMax, Step = 10,
            Value = Config.CamTurnSpeed, CustomMinimumSize = new Vector2(0, 18),
        };
        var turnValue = new Label { Text = TurnSpeedText(Config.CamTurnSpeed), CustomMinimumSize = new Vector2(52, 0) };
        _camTurnSpeed.ValueChanged += v => turnValue.Text = TurnSpeedText((float)v);
        var turnRow = Row("Camera Turn Speed", _camTurnSpeed);
        turnRow.AddChild(turnValue);
        turnRow.TooltipText = "How fast the middle-mouse click spins the camera a half turn.";
        turnRow.Visible = Platform.PointerUi;
        vb.AddChild(turnRow);

        var shakeRow = Row("Camera Shake", _cameraShake = new CheckButton { ButtonPressed = Config.CameraShake });
        shakeRow.TooltipText = "Let gates, rolling stones and rockslides shake the camera.";
        vb.AddChild(shakeRow);

        var edgeRow = Row("Screen Edge Panning", _camEdgePan = new CheckButton { ButtonPressed = Config.CamEdgePan });
        edgeRow.TooltipText = "In fullscreen, hold the cursor against the left or right border to turn the camera.";
        edgeRow.Visible = Platform.PointerUi;
        vb.AddChild(edgeRow);

        _camEdgeSpeed = new HSlider
        {
            MinValue = Config.CamEdgePanSpeedMin, MaxValue = Config.CamEdgePanSpeedMax, Step = 1,
            Value = Config.CamEdgePanSpeed, CustomMinimumSize = new Vector2(0, 18),
        };
        var edgeValue = new Label { Text = TurnSpeedText(Config.CamEdgePanSpeed), CustomMinimumSize = new Vector2(52, 0) };
        _camEdgeSpeed.ValueChanged += v => edgeValue.Text = TurnSpeedText((float)v);
        var edgeSpeedRow = Row("Edge Pan Speed", _camEdgeSpeed);
        edgeSpeedRow.AddChild(edgeValue);
        edgeSpeedRow.TooltipText = $"The original client turns at {Config.CamEdgePanSpeedRetail:0} °/s.";
        edgeSpeedRow.Visible = Platform.PointerUi;
        vb.AddChild(edgeSpeedRow);

        if (!Platform.PointerUi)
        {
            vb.AddChild(StickRow("Move Stick", out _moveStick, Config.MoveStickSensitivity,
                                 "How far the left stick pushes before the character runs flat out."));
            vb.AddChild(StickRow("Look Stick", out _lookStick, Config.LookStickSensitivity,
                                 "How fast the right stick swings the camera."));
        }

        RestoreButton(vb, () =>
        {
            _camTurnSpeed.Value = Config.DefaultOf<float>(nameof(Config.CamTurnSpeed), "controls", "cam_turn_speed");
            _cameraShake.ButtonPressed = Config.DefaultOf<bool>(nameof(Config.CameraShake), "controls", "camera_shake");
            _camEdgePan.ButtonPressed = Config.DefaultOf<bool>(nameof(Config.CamEdgePan), "controls", "cam_edge_pan");
            _camEdgeSpeed.Value = Config.DefaultOf<float>(nameof(Config.CamEdgePanSpeed), "controls", "cam_edge_pan_speed");
            if (_moveStick != null) _moveStick.Value = Config.DefaultOf<float>(nameof(Config.MoveStickSensitivity), "controls", "move_stick");
            if (_lookStick != null) _lookStick.Value = Config.DefaultOf<float>(nameof(Config.LookStickSensitivity), "controls", "look_stick");
        });
    }

    private static HSlider LightRow(VBoxContainer vb, string label, LightRange range, int value)
    {
        var slider = new HSlider
        {
            MinValue = range.Min, MaxValue = range.Max, Step = LightRange.Step,
            Value = value, CustomMinimumSize = new Vector2(0, 18),
        };
        var shown = new Label { Text = $"{value} %", CustomMinimumSize = new Vector2(52, 0) };
        slider.ValueChanged += v => shown.Text = $"{(int)v} %";
        var row = Row(label, slider);
        row.AddChild(shown);
        vb.AddChild(row);
        return slider;
    }

    private static HBoxContainer StickRow(string label, out HSlider slider, float value, string hint)
    {
        slider = new HSlider
        {
            MinValue = Config.StickSensitivityMin,
            MaxValue = Config.StickSensitivityMax,
            Step = Config.StickSensitivityStep,
            Value = value,
            CustomMinimumSize = new Vector2(0, 18),
        };
        var readout = new Label { Text = $"{value * 100f:F0}%", CustomMinimumSize = new Vector2(52, 0) };
        var bound = slider;
        slider.ValueChanged += v => readout.Text = $"{v * 100f:F0}%";
        var row = Row(label, bound);
        row.AddChild(readout);
        row.TooltipText = hint;
        return row;
    }

    private static string TurnSpeedText(float degreesPerSecond) => $"{degreesPerSecond:0} °/s";

    private void BuildSoundTab(VBoxContainer vb)
    {
        vb.AddChild(Row("Enabled", _sndOn = new CheckButton { ButtonPressed = Config.AudioEnabled }));
        vb.AddChild(Row("Other players' sounds", _sndOthers = new CheckButton { ButtonPressed = Config.OtherPlayersSounds }));
        vb.AddChild(VolumeRow("Master", Audio.BusMaster, out _volMaster, Config.MasterVolume));
        vb.AddChild(VolumeRow("Music", Audio.BusMusic, out _volMusic, Config.MusicVolume));
        vb.AddChild(VolumeRow("Sound effects", Audio.BusSfx, out _volSfx, Config.SfxVolume));
        vb.AddChild(VolumeRow("Interface sounds", Audio.BusUi, out _volUi, Config.UiVolume));
        vb.AddChild(VolumeRow("Voice", Audio.BusVoice, out _volVoice, Config.VoiceVolume));

        RestoreButton(vb, () =>
        {
            _sndOn.ButtonPressed = Config.DefaultOf<bool>(nameof(Config.AudioEnabled), "audio", "enabled");
            _sndOthers.ButtonPressed = Config.DefaultOf<bool>(nameof(Config.OtherPlayersSounds), "audio", "other_players");
            _volMaster.Value = Config.DefaultOf<float>(nameof(Config.MasterVolume), "audio", "master");
            _volMusic.Value = Config.DefaultOf<float>(nameof(Config.MusicVolume), "audio", "music");
            _volSfx.Value = Config.DefaultOf<float>(nameof(Config.SfxVolume), "audio", "sfx");
            _volUi.Value = Config.DefaultOf<float>(nameof(Config.UiVolume), "audio", "ui");
            _volVoice.Value = Config.DefaultOf<float>(nameof(Config.VoiceVolume), "audio", "voice");
        });
    }

    public override void _Input(InputEvent ev)
    {
        if (ev is InputEventJoypadButton { Pressed: true } pad)
        {
            if (CapturePadButton(pad)) GetViewport().SetInputAsHandled();
            return;
        }
        if (ev is InputEventKey { Pressed: false } released)
        {
            if (CaptureModifierRelease(released)) GetViewport().SetInputAsHandled();
            return;
        }
        if (ev is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (CaptureBindKey(key)) { GetViewport().SetInputAsHandled(); return; }
        if (key.Keycode != Key.Escape) return;
        GetViewport().SetInputAsHandled();
        QueueFree();
    }

    private static VBoxContainer Tab(TabContainer tabs, string name)
    {
        var scroll = new ScrollContainer { Name = name, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        tabs.AddChild(scroll);
        var margin = new MarginContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (var s in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride($"margin_{s}", 10);
        scroll.AddChild(margin);
        var vb = new VBoxContainer { CustomMinimumSize = new Vector2(Platform.Pick(TabWidth, 0), 0) };
        vb.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        vb.AddThemeConstantOverride("separation", 12);
        margin.AddChild(vb);
        return vb;
    }

    private static HBoxContainer VolumeRow(string label, string bus, out HSlider slider, float value)
    {
        slider = new HSlider
        {
            MinValue = 0, MaxValue = 1, Step = 0.05, Value = value,
            CustomMinimumSize = new Vector2(0, 18),
        };
        var pct = new Label { Text = $"{value * 100:0}%", CustomMinimumSize = new Vector2(52, 0) };
        slider.ValueChanged += v =>
        {
            pct.Text = $"{v * 100:0}%";
            Audio.PreviewVolume(bus, (float)v);
        };
        var row = Row(label, slider);
        row.AddChild(pct);
        return row;
    }

    private static string UiScaleText(float scale) => $"{scale * 100f:F0}%";

    private static string ViewDistanceText(float scale) => $"{scale * 100f:F0}%";

    private static HBoxContainer Row(string label, Control control)
    {
        var hb = new HBoxContainer();
        hb.AddThemeConstantOverride("separation", 10);
        hb.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(110, 0) });
        control.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        hb.AddChild(control);
        return hb;
    }
}
