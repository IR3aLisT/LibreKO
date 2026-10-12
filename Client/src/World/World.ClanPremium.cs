using Godot;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private CanvasLayer _clanPremiumLayer = null!;
    private PanelContainer _clanPremiumChip = null!;
    private Panel _clanPremiumDot = null!;
    private Label _clanPremiumLabel = null!;
    private bool _clanPremiumInClan;
    private const double ClanPremiumRefreshSeconds = 60.0;

    private void ClanPremiumInit()
    {
        BuildClanPremiumChip();
        Net.I.ClanPremiumEvent += OnClanPremiumStatus;
        Net.I.MyClanChangedEvent += OnClanPremiumClanInfo;

        RefreshClanPremiumChip();
        Net.I.SendClanPremiumQuery();
    }

    private void ClanPremiumDispose()
    {
        Net.I.ClanPremiumEvent -= OnClanPremiumStatus;
        Net.I.MyClanChangedEvent -= OnClanPremiumClanInfo;
    }

    private void BuildClanPremiumChip()
    {
        _clanPremiumLayer = new CanvasLayer { Layer = 66 };
        AddChild(_clanPremiumLayer);

        _clanPremiumChip = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Pass };
        _clanPremiumChip.AddThemeStyleboxOverride("panel", UiTheme.Chip());

        _clanPremiumChip.AnchorLeft = 1; _clanPremiumChip.AnchorRight = 1;
        _clanPremiumChip.AnchorTop = 0; _clanPremiumChip.AnchorBottom = 0;
        _clanPremiumChip.GrowHorizontal = Control.GrowDirection.Begin;
        _clanPremiumChip.OffsetRight = -14;
        _clanPremiumChip.OffsetTop = 12 + (int)MiniMap.SquareSize + 8 + 30;
        _clanPremiumChip.Visible = false;
        _clanPremiumLayer.AddChild(_clanPremiumChip);

        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 7);
        _clanPremiumChip.AddChild(row);

        _clanPremiumDot = new Panel
        {
            CustomMinimumSize = new Vector2(9, 9),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        var dotWrap = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        dotWrap.AddChild(_clanPremiumDot);
        row.AddChild(dotWrap);

        _clanPremiumLabel = UiTheme.Text("Clan: No Premium", 12, UiTheme.TextLo);
        _clanPremiumLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        row.AddChild(_clanPremiumLabel);

        SetClanPremiumDot(UiTheme.TextDim);
        var refresh = new Godot.Timer { WaitTime = ClanPremiumRefreshSeconds, Autostart = true };
        refresh.Timeout += RefreshClanPremiumChip;
        _clanPremiumLayer.AddChild(refresh);
        HudLayout.Attach(
            _clanPremiumChip,
            "hud_clan_premium",
            _clanPremiumChip,
            () => _clanPremiumChip.Position);
    }

    private void SetClanPremiumDot(Color color)
    {
        var sb = new StyleBoxFlat { BgColor = color };
        sb.SetCornerRadiusAll(5);
        _clanPremiumDot.AddThemeStyleboxOverride("panel", sb);
    }

    private void OnClanPremiumStatus(bool active)
    {
        RefreshClanPremiumChip();
        RefreshPremiumSeller();
    }

    private void RefreshPremiumSeller() =>
        ItemData.PremiumSeller = (Net.I.PremiumAccountStatus != 0 && Net.I.PremiumHours > 0) || Net.I.ClanPremiumActive;

    private void OnClanPremiumClanInfo(MyClanInfo info)
    {
        _clanPremiumInClan = info.InClan;
        RefreshClanPremiumChip();
    }

    private void RefreshClanPremiumChip()
    {
        _clanPremiumChip.Visible = _clanPremiumInClan;
        if (!_clanPremiumInClan) return;

        long left = Net.I.ClanPremiumEndsAt - System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        _clanPremiumChip.TooltipText = Net.I.ClanPremiumActive && Net.I.ClanPremiumEndsAt > 0
            ? "Clan premium ends " + System.DateTimeOffset.FromUnixTimeSeconds(Net.I.ClanPremiumEndsAt).LocalDateTime.ToString("yyyy-MM-dd HH:mm")
            : "";
        if (Net.I.ClanPremiumActive)
        {
            _clanPremiumLabel.Text = left > 0 ? $"Clan Premium · {DurationText.Short(left)}" : "Clan Premium";
            _clanPremiumLabel.AddThemeColorOverride("font_color", UiTheme.GoldBright);
            SetClanPremiumDot(UiTheme.Gold);
        }
        else
        {
            _clanPremiumLabel.Text = "Clan: No Premium";
            _clanPremiumLabel.AddThemeColorOverride("font_color", UiTheme.TextLo);
            SetClanPremiumDot(UiTheme.TextDim);
        }
    }
}
