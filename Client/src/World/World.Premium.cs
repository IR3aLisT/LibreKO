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
        Net.I.PremiumEvent += OnPremiumStatus;
        Net.I.LevelChangeEvent += OnPremiumLevelChange;

        OnPremiumStatus(Net.I.PremiumAccountStatus, Net.I.PremiumType, Net.I.PremiumHours);
        Net.I.SendPremiumRequest();
    }

    private void PremiumDispose()
    {
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
        if (!active)
        {
            _premiumLabel.Text = "No Premium";
            _premiumLabel.AddThemeColorOverride("font_color", UiTheme.TextLo);
            _premiumChip.TooltipText = "";
            SetPremiumDot(UiTheme.TextDim);
            return;
        }

        string prefix = accountStatus == PcRoomStatus ? "PC Room" : PremiumNames.Of(premiumType);
        string timeLeft = FormatPremiumTime(remainingHours);
        _premiumLabel.Text = $"{prefix}: {timeLeft}";
        _premiumChip.TooltipText = PremiumBonus.Tooltip(prefix, timeLeft, Net.I.PremiumBonuses);
        _premiumLabel.AddThemeColorOverride("font_color", UiTheme.GoldBright);
        SetPremiumDot(UiTheme.Gold);
    }

    private static string FormatPremiumTime(int hours)
    {
        if (hours <= 0) return "<1h";
        if (hours < 24) return $"{hours}h";
        int days = hours / 24;
        int rem = hours % 24;
        return rem > 0 ? $"{days}d {rem}h" : $"{days}d";
    }
}
