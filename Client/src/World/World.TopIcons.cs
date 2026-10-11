using System;
using Godot;

namespace LibreKO;

public partial class World
{
    private const float TopIconSize = 29f;
    private const float TopIconGap = 6f;
    private const float TopIconPadX = 9f;

    private CanvasLayer _topIconLayer = null!;
    private Button _powerUpStoreIcon = null!;
    private TextureRect _powerUpStoreImage = null!;
    private Button _lotteryIcon = null!;
    private TextureRect _lotteryImage = null!;
    private Button _genieIcon = null!;

    private static Color TopIconColor(bool waiting) => waiting ? UiTheme.GoldBright : UiTheme.TextLo;

    private void TopIconsInit()
    {
        _topIconLayer = new CanvasLayer { Layer = 66 };
        AddChild(_topIconLayer);

        _powerUpStoreIcon = TopIconButton(_topIconLayer, "system/gem", "Power-Up Store",
            OpenPowerUpStore, out _powerUpStoreImage);
        _powerUpStoreImage.SelfModulate = UiTheme.Premium;
        _powerUpStoreIcon.Resized += PlacePowerUpStoreIcon;

        _lotteryIcon = TopIconButton(_topIconLayer, "system/ticket", "Lottery Event",
            ToggleLottery, out _lotteryImage);
        _lotteryImage.SelfModulate = TopIconColor(true);
        _lotteryIcon.Visible = false;
        _lotteryIcon.Resized += PlaceLotteryIcon;
        _lotteryIcon.VisibilityChanged += PlaceLotteryIcon;

        _genieIcon = TopIconButton(_topIconLayer, "system/sparkle", "Genie", ToggleGenie, out _);
        _genieIcon.Visible = !Platform.TouchUi;

        if (_premiumChip != null) _premiumChip.Resized += PlaceTopIcons;
        if (_pluginHud.TryGetValue(LibreKO.Plugins.HudPart.MiniMap, out var themedMap))
        {
            themedMap.ItemRectChanged += PlaceTopIcons;
            themedMap.VisibilityChanged += PlaceTopIcons;
        }
        Callable.From(PlaceTopIcons).CallDeferred();
        Callable.From(PlacePowerUpStoreIcon).CallDeferred();
        Callable.From(PlaceLotteryIcon).CallDeferred();
    }

    private float MiniMapRightSpan()
    {
        if (!_pluginHud.TryGetValue(LibreKO.Plugins.HudPart.MiniMap, out var themed) || !IsInstanceValid(themed))
            return HudAnchor.Edge + MiniMap.SquareSize;
        if (!themed.IsVisibleInTree() || themed.Size.X <= 0f) return HudAnchor.Edge;
        var rect = themed.GetGlobalRect();
        float screen = GetViewport().GetVisibleRect().Size.X;
        bool topRight = rect.End.X >= screen - TopRightTolerance && rect.Position.Y <= TopRightTolerance;
        return topRight ? Mathf.Max(HudAnchor.Edge, screen - rect.Position.X) : HudAnchor.Edge;
    }

    private const float TopRightTolerance = 48f;

    private void PinPremiumChip()
    {
        if (!IsInstanceValid(_premiumChip)) return;
        HudAnchor.Pin(_premiumChip, HudAnchor.Spot.TopRight, new Vector2(MiniMapRightSpan() + StatusHudGap, HudAnchor.Edge));
    }

    private void TopIconsDispose()
    {
        if (IsInstanceValid(_topIconLayer)) _topIconLayer.QueueFree();
    }

    private static Button TopIconButton(
        CanvasLayer layer, string iconId, string tooltip, Action pressed, out TextureRect image)
    {
        var button = new Button { FocusMode = Control.FocusModeEnum.None, TooltipText = tooltip };
        var flat = new StyleBoxEmpty();
        button.AddThemeStyleboxOverride("normal", flat);
        button.AddThemeStyleboxOverride("hover", flat);
        button.AddThemeStyleboxOverride("pressed", flat);
        button.Pressed += pressed;
        layer.AddChild(button);

        image = UiIcons.Image(iconId, new Vector2(TopIconSize, TopIconSize), TopIconColor(false));
        image.MouseFilter = Control.MouseFilterEnum.Ignore;
        image.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        button.AddChild(image);
        return button;
    }

    private void RefreshLotteryIcon()
    {
        if (!IsInstanceValid(_lotteryIcon)) return;
        _lotteryIcon.Visible = _lotteryState?.Active == true;
    }

    private void PlacePowerUpStoreIcon() =>
        PlaceTopIcon(_powerUpStoreIcon, _powerUpStoreImage, HudPlacement.PowerUpStoreIcon);

    private void PlaceLotteryIcon() =>
        PlaceTopIcon(_lotteryIcon, _lotteryImage, HudPlacement.LotteryIcon);

    private void PlaceTopIcon(Button icon, TextureRect image, HudPlacement.Slot touchSlot)
    {
        if (!Platform.TouchUi)
        {
            PlaceTopIcons();
            return;
        }
        if (!IsInstanceValid(icon)) return;
        float side = HudPlacement.LauncherButtonSize;
        icon.CustomMinimumSize = new Vector2(side, side);
        float inset = side * HudPlacement.LauncherGlyphInset;
        image.OffsetLeft = image.OffsetTop = inset;
        image.OffsetRight = image.OffsetBottom = -inset;
        touchSlot.ApplyTo(icon);
    }

    private void PlaceTopIcons()
    {
        if (Platform.TouchUi || !IsInstanceValid(_premiumChip)) return;

        PinPremiumChip();
        var size = new Vector2(TopIconSize + TopIconPadX * 2f, Mathf.Max(_premiumChip.Size.Y, TopIconSize));
        float right = MiniMapRightSpan() + StatusHudGap + _premiumChip.Size.X;
        float top = HudAnchor.Edge + (_premiumChip.Size.Y - size.Y) * 0.5f;
        foreach (Control icon in new Control[] { _attendanceGift, _trophy, _mailIconButton, _powerUpStoreIcon, _lotteryIcon, _tiIcon, _eventsIcon, _genieIcon, _drIcon, _siegeIcon })
        {
            if (!IsInstanceValid(icon) || !icon.Visible) continue;
            right += TopIconGap;
            icon.CustomMinimumSize = size;
            HudAnchor.Pin(icon, HudAnchor.Spot.TopRight, new Vector2(right, top));
            right += size.X;
        }
    }
}
