using Godot;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private Button? _helmetToggle, _costumeToggle, _rankAuraToggle;
    private const string RankAuraToggleIcon = "system/star";

    private void CosmeticsInit()
    {
        Net.I.HelmetEvent += OnHelmet;
        Net.I.CostumeEvent += OnCostume;
    }

    private void CosmeticsDispose()
    {
        Net.I.HelmetEvent -= OnHelmet;
        Net.I.CostumeEvent -= OnCostume;
    }

    private void ToggleHelmet()
    {
        Net.I.SendLookToggles(!Net.I.HelmetHidden, Net.I.CostumeHidden);
    }

    private void ToggleRankAura() => Net.I.SetOwnRankAuraShown(!Net.I.OwnRankAuraShown);

    private void ToggleCostume()
    {
        Net.I.SendLookToggles(Net.I.HelmetHidden, !Net.I.CostumeHidden);
    }

    private void OnHelmet(int charId, bool hidden)
    {
        if (charId == _myId)
        {
            RerenderSelfEquipment();
            RefreshLookToggles();
            return;
        }
        if (!_ents.TryGetValue(charId, out var e) || e.IsNpc || e.HelmetHidden == hidden) return;
        e.HelmetHidden = hidden;
        RedressEntity(e);
    }

    private void OnCostume(int charId, bool hidden)
    {
        if (charId == _myId)
        {
            RerenderSelfEquipment();
            RefreshLookToggles();
            return;
        }
        if (!_ents.TryGetValue(charId, out var e) || e.IsNpc || e.CostumeHidden == hidden) return;
        e.CostumeHidden = hidden;
        RedressEntity(e);
    }

    private Button LookToggle(string icon, System.Action pressed)
    {
        var button = UiTheme.IconButton(UiIcons.Get(icon), "");
        button.CustomMinimumSize = new Vector2(LookToggleSize, LookToggleSize);
        button.AddThemeConstantOverride("icon_max_width", LookToggleIcon);
        button.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        button.Pressed += pressed;
        return button;
    }

    private void RefreshLookToggles()
    {
        StyleLookToggle(_helmetToggle, Net.I.HelmetHidden, "Helmet hidden - click to show it", "Hide the helmet");
        StyleLookToggle(_costumeToggle, Net.I.CostumeHidden, "Costume hidden - click to show it", "Hide the costume");
        StyleLookToggle(_rankAuraToggle, !Net.I.OwnRankAuraShown, "Rank aura hidden - click to show it", "Hide your rank aura");
        if (_rankAuraToggle != null && IsInstanceValid(_rankAuraToggle)) _rankAuraToggle.Visible = SelfHasRankAura;
    }

    private static void StyleLookToggle(Button? button, bool hidden, string hiddenTip, string shownTip)
    {
        if (button == null || !GodotObject.IsInstanceValid(button)) return;
        var tint = hidden ? new Color(UiTheme.TextHi, LookToggleHiddenAlpha) : UiTheme.GoldBright;
        button.AddThemeColorOverride("icon_normal_color", tint);
        button.AddThemeColorOverride("icon_hover_color", hidden ? UiTheme.TextHi : Colors.White);
        button.TooltipText = hidden ? hiddenTip : shownTip;
    }

    private const float LookToggleSize = 22f;
    private const float LookToggleInset = 2f;
    private const int LookToggleIcon = 16;
    private const float LookToggleHiddenAlpha = 0.35f;
}
