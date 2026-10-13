using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private CanvasLayer _premiumLayer = null!;
    private PanelContainer _premiumChip = null!;
    private Panel _premiumDot = null!;
    private Label _premiumLabel = null!;

    private void PremiumInit()
    {
        BuildPremiumChip();
        BuildClanBonusChip();
        Net.I.PremiumEvent += OnPremiumStatus;
        Net.I.LevelChangeEvent += OnPremiumLevelChange;

        OnPremiumStatus(Net.I.PremiumAccountStatus, Net.I.PremiumType, Net.I.PremiumHours);
        Net.I.SendPremiumRequest();
    }

    private const float ClanBonusChipGap = 4f;
    private PanelContainer _clanBonusChip = null!;
    private Label _clanBonusMarks = null!;

    private void BuildClanBonusChip()
    {
        _clanBonusChip = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop, Visible = false };
        _clanBonusChip.AddThemeStyleboxOverride("panel", UiTheme.Chip());
        _premiumLayer.AddChild(_clanBonusChip);
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 7);
        _clanBonusChip.AddChild(row);
        var title = UiTheme.Text("Clan", 12, UiTheme.TextLo);
        title.MouseFilter = Control.MouseFilterEnum.Ignore;
        row.AddChild(title);
        _clanBonusMarks = UiTheme.Text("", 12, UiTheme.GoldBright);
        _clanBonusMarks.MouseFilter = Control.MouseFilterEnum.Ignore;
        row.AddChild(_clanBonusMarks);
        Net.I.ClanBonusEvent += OnClanBonus;
        OnClanBonus(Net.I.ClanOnlineCount);
    }

    private void OnClanBonus(int online)
    {
        if (!IsInstanceValid(_clanBonusChip)) return;
        bool shown = online != Net.NoClanBonus && MyClan.InClan;
        _clanBonusChip.Visible = shown;
        if (!shown) return;
        _clanBonusMarks.Text = ClanBonusCard.Marks(online,
            ItemData.Text(ClanBonusCard.OnText, ClanBonusCard.OnFallback),
            ItemData.Text(ClanBonusCard.OffText, ClanBonusCard.OffFallback));
        _clanBonusChip.TooltipText = ClanBonusCard.Tooltip(online,
            ItemData.Text(ClanBonusCard.NeedText, ClanBonusCard.NeedFallback),
            ItemData.Text(ClanBonusCard.BonusText, ClanBonusCard.BonusFallback));
        PinClanBonusChip();
    }

    private void PinClanBonusChip()
    {
        if (!IsInstanceValid(_clanBonusChip) || !IsInstanceValid(_premiumChip)) return;
        float below = _premiumChip.Size.Y + ClanBonusChipGap;
        HudAnchor.Pin(_clanBonusChip, HudAnchor.Spot.TopRight,
            new Vector2(MiniMapRightSpan() + StatusHudGap, HudAnchor.Edge + below));
    }

    private void PremiumDispose()
    {
        Net.I.ClanBonusEvent -= OnClanBonus;
        Net.I.PremiumEvent -= OnPremiumStatus;
        Net.I.LevelChangeEvent -= OnPremiumLevelChange;
    }

    private void OnPremiumLevelChange(int level, int statPoints, int skillPool, long maxExp, long exp, int maxHp, int hp, int maxMp, int mp)
    {
        if (Net.I.PremiumHours > 0) Net.I.SendPremiumRequest();
    }

    private void BuildPremiumChip()
    {
        _premiumLayer = new CanvasLayer { Layer = 66 };
        AddChild(_premiumLayer);

        _premiumChip = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        _premiumChip.AddThemeStyleboxOverride("panel", UiTheme.Chip());
        _premiumLayer.AddChild(_premiumChip);
        PinPremiumChip();

        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 7);
        _premiumChip.AddChild(row);

        _premiumDot = new Panel
        {
            CustomMinimumSize = new Vector2(9, 9),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        var dotWrap = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        dotWrap.AddChild(_premiumDot);
        row.AddChild(dotWrap);

        _premiumLabel = UiTheme.Text("No Premium", 12, UiTheme.TextLo);
        _premiumLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        row.AddChild(_premiumLabel);

        SetPremiumDot(UiTheme.TextDim);
    }

    private void SetPremiumDot(Color color)
    {
        var sb = new StyleBoxFlat { BgColor = color };
        sb.SetCornerRadiusAll(5);
        _premiumDot.AddThemeStyleboxOverride("panel", sb);
    }

    private const int PcRoomStatus = 2;

    private void OnPremiumStatus(int accountStatus, int premiumType, int remainingHours)
    {
        bool active = accountStatus != 0 && remainingHours > 0;
        RefreshPremiumSeller();
        if (!active)
        {
            _premiumLabel.Text = "No Premium";
            _premiumLabel.AddThemeColorOverride("font_color", UiTheme.TextLo);
            _premiumChip.TooltipText = "";
            SetPremiumDot(UiTheme.TextDim);
            return;
        }

        string name = accountStatus == PcRoomStatus ? "PC Room" : PremiumTitle(premiumType);
        var (timeText, timeFallback, timeValue) = PremiumNames.TimeLeft(remainingHours);
        string timeLine = ItemData.Text(timeText, timeFallback).Replace("%d", timeValue.ToString());
        _premiumLabel.Text = name;
        _premiumChip.TooltipText = PremiumBonus.Tooltip(name, timeLine, Net.I.PremiumBonuses);
        _premiumLabel.AddThemeColorOverride("font_color", UiTheme.GoldBright);
        SetPremiumDot(UiTheme.Gold);
    }

    private static string PremiumTitle(int type)
    {
        string premium = ItemData.Text(PremiumNames.PremiumText, PremiumNames.PremiumFallback);
        return PremiumNames.Name(type) is { } name ? $"{ItemData.Text(name.Text, name.Fallback)} {premium}" : premium;
    }
}
