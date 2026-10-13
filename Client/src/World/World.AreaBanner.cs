using System.Collections.Generic;
using Godot;
using LibreKO.Domain;
using FileAccess = Godot.FileAccess;

namespace LibreKO;

public partial class World
{
    private const string AreaFile = "res://assets/terrain/{0}/areas.json";
    private const int AreaTitleFontSize = 26;
    private const int AreaSubtitleFontSize = 20;
    private const float AreaBannerTop = 100f;
    private const float AreaSubtitleDrop = 50f;
    private const char AreaSubtitleSeparator = '/';
    private const double AreaNameHold = 1.0;
    private const double AreaNoticeHold = 3.0;
    private const double AreaNameFadeIn = 0.5;
    private const double AreaNoticeFadeIn = 1.5;
    private const double AreaFadeOut = 1.0;
    private const double AreaHintHold = 5.0;
    private const float AreaHintHeight = 0.4f;
    private static readonly Color AreaBannerColour = new(1f, 1f, 0f);
    private static readonly Color AreaWarningColour = new(1f, 0f, 0f);

    private readonly record struct AreaRect(int Id, float MinX, float MaxX, float MinZ, float MaxZ)
    {
        public bool Contains(float x, float z) => x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;
    }

    private readonly List<AreaRect> _areas = new();
    private readonly List<int> _areasHere = new();
    private readonly HashSet<int> _areasBefore = new();
    private int _areasZone = -1;
    private Control? _areaBanner;
    private Label _areaTitle = null!, _areaSubtitle = null!;
    private Tween? _areaTween;

    private void AreaBannerTick()
    {
        if (_zone != _areasZone) LoadAreas(_zone);
        if (_areas.Count == 0) return;

        _areasHere.Clear();
        foreach (var area in _areas)
            if (area.Contains(_myKoX, _myKoZ) && !_areasHere.Contains(area.Id)) _areasHere.Add(area.Id);
        int entered = AreaNames.Entered(_areasHere, _areasBefore);
        if (entered is AreaNames.Unchanged or AreaNames.Left) return;
        if (ShowRoyaleAreaCaption(entered)) return;

        var notice = AreaNames.NoticeFor(_zone, entered);
        string text = notice.TextId > 0 ? ItemData.Text(notice.TextId, "") : "";
        if (text.Length == 0) return;
        switch (notice.Kind)
        {
            case AreaNoticeKind.Log:
                CombatLogAdd(text, CombatLogKind.Status, AreaBannerColour);
                break;
            case AreaNoticeKind.Banner:
            case AreaNoticeKind.WarningBanner:
                ShowAreaBanner(text, notice.Kind == AreaNoticeKind.WarningBanner ? AreaWarningColour : AreaBannerColour,
                    notice.LongHold ? AreaNoticeHold : AreaNameHold, notice.LongHold ? AreaNoticeFadeIn : AreaNameFadeIn,
                    AreaBannerTop);
                break;
            case AreaNoticeKind.Hint:
                ShowAreaBanner(text, AreaBannerColour, AreaHintHold, AreaNoticeFadeIn,
                    GetViewport().GetVisibleRect().Size.Y * AreaHintHeight);
                break;
        }
    }

    private void LoadAreas(int zone)
    {
        _areasZone = zone;
        _areas.Clear();
        _areasBefore.Clear();
        if (ZoneCatalog.Stem(zone) is not { } stem) return;
        string path = string.Format(AreaFile, stem);
        if (!FileAccess.FileExists(path)) return;
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null || Json.ParseString(file.GetAsText()).AsGodotArray() is not { } rows) return;
        foreach (var row in rows)
        {
            var d = row.AsGodotDictionary();
            _areas.Add(new AreaRect(d["id"].AsInt32(), d["minX"].AsSingle(), d["maxX"].AsSingle(),
                d["minZ"].AsSingle(), d["maxZ"].AsSingle()));
        }
    }

    private void ShowAreaBanner(string text, Color colour, double hold, double fadeIn, float top)
    {
        EnsureAreaBanner();
        _areaTitle.OffsetTop = top;
        _areaSubtitle.OffsetTop = top + AreaSubtitleDrop;
        int split = text.IndexOf(AreaSubtitleSeparator);
        _areaTitle.Text = split < 0 ? text : text[..split];
        _areaSubtitle.Text = split < 0 ? "" : text[(split + 1)..];
        _areaSubtitle.Visible = _areaSubtitle.Text.Length > 0;
        _areaTitle.AddThemeColorOverride("font_color", colour);
        _areaSubtitle.AddThemeColorOverride("font_color", colour);

        var banner = _areaBanner!;
        _areaTween?.Kill();
        banner.Modulate = new Color(1f, 1f, 1f, 0f);
        banner.Visible = true;
        _areaTween = CreateTween();
        _areaTween.TweenProperty(banner, "modulate:a", 1f, fadeIn);
        _areaTween.TweenInterval(hold);
        _areaTween.TweenProperty(banner, "modulate:a", 0f, AreaFadeOut);
        _areaTween.TweenCallback(Callable.From(() => banner.Visible = false));
    }

    private void EnsureAreaBanner()
    {
        if (_areaBanner != null && IsInstanceValid(_areaBanner)) return;
        var layer = new CanvasLayer { Layer = 70 };
        AddChild(layer);
        _areaBanner = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _areaBanner.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        layer.AddChild(_areaBanner);
        _areaTitle = AreaBannerLine(AreaTitleFontSize, AreaBannerTop);
        _areaSubtitle = AreaBannerLine(AreaSubtitleFontSize, AreaBannerTop + AreaSubtitleDrop);
        _areaBanner.AddChild(_areaTitle);
        _areaBanner.AddChild(_areaSubtitle);
    }

    private static Label AreaBannerLine(int fontSize, float top)
    {
        var label = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        label.OffsetTop = top;
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 6);
        return label;
    }
}
