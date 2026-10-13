using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const int BeautyPreviewWidth = 214;
    private const int BeautyPreviewHeight = 275;
    private const int FaceChangeCoupon = 810109000;
    private const int MakeOverCoupon = 810340000;
    private const int NoCouponText = 18901;
    private const int LookChangedText = 18902;
    private const int LookFailedText = 18903;
    private const int HairColourText = 200025;
    private const int HairColourPromptText = 18905;
    private const string BeautyConfirmText = "Use a Make Over Coupon to change your look?";
    private const float PaletteGap = 8f;

    private static readonly int[] BeautyCoupons = { FaceChangeCoupon, MakeOverCoupon };

    private CanvasLayer _changeHairLayer = null!;
    private HudWindow _changeHairPanel = null!;
    private LookPreview _beautyPreview = null!;
    private LookEditor _beautyEditor = null!;
    private Label _changeHairStatus = null!;
    private Button _beautyConfirm = null!;
    private bool _changeHairShown;
    private bool _beautyInFlight;
    private HudWindow? _hairPalette;
    private ColorPicker _hairPalettePicker = null!;
    private LookEditor? _hairPaletteTarget;

    private void ChangeHairInit()
    {
        BuildChangeHairPanel();
        Net.I.ChangeHairResultEvent += OnChangeHairResult;
        Net.I.BeautyShopEvent += OpenChangeHair;
    }

    private void ChangeHairDispose()
    {
        Net.I.ChangeHairResultEvent -= OnChangeHairResult;
        Net.I.BeautyShopEvent -= OpenChangeHair;
    }

    private void BuildChangeHairPanel()
    {
        _changeHairLayer = new CanvasLayer { Layer = 74 };
        AddChild(_changeHairLayer);

        _changeHairPanel = new HudWindow("changehair", "Beauty Shop") { Visible = false };
        _changeHairPanel.Closed += CloseChangeHair;
        _changeHairLayer.AddChild(_changeHairPanel);

        var column = _changeHairPanel.Body;
        column.AddThemeConstantOverride("separation", 8);
        _beautyPreview = new LookPreview(BeautyPreviewWidth, BeautyPreviewHeight);
        column.AddChild(LookPreviewColumn(_beautyPreview, zoom: true));

        _beautyEditor = new LookEditor(withRace: false, withColour: false);
        _beautyEditor.Changed += ShowBeautyLook;
        column.AddChild(_beautyEditor);

        var colour = LookActionButton(ItemData.Text(HairColourText, "Change hair color"), () => OpenHairPalette(_beautyEditor));
        column.AddChild(colour);

        _changeHairStatus = HudStyle.Label(12);
        _changeHairStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _changeHairStatus.CustomMinimumSize = new Vector2(BeautyPreviewWidth, 0);
        column.AddChild(_changeHairStatus);

        var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        actions.AddThemeConstantOverride("separation", 8);
        _beautyConfirm = LookActionButton("OK", SubmitChangeHair);
        actions.AddChild(_beautyConfirm);
        actions.AddChild(LookActionButton("Cancel", CloseChangeHair));
        column.AddChild(actions);
    }

    private void OpenChangeHair()
    {
        if (_changeHairShown) return;
        var me = Net.I.LastEnter;
        _beautyInFlight = false;
        _beautyEditor.Load(new[] { me.Race }, me.Race, _selfFace, _selfHair);
        ShowBeautyLook();
        SetChangeHairStatus("", false);
        _changeHairPanel.Visible = true;
        _changeHairShown = true;
        _beautyPreview.AlwaysRender = _changeHairPanel.PluginReplaced;
        PluginHost.Game.RaiseBeautyShop();
    }

    private void CloseChangeHair()
    {
        if (!_changeHairShown) return;
        _changeHairShown = false;
        _changeHairPanel.Visible = false;
        _beautyPreview.AlwaysRender = false;
        _beautyPreview.Clear();
        CloseHairPalette();
        PluginHost.Game.RaiseBeautyShop();
    }

    private void ShowBeautyLook()
    {
        _beautyEditor.Refresh();
        _beautyPreview.Show(_beautyEditor.Race, _beautyEditor.Face, _beautyEditor.HairStyle, _beautyEditor.HairColour);
        _beautyConfirm.Disabled = _beautyInFlight;
        PluginHost.Game.RaiseBeautyShop();
    }

    private int BeautyCouponPosition()
    {
        if (Inv == null) return -1;
        foreach (int coupon in BeautyCoupons)
            for (int i = 0; i < InventoryConstants.HaveMax; i++)
            {
                int slot = InventoryConstants.InventoryStart + i;
                if (slot < Inv.Length && Inv[slot].ItemId == coupon && Inv[slot].Count > 0) return i;
            }
        return -1;
    }

    private void SubmitChangeHair()
    {
        if (_beautyInFlight) return;
        int position = BeautyCouponPosition();
        if (position < 0)
        {
            Notice.Show(this, ItemData.Text(NoCouponText, "You do not have a Makeover Coupon."));
            CloseChangeHair();
            return;
        }
        Notice.Confirm(this, BeautyConfirmText, "OK", "Cancel", () =>
        {
            _beautyInFlight = true;
            ShowBeautyLook();
            Net.I.SendChangeHair((byte)position, _beautyEditor.Face, _beautyEditor.Hair);
        });
    }

    private void OnChangeHairResult(bool ok, int face, int hair)
    {
        _beautyInFlight = false;
        if (!ok)
        {
            Notice.Show(this, ItemData.Text(LookFailedText, "Feature modification failed."));
            if (_changeHairShown) ShowBeautyLook();
            return;
        }
        _selfHair = hair;
        _selfFace = face;
        RerenderSelfEquipment();
        Notice.Show(this, ItemData.Text(LookChangedText, "Feature modification succeeded."));
        CloseChangeHair();
    }

    private void SetChangeHairStatus(string text, bool warn)
    {
        _changeHairStatus.Text = text;
        _changeHairStatus.Visible = text.Length > 0;
        _changeHairStatus.AddThemeColorOverride("font_color", warn ? new Color("ff6a6a") : Colors.White);
    }

    private void OpenHairPalette(LookEditor target)
    {
        if (_hairPalette == null || !IsInstanceValid(_hairPalette)) BuildHairPalette();
        _hairPaletteTarget = target;
        _hairPalettePicker.Color = target.HairColour;
        _hairPalette!.Visible = true;
        var beside = target == _genderEditor ? _genderPanel : _changeHairPanel;
        _hairPalette.Position = beside.Position + new Vector2(beside.Size.X + PaletteGap, 0f);
    }

    private void CloseHairPalette()
    {
        if (_hairPalette != null && IsInstanceValid(_hairPalette)) _hairPalette.Visible = false;
    }

    private void BuildHairPalette()
    {
        _hairPalette = new HudWindow("hairpalette", "Hair Colour", persistLayout: false) { Visible = false };
        _hairPalette.Closed += CloseHairPalette;
        _changeHairLayer.AddChild(_hairPalette);
        var body = _hairPalette.Body;
        body.AddThemeConstantOverride("separation", 8);
        var prompt = HudStyle.Label(12);
        prompt.Text = ItemData.Text(HairColourPromptText, "Please select your hair color.");
        body.AddChild(prompt);
        _hairPalettePicker = new ColorPicker
        {
            EditAlpha = false,
            PickerShape = ColorPicker.PickerShapeType.HsvRectangle,
            ColorMode = ColorPicker.ColorModeType.Rgb,
            PresetsVisible = false,
            SamplerVisible = false,
            ColorModesVisible = false,
            HexVisible = false,
        };
        body.AddChild(_hairPalettePicker);
        var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        actions.AddThemeConstantOverride("separation", 8);
        actions.AddChild(LookActionButton("Apply", () =>
        {
            (_hairPaletteTarget ?? _beautyEditor).SetHairColour(_hairPalettePicker.Color);
            CloseHairPalette();
        }));
        actions.AddChild(LookActionButton("Cancel", CloseHairPalette));
        body.AddChild(actions);
    }
}
