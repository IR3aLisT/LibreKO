using Godot;
using LibreKO.Domain;

namespace LibreKO;

public static class DragLayer
{
    private const int TopLayer = 230;
    private const string LayerName = "DragLayer";

    public static void ShowItem(Control source, int itemId, Vector2 grabAt)
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
        var shown = GmItemId.Wrap(icon, itemId);
        shown.Position = -grabAt * scale;
        var holder = new Control();
        holder.AddChild(shown);
        Show(source, holder);
    }

    public static void Show(Control source, Control preview)
    {
        source.SetDragPreview(preview);
        if (!preview.IsInsideTree()) return;
        IgnoreMouse(preview);
        preview.Reparent(Root(source.GetTree()), keepGlobalTransform: false);
    }

    private static Control Root(SceneTree tree)
    {
        if (tree.Root.GetNodeOrNull<CanvasLayer>(LayerName) is { } existing)
            return existing.GetChild<Control>(0);
        var layer = new CanvasLayer { Name = LayerName, Layer = TopLayer };
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
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
