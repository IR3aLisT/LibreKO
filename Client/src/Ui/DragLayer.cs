using Godot;
using LibreKO.Domain;

namespace LibreKO;

public static class DragLayer
{
    public const string AmountKey = "amount";
    private const int TopLayer = 230;
    private const string LayerName = "DragLayer";
    private const int CountFontSize = 12;

    public static void ShowItem(Control source, int itemId, Vector2 grabAt) =>
        Show(source, Preview(source, itemId, grabAt, out _));

    public static void ShowStack(Control source, ItemSlot item, Vector2 grabAt, Godot.Collections.Dictionary data)
    {
        var def = ItemData.Get(item.ItemId);
        var holder = Preview(source, item.ItemId, grabAt, out var count);
        int shown = ItemData.ShownCount(def, item);
        count.Text = shown > 1 ? shown.ToString() : "";
        Show(source, holder);
        if (Platform.TouchUi || !StackDial.Applies(def?.Countable ?? 0, item.Count) || !holder.IsInsideTree()) return;
        Root(source.GetTree()).Arm(new StackDial(item.Count), count, data);
    }

    public static int? ChosenAmount(Godot.Collections.Dictionary data) =>
        data.TryGetValue(AmountKey, out var amount) ? amount.AsInt32() : null;

    public static void Show(Control source, Control preview)
    {
        source.SetDragPreview(preview);
        if (!preview.IsInsideTree()) return;
        IgnoreMouse(preview);
        preview.Reparent(Root(source.GetTree()), keepGlobalTransform: false);
    }

    private static Control Preview(Control source, int itemId, Vector2 grabAt, out Label count)
    {
        var scale = source.GetGlobalTransformWithCanvas().Scale;
        var size = source.Size * scale;
        var icon = new TextureRect
        {
            Texture = ItemData.Icon(itemId),
            CustomMinimumSize = size,
            Size = size,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        UpgradeBadge.Show(icon, itemId);
        count = CountLabel(Mathf.RoundToInt(CountFontSize * scale.Y));
        icon.AddChild(count);
        var shown = GmItemId.Wrap(icon, itemId);
        shown.Position = -grabAt * scale;
        var holder = new Control();
        holder.AddChild(shown);
        return holder;
    }

    private static Label CountLabel(int fontSize)
    {
        var count = HudStyle.Label(fontSize, HorizontalAlignment.Right);
        count.VerticalAlignment = VerticalAlignment.Bottom;
        count.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        count.AddThemeStyleboxOverride("normal", new StyleBoxEmpty { ContentMarginRight = 3, ContentMarginBottom = 1 });
        count.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.92f));
        count.AddThemeConstantOverride("shadow_offset_x", 1);
        count.AddThemeConstantOverride("shadow_offset_y", 1);
        count.AddThemeConstantOverride("outline_size", 3);
        count.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.85f));
        return count;
    }

    private static DragLayerRoot Root(SceneTree tree)
    {
        if (tree.Root.GetNodeOrNull<CanvasLayer>(LayerName) is { } existing)
            return existing.GetChild<DragLayerRoot>(0);
        var layer = new CanvasLayer { Name = LayerName, Layer = TopLayer };
        var root = new DragLayerRoot();
        layer.AddChild(root);
        tree.Root.AddChild(layer);
        return root;
    }

    private static void IgnoreMouse(Control control)
    {
        control.MouseFilter = Control.MouseFilterEnum.Ignore;
        foreach (var child in control.GetChildren())
            if (child is Control inner) IgnoreMouse(inner);
    }
}

public sealed partial class DragLayerRoot : Control
{
    private const double MsecPerSecond = 1000.0;

    private StackDial? _dial;
    private Label? _count;
    private Godot.Collections.Dictionary? _data;

    public DragLayerRoot() => MouseFilter = MouseFilterEnum.Ignore;

    internal void Arm(StackDial dial, Label count, Godot.Collections.Dictionary data)
    {
        _dial = dial;
        _count = count;
        _data = data;
        Publish();
    }

    public override void _Input(InputEvent ev)
    {
        if (_dial == null || ev is not InputEventMouseButton { ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown } wheel) return;
        if (!GetViewport().GuiIsDragging())
        {
            Disarm();
            return;
        }
        GetViewport().SetInputAsHandled();
        if (!wheel.Pressed) return;
        _dial.Turn(wheel.ButtonIndex == MouseButton.WheelUp ? StackDial.Up : StackDial.Down, Time.GetTicksMsec() / MsecPerSecond);
        Publish();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationDragEnd) Disarm();
    }

    private void Publish()
    {
        if (_dial == null || _data == null) return;
        _data[DragLayer.AmountKey] = _dial.Amount;
        if (_count == null || !IsInstanceValid(_count)) return;
        _count.Text = _dial.Amount.ToString();
        _count.AddThemeColorOverride("font_color", _dial.Whole ? Colors.White : UiTheme.GoldBright);
    }

    private void Disarm()
    {
        _dial = null;
        _count = null;
        _data = null;
    }
}
