using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const string ExpSealTitle = "EXP Seal";
    private const int ExpSealBodyWidth = 380;
    private const float ExpSealJarSize = 44f;
    private const int BagStart = 14;
    private static readonly Color ExpSealRefusalColour = new("ff3b3b");
    private static readonly Color ExpSealGainColour = new(1f, 1f, 0f);

    private CanvasLayer _expSealLayer = null!;
    private HudWindow _expSealWindow = null!;
    private TextureRect _expSealJar = null!;
    private Label _expSealPercent = null!, _expSealAmount = null!;
    private Button _expSealOn = null!, _expSealOff = null!;
    private OptionButton _expSealPick = null!;
    private bool _expSealJarPlaced;

    private void ExpSealInit()
    {
        _expSealLayer = new CanvasLayer { Layer = 63 };
        AddChild(_expSealLayer);
        BuildExpSealWindow();
        Net.I.ExpSealEvent += OnExpSeal;
    }

    private void ExpSealDispose() => Net.I.ExpSealEvent -= OnExpSeal;

    private void BuildExpSealWindow()
    {
        _expSealWindow = new HudWindow("exp_seal", ExpSealTitle, ExpSealBodyWidth) { Visible = false };
        _expSealWindow.Closed += CloseExpSeal;
        _expSealLayer.AddChild(_expSealWindow);
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 10);
        _expSealWindow.Body.AddChild(body);

        _expSealJar = new TextureRect
        {
            CustomMinimumSize = new Vector2(ExpSealJarSize, ExpSealJarSize),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            TooltipText = ItemData.Text(ExpSeal.HelpText, "[Experience Seal System]"),
        };
        var jarFrame = UiTheme.Section();
        jarFrame.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        jarFrame.AddChild(_expSealJar);
        body.AddChild(jarFrame);

        _expSealPercent = UiTheme.Text("", 13, UiTheme.TextHi, HorizontalAlignment.Center);
        body.AddChild(_expSealPercent);
        _expSealAmount = UiTheme.Text("", 12, UiTheme.TextLo, HorizontalAlignment.Center);
        body.AddChild(_expSealAmount);

        var toggle = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        toggle.AddThemeConstantOverride("separation", 10);
        toggle.AddChild(UiTheme.Text(ExpSealTitle, 13, UiTheme.TextHi));
        _expSealOn = UiTheme.SmallButton("ON", "");
        _expSealOn.Pressed += () => Net.I.SendExpSealToggle(true);
        _expSealOff = UiTheme.SmallButton("OFF", "");
        _expSealOff.Pressed += () => Net.I.SendExpSealToggle(false);
        toggle.AddChild(_expSealOn);
        toggle.AddChild(_expSealOff);
        body.AddChild(toggle);

        var store = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        store.AddThemeConstantOverride("separation", 8);
        _expSealPick = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        _expSealPick.AddItem(ItemData.Text(ExpSeal.ObtainText, "Obtain EXP"));
        foreach (var amount in ExpSeal.Amounts) _expSealPick.AddItem(amount.ToString("N0"));
        store.AddChild(_expSealPick);
        var start = UiTheme.ActionButton(ItemData.Text(ExpSeal.ObtainText, "Obtain EXP"), "");
        start.Pressed += StoreSealedExp;
        store.AddChild(start);
        body.AddChild(store);
        RenderExpSeal();
    }

    private void OpenExpSeal(int jarSlot = -1)
    {
        _expSealJarPlaced = jarSlot >= 0;
        _expSealJar.Texture = _expSealJarPlaced ? ItemData.Icon(ExpSeal.EmptyJar) : null;
        RenderExpSeal();
        ShowOnTop(_expSealWindow);
    }

    private void CloseExpSeal()
    {
        _expSealWindow.Visible = false;
        _expSealJarPlaced = false;
        _expSealJar.Texture = null;
        _expSealPick.Selected = 0;
    }

    private void RenderExpSeal()
    {
        long total = Net.I.SealedExperience;
        _expSealPercent.Text = $"Sealed EXP: {(int)ExpSeal.Percent(total)} %";
        _expSealAmount.Text = $"( {total:N0} / {ExpSeal.Capacity:N0} )";
        _expSealOn.Modulate = Net.I.ExpSealState == ExpSeal.StateOn ? UiTheme.GoldBright : Colors.White;
        _expSealOff.Modulate = Net.I.ExpSealState == ExpSeal.StateOff ? UiTheme.GoldBright : Colors.White;
    }

    private void StoreSealedExp()
    {
        long amount = _expSealPick.Selected > 0 ? ExpSeal.Amounts[_expSealPick.Selected - 1] : 0;
        if (ExpSeal.LocalRefusal(_expSealJarPlaced, Net.I.SealedExperience, amount) is { } refusal)
        {
            CombatNotice(ItemData.Text(refusal, ""), ExpSealRefusalColour);
            return;
        }
        Net.I.SendExpSealStore(FirstBagPos(ExpSeal.EmptyJar), amount);
    }

    private byte FirstBagPos(int itemId)
    {
        for (int slot = BagStart; slot < Inv.Length; slot++)
            if (Inv[slot].ItemId == itemId) return (byte)(slot - BagStart);
        return ExpSeal.NoBagPos;
    }

    private void UseExpJar(int flag)
    {
        int jar = ExpSeal.JarForFlag(flag);
        if (jar != 0) Net.I.SendExpSealUse(jar, FirstBagPos(jar));
    }

    private void OnExpSeal(ExpSealReply reply)
    {
        switch (reply.Sub)
        {
            case ExpSeal.SubStore:
                if (reply.Code == ExpSeal.Succeeded)
                {
                    _expSealJarPlaced = false;
                    _expSealJar.Texture = null;
                }
                else if (ExpSeal.StoreRefusal(reply.Code) is { } refusal)
                    CombatNotice(ItemData.Text(refusal, ""), ExpSealRefusalColour);
                break;
            case ExpSeal.SubTotal when Net.I.SealedGain > 0:
                CombatNotice(ItemData.Text(ExpSeal.GainText, "%I64d experience sealed in the pot.")
                    .Replace("%I64d", Net.I.SealedGain.ToString()), ExpSealGainColour);
                break;
            case ExpSeal.SubUseJar when ExpSeal.UseRefusal(reply.Code) is { } use:
                string text = ItemData.Text(use.Id, "");
                CombatNotice(use.WithCode ? $"{text}({reply.Code})" : text, ExpSealRefusalColour);
                break;
        }
        RenderExpSeal();
        UpdateExpBar();
    }
}
