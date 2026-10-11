using Godot;

namespace LibreKO;

public partial class World
{
    private const string GatherChipLayoutId = "hud_gather";
    private const float GatherChipLift = 230f;
    private const int GatherChipFont = 13;

    private PanelContainer? _gatherChip;
    private Label _gatherChipText = null!;

    private void BuildGatherChip()
    {
        var layer = new CanvasLayer { Layer = 64 };
        AddChild(layer);
        var chip = new PanelContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        chip.AddThemeStyleboxOverride("panel", UiTheme.Chip());
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 8);
        chip.AddChild(row);
        _gatherChipText = UiTheme.Text("", GatherChipFont, UiTheme.Gold);
        _gatherChipText.MouseFilter = Control.MouseFilterEnum.Ignore;
        _gatherChipText.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        row.AddChild(_gatherChipText);
        var stop = UiTheme.SmallButton("Stop", "");
        stop.Pressed += () => StopGather(sendStop: true);
        row.AddChild(stop);
        layer.AddChild(chip);
        _gatherChip = chip;
        HudLayout.Attach(chip, GatherChipLayoutId, chip, GatherChipHome);
    }

    private Vector2 GatherChipHome()
    {
        var screen = GetViewport().GetVisibleRect().Size;
        var size = _gatherChip?.GetCombinedMinimumSize() ?? Vector2.Zero;
        return new Vector2((screen.X - size.X) / 2f, screen.Y - GatherChipLift - size.Y);
    }

    private void RefreshGatherChip()
    {
        if (!_gathering)
        {
            if (_gatherChip != null) _gatherChip.Visible = false;
            return;
        }
        if (_gatherChip == null || !IsInstanceValid(_gatherChip)) BuildGatherChip();
        _gatherChipText.Text = _gatherFishing ? "Fishing in progress" : "Mining in progress";
        _gatherChip!.TooltipText = $"{KeyBinds.Get(KeyAction.Interact).Text} also stops it.";
        _gatherChip.Visible = true;
    }
}
