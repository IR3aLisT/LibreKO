using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const string SiegeWarTitle = "Castle Siege War";
    private const float SiegeStateIconSize = 32f;
    private const float SiegeWarWindowRightGap = 314f;
    private const float SiegeWarWindowTop = 70f;
    private const int SiegeWarBodyWidth = 195;
    private static readonly Color SiegeDefenseColour = new(0f, 0f, 1f);
    private static readonly Color SiegeAttackColour = new(1f, 0f, 0f);
    private static readonly Color SiegeRefusalColour = new("ff7f50");

    private Button _siegeIcon = null!;
    private TextureRect _siegeIconImage = null!;
    private CanvasLayer _siegeWarLayer = null!;
    private HudWindow _siegeWarPanel = null!;
    private TextureRect _siegeState = null!;
    private Label _siegeWarClock = null!, _siegeHoldClock = null!, _siegeOwner = null!;
    private double _siegeWarLeft, _siegeHoldLeft;
    private SiegeWarStatus _siegeShown = SiegeWarStatus.None;

    private void SiegeWarInit()
    {
        _siegeIcon = TopIconButton(_topIconLayer, "system/combat-defence", SiegeWarTitle, ToggleSiegeWar, out _siegeIconImage);
        _siegeIcon.Resized += PlaceTopIcons;
        _siegeIcon.VisibilityChanged += PlaceTopIcons;
        BuildSiegeWarWindow();
        Net.I.SiegeWarEvent += OnSiegeWar;
        Net.I.SiegeBaseEvent += OnSiegeBase;
        Net.I.SiegePointsEvent += OnSiegePoints;
        OnSiegeWar();
    }

    private void SiegeWarDispose()
    {
        Net.I.SiegeWarEvent -= OnSiegeWar;
        Net.I.SiegeBaseEvent -= OnSiegeBase;
        Net.I.SiegePointsEvent -= OnSiegePoints;
    }

    private void BuildSiegeWarWindow()
    {
        _siegeWarLayer = new CanvasLayer { Layer = 62 };
        AddChild(_siegeWarLayer);
        float screenWidth = IsInsideTree() ? GetViewport().GetVisibleRect().Size.X : 0f;
        var origin = new Vector2(Mathf.Max(0f, screenWidth - SiegeWarWindowRightGap), SiegeWarWindowTop);
        _siegeWarPanel = new HudWindow("siege_war", SiegeWarTitle, origin, SiegeWarBodyWidth) { Visible = false };
        _siegeWarPanel.Closed += () => _siegeWarPanel.Visible = false;
        _siegeWarLayer.AddChild(_siegeWarPanel);

        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 4);
        _siegeWarPanel.Body.AddChild(body);

        _siegeState = UiIcons.Image("system/combat-defence", new Vector2(SiegeStateIconSize, SiegeStateIconSize), UiTheme.TextLo);
        _siegeState.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        body.AddChild(_siegeState);
        _siegeWarClock = UiTheme.Text("", 13, UiTheme.TextHi);
        _siegeHoldClock = UiTheme.Text("", 13, UiTheme.TextHi);
        _siegeOwner = UiTheme.Text("", 13, UiTheme.GoldBright);
        body.AddChild(_siegeWarClock);
        body.AddChild(_siegeHoldClock);
        body.AddChild(_siegeOwner);
    }

    private void ToggleSiegeWar()
    {
        if (_siegeWarPanel.Visible) { _siegeWarPanel.Visible = false; return; }
        RenderSiegeWar();
        ShowOnTop(_siegeWarPanel);
    }

    private void OnSiegeWar()
    {
        var status = Net.I.SiegeWar;
        ShowSiegeStatus(status);
        bool shown = SiegeWar.At(_zone, status);
        _siegeIcon.Visible = shown;
        if (!shown) _siegeWarPanel.Visible = false;
        RefreshPlayerHostility(Net.I.CurrentZoneAbility);
        foreach (var (id, e) in _ents)
        {
            if (e.IsNpc) continue;
            ApplySiegeName(e);
            RefreshClanLine(e, id);
        }
        PluginNotifyTarget();
    }

    private bool AtSiegeWar => SiegeWar.At(_zone, Net.I.SiegeWar);

    private bool RefusedInSiegeWar()
    {
        if (!AtSiegeWar) return false;
        CombatNotice(ItemData.Text(SiegeWar.RefusedInWarText, SiegeWar.Fallback(SiegeWar.RefusedInWarText)), SiegeRefusalColour);
        return true;
    }

    private static string SiegeText(int textId) => ItemData.Text(textId, SiegeWar.Fallback(textId));

    private string ShownName(Ent e)
    {
        if (e.IsNpc || !AtSiegeWar) return e.Name;
        int mask = SiegeWar.TargetMaskText(Net.I.IsGm, MyClan.InClan ? MyClan.ClanId : 0, MyClan.InClan ? MyClan.AllianceId : 0,
            e.KnightsId, e.AllianceId, Net.I.SiegeWar);
        return mask == 0 ? e.Name : SiegeText(mask);
    }

    private (string Text, Color Colour)? SiegeClanLine(Ent e)
    {
        if (e.IsNpc || !AtSiegeWar) return null;
        bool owner = SiegeWar.OwnerSide(e.KnightsId, e.AllianceId, Net.I.SiegeWar);
        return (SiegeText(owner ? SiegeWar.DefenseClanText : SiegeWar.SiegeClanText), owner ? SiegeDefenseColour : SiegeAttackColour);
    }

    private void ApplySiegeName(Ent e)
    {
        if (e.NameTag == null || !GodotObject.IsInstanceValid(e.NameTag)) return;
        if (!AtSiegeWar)
        {
            int hidden = PlateRules.AnonymousNameText(_zone, Net.I.Nation, MyClan.InClan ? MyClan.ClanId : 0, e.Nation, e.KnightsId);
            e.NameTag.Text = hidden > 0 ? ItemData.Text(hidden, e.Name) : e.Name;
            RefreshPlayerNameColour(e);
            return;
        }
        bool owner = SiegeWar.OwnerSide(e.KnightsId, e.AllianceId, Net.I.SiegeWar);
        e.NameTag.Text = "[" + SiegeText(owner ? SiegeWar.DefenseMemberText : SiegeWar.SiegeMemberText) + "]" + e.Name;
        NamePlate.Tint(e.NameTag, owner ? SiegeDefenseColour : SiegeAttackColour);
    }

    private void ShowSiegeStatus(SiegeWarStatus status)
    {
        _siegeShown = status;
        _siegeWarLeft = status.WarSeconds;
        _siegeHoldLeft = status.HoldSeconds;
        bool held = status.Held;
        _siegeState.Texture = UiIcons.Get(held ? "system/combat-attack" : "system/combat-defence");
        _siegeState.SelfModulate = held ? UiTheme.GoldBright : UiTheme.TextLo;
        if (status.OwnerName.Length > 0)
            _siegeOwner.Text = ItemData.Text(SiegeWar.OwnerText, "Siege Owner Clan : [%s]").Replace("%s", status.OwnerName);
        RenderSiegeWar();
    }

    private void SiegeWarTick(double delta)
    {
        if (!_siegeIcon.Visible) return;
        _siegeWarLeft -= delta;
        if (_siegeShown.Held) _siegeHoldLeft -= delta;
        if (_siegeWarLeft <= 0)
        {
            _siegeWarPanel.Visible = false;
            return;
        }
        if (_siegeWarPanel.Visible) RenderSiegeWar();
    }

    private void RenderSiegeWar()
    {
        _siegeWarClock.Text = SiegeWar.Clock(ItemData.Text(SiegeWar.WarClockText, "Castle Siege Time : [%2d : %d]"), (int)_siegeWarLeft);
        int hold = _siegeShown.Held ? (int)_siegeHoldLeft : 0;
        _siegeHoldClock.Text = SiegeWar.Clock(ItemData.Text(SiegeWar.HoldClockText, "Occupy Time : [%2d : %d]"), hold);
    }

    private void OnSiegeBase(short result)
    {
        if (SiegeWar.BaseRefusal(result) is { } text)
            Notice.Show(this, ItemData.Text(text.Id, text.Fallback));
    }

    private void OnSiegePoints(SiegePoints points)
    {
        if (SiegeWar.PointsText(points.Kind) is not { } text) return;
        string line = ItemData.Text(text.Id, text.Fallback);
        line = points.Kind == SiegePoints.Renewed
            ? ReplaceFirst(line, "%d", points.Total.ToString())
            : ReplaceFirst(ReplaceFirst(line, "%d", points.Gained.ToString()), "%d", points.Total.ToString());
        CombatNotice(line, points.Kind == SiegePoints.Renewed ? SiegePointsRenewedColour : SiegePointsColour);
    }

    private static readonly Color SiegePointsColour = Color.Color8(0xE2, 0xC4, 0x77);
    private static readonly Color SiegePointsRenewedColour = Color.Color8(0x87, 0xCE, 0xEB);

    private static string ReplaceFirst(string text, string token, string value)
    {
        int at = text.IndexOf(token, System.StringComparison.Ordinal);
        return at < 0 ? text : text[..at] + value + text[(at + token.Length)..];
    }
}
