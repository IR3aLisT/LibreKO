using Godot;
using LibreKO.Domain;

namespace LibreKO;

public partial class World
{
    private const string UiOpacityNode = "UiOpacity";

    private void UiOpacityInit()
    {
        GetTree().NodeAdded += OnUiOpacityNodeAdded;
        Config.UiAlphaChanged += ApplyUiOpacityEverywhere;
        Callable.From(ApplyUiOpacityEverywhere).CallDeferred();
    }

    private void UiOpacityDispose()
    {
        GetTree().NodeAdded -= OnUiOpacityNodeAdded;
        Config.UiAlphaChanged -= ApplyUiOpacityEverywhere;
    }

    private void OnUiOpacityNodeAdded(Node node)
    {
        if (node is CanvasLayer layer) Callable.From(() => ApplyUiOpacity(layer)).CallDeferred();
    }

    private void ApplyUiOpacityEverywhere()
    {
        foreach (var node in GetTree().Root.FindChildren("*", nameof(CanvasLayer), true, false))
            if (node is CanvasLayer layer) ApplyUiOpacity(layer);
    }

    private static void ApplyUiOpacity(CanvasLayer layer)
    {
        if (!IsInstanceValid(layer) || !UiOpacityRules.Fades(layer.Layer)) return;
        var tint = layer.GetNodeOrNull<CanvasModulate>(UiOpacityNode);
        if (Config.UiAlpha >= UiOpacityRules.Opaque)
        {
            tint?.QueueFree();
            return;
        }
        if (tint == null)
        {
            tint = new CanvasModulate { Name = UiOpacityNode };
            layer.AddChild(tint);
        }
        tint.Color = new Color(1f, 1f, 1f, UiOpacityRules.Fraction(Config.UiAlpha));
    }
}
