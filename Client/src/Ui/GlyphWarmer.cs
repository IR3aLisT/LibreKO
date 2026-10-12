using System.Collections.Generic;
using System.Diagnostics;
using Godot;

namespace LibreKO;

internal static class GlyphWarmer
{
    private const int UnscaledLevel = 64;
    private const double MinOversampling = 0.1;
    private const double MaxOversampling = 100.0;
    private const double RewarmFrameBudgetMs = 4.0;

    private static readonly (long First, long Last)[] TextRanges = { (0x20, 0x7E), (0xA0, 0xFF), (0x2013, 0x2026) };
    internal static readonly (long First, long Last)[] AsciiRange = { (0x20, 0x7E) };

    private static readonly Dictionary<int, Dictionary<(Rid Font, Vector2I Size), (long First, long Last)[]>> _warmed = new();
    private static readonly HashSet<(Font Font, int Size, int Outline, (long First, long Last)[] Ranges)> _requests = new();
    private static readonly Queue<(Font Font, int Size, int Outline, (long First, long Last)[] Ranges)> _rewarm = new();
    private static readonly HashSet<Rid> _safeUploads = new();
    private static readonly HashSet<(Font, int, int, int, bool)> _scratch = new();
    private static SceneTree? _tree;
    private static int _rootLevel = UnscaledLevel;
    private static bool _draining;

    internal static void Watch(SceneTree tree)
    {
        if (_tree != null) return;
        _tree = tree;
        _rootLevel = LevelOf(tree.Root);
        tree.NodeAdded += OnNodeAdded;
        tree.Root.SizeChanged += OnRootSizeChanged;
    }

    internal static int Level(float oversampling) =>
        (int)(Math.Clamp(oversampling, MinOversampling, MaxOversampling) * UnscaledLevel);

    internal static int RasterSize(int size, int level) =>
        size * level % UnscaledLevel == 0 ? size * level / UnscaledLevel : 0;

    internal static int RasterOutline(int outline, int level) =>
        (int)(outline * (float)((double)level / UnscaledLevel));

    internal static int LevelOf(Node node) =>
        node.GetViewport() is { } viewport ? Level(viewport.GetOversampling()) : UnscaledLevel;

    private static void OnNodeAdded(Node node)
    {
        if (node is not (Label or Button or LineEdit or RichTextLabel or Label3D)) return;
        _scratch.Clear();
        AddFor(node, _scratch);
        foreach (var (font, size, outline, level, spatial) in _scratch) Render(font, size, outline, TextRanges, level, spatial);
    }

    private static void OnRootSizeChanged()
    {
        if (_tree == null) return;
        int level = LevelOf(_tree.Root);
        if (level == _rootLevel) return;
        _rootLevel = level;
        Forget();
        _requests.RemoveWhere(r => !GodotObject.IsInstanceValid(r.Font));
        _rewarm.Clear();
        foreach (var request in _requests) _rewarm.Enqueue(request);
        if (_draining) return;
        _draining = true;
        _tree.ProcessFrame += Rewarm;
    }

    private static void Rewarm()
    {
        long until = Stopwatch.GetTimestamp() + (long)(RewarmFrameBudgetMs * Stopwatch.Frequency / 1000.0);
        while (Stopwatch.GetTimestamp() < until && _rewarm.TryDequeue(out var r))
            Render(r.Font, r.Size, r.Outline, r.Ranges, _rootLevel);
        if (_rewarm.Count > 0 || _tree == null) return;
        _tree.ProcessFrame -= Rewarm;
        _draining = false;
    }

    private static void Forget()
    {
        var server = TextServerManager.GetPrimaryInterface();
        foreach (int level in new List<int>(_warmed.Keys))
        {
            if (level == UnscaledLevel || level == _rootLevel) continue;
            foreach (var key in _warmed[level].Keys)
                if (server.Has(key.Font) && !WarmedAt(UnscaledLevel, key) && !WarmedAt(_rootLevel, key))
                    server.FontRemoveSizeCache(key.Font, key.Size);
            _warmed.Remove(level);
        }
    }

    private static bool WarmedAt(int level, (Rid, Vector2I) key) =>
        _warmed.TryGetValue(level, out var keys) && keys.ContainsKey(key);

    internal static int Warm(Node root)
    {
        long started = Stopwatch.GetTimestamp();
        var wanted = new HashSet<(Font, int, int, int, bool)>();
        Collect(root, wanted);
        int rendered = 0;
        foreach (var (font, size, outline, level, spatial) in wanted) rendered += Render(font, size, outline, TextRanges, level, spatial);
        if (rendered > 0)
            GD.Print($"[glyphs] {root.Name}: {rendered} glyph caches in {(Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency:0} ms");
        return rendered;
    }

    internal static int Render(Font? font, int size, int outline, (long First, long Last)[] ranges, int level, bool spatial = false)
    {
        if (font == null || size <= 0 || !GodotObject.IsInstanceValid(font)) return 0;
        if (!spatial && level == _rootLevel) _requests.Add((font, size, outline, ranges));
        var server = TextServerManager.GetPrimaryInterface();
        var rids = font.GetRids();
        foreach (var rid in rids)
            if (_safeUploads.Add(rid) && !server.FontGetGenerateMipmaps(rid)) server.FontSetGenerateMipmaps(rid, true);

        int raster = RasterSize(size, level);
        if (raster == 0) return 0;
        var key = new Vector2I(raster, RasterOutline(outline, level));
        if (!_warmed.TryGetValue(level, out var warmed)) _warmed[level] = warmed = new();
        int rendered = 0;
        foreach (var rid in rids)
        {
            if (warmed.TryGetValue((rid, key), out var done) && (done == ranges || done == TextRanges)) continue;
            warmed[(rid, key)] = ranges;
            foreach (var (first, last) in ranges) server.FontRenderRange(rid, key, first, last);
            rendered++;
        }
        return rendered;
    }

    private static void Add(HashSet<(Font, int, int, int, bool)> wanted, Font? font, int size, int outline, int shadowOutline,
        int level, bool spatial = false)
    {
        if (font == null || size <= 0) return;
        wanted.Add((font, size, 0, level, spatial));
        if (outline > 0) wanted.Add((font, size, outline, level, spatial));
        if (shadowOutline > 0) wanted.Add((font, size, shadowOutline, level, spatial));
    }

    private static int ShadowOutline(Control control) =>
        control.GetThemeColor("font_shadow_color").A > 0f ? control.GetThemeConstant("shadow_outline_size") : 0;

    private static void AddFor(Node node, HashSet<(Font, int, int, int, bool)> wanted)
    {
        switch (node)
        {
            case Label { LabelSettings: { } settings } label:
                Add(wanted, settings.Font ?? label.GetThemeFont("font"), settings.FontSize, settings.OutlineSize,
                    settings.ShadowColor.A > 0f ? settings.ShadowSize : 0, LevelOf(node));
                break;
            case Label or Button or LineEdit:
                var control = (Control)node;
                Add(wanted, control.GetThemeFont("font"), control.GetThemeFontSize("font_size"),
                    control.GetThemeConstant("outline_size"), ShadowOutline(control), LevelOf(node));
                break;
            case RichTextLabel rich:
                int richOutline = rich.GetThemeConstant("outline_size");
                int level = LevelOf(node);
                Add(wanted, rich.GetThemeFont("normal_font"), rich.GetThemeFontSize("normal_font_size"), richOutline, 0, level);
                Add(wanted, rich.GetThemeFont("bold_font"), rich.GetThemeFontSize("bold_font_size"), richOutline, 0, level);
                break;
            case Label3D plate:
                Add(wanted, plate.Font ?? ThemeDB.FallbackFont, plate.FontSize, plate.OutlineSize, 0, UnscaledLevel, spatial: true);
                break;
        }
    }

    private static void Collect(Node node, HashSet<(Font, int, int, int, bool)> wanted)
    {
        AddFor(node, wanted);
        foreach (var child in node.GetChildren()) Collect(child, wanted);
    }
}
