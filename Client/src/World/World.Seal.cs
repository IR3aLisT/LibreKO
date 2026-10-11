using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const int SealFee = 1_000_000;
    private const int SealCodeLength = 4;
    private const int SealStoneItem = 810_890_000;
    private const int SealConfirmText = 15110;
    private const int UnsealConfirmText = 15111;
    private const int UnbindStonesText = 4613;
    private const int SealShortFeeText = 15114;
    private const int SealWrongCodeText = 15107;
    private const int SealPremiumOnlyText = 15105;
    private const int SealFailedText = 15115;
    private const int UnsealFailedText = 15116;
    private const int SealTooSoonText = 19008;
    private const int SealNoVaultPasswordText = 32150;
    private const int SealWrongVaultPasswordText = 15113;
    private const int SealVaultLockedText = 1669;
    private const int BindFailedText = 19006;
    private const int UnbindNotSealableText = 19000;
    private const int UnbindMissingStonesText = 19005;
    private const int UnbindFailedText = 19002;
    private const string SealCodePrompt = "Enter your VIP vault password";
    private const string SealItemChangedText = "The item in the socket changed. Check it and confirm again.";
    private const string SealCodeChangedText = SealCodePrompt + " and confirm again.";

    internal enum SealMode { Secret, Bind }

    private static string SealWindowTitle(SealMode mode) =>
        mode == SealMode.Secret ? "Item Seal / Unseal" : "Item Bind / Release";

    private CanvasLayer _sealLayer = null!;
    private HudWindow _sealPanel = null!;
    private WarehouseCell _sealSocket = null!;
    private BagCompanion? _sealCompanion;
    private Label _sealHeadline = null!;
    private Label _sealPrompt = null!;
    private VBoxContainer _sealCodeRow = null!;
    private LineEdit _sealCodeField = null!;
    private PanelContainer _sealPad = null!;
    private Label _sealGold = null!;
    private Button _sealConfirm = null!;

    private SealMode _sealMode = SealMode.Secret;
    private int _sealSlot = -1;
    private string _sealCode = "";
    private bool _sealShown;
    private bool _sealWaiting;
    private Notice? _sealNotice;

    private void SealInit()
    {
        BuildSealWindow();
        Net.I.ItemSealEvent += OnItemSeal;
        Net.I.InventorySlotEvent += OnSealInventorySlot;
    }

    private void SealDispose()
    {
        Net.I.ItemSealEvent -= OnItemSeal;
        Net.I.InventorySlotEvent -= OnSealInventorySlot;
    }

    private void OnSealInventorySlot(int abs, ItemSlot item)
    {
        if (_sealShown) RefreshSealWindow();
    }

    private static ItemSealType SealActionFor(SealMode mode, ItemFlag state) => mode switch
    {
        SealMode.Secret => state == ItemFlag.Sealed ? ItemSealType.Unseal : ItemSealType.Seal,
        _ => state == ItemFlag.Bound ? ItemSealType.Unbind : ItemSealType.Bind,
    };

    private void OpenSealWindow(SealMode mode)
    {
        _sealMode = mode;
        _sealSlot = -1;
        _sealCode = "";
        DismissSealNotice();
        _sealPad.Visible = false;
        _sealPanel.Title = SealWindowTitle(mode);
        _sealPanel.Visible = true;
        _sealShown = true;
        _sealCompanion ??= new BagCompanion(SealTakeFromBag, SealBagFit, _ => "", CloseSealWindow);
        AttachBagCompanion(_sealCompanion);
        RefreshSealWindow();
    }

    private bool SealTakeFromBag(int abs)
    {
        if (!InMainBag(abs)) return false;
        TakeSealSocket(abs - GridStart);
        return true;
    }

    private BagFit SealBagFit(int abs) =>
        abs == _sealSlot ? BagFit.Staged : SealSocketRefusal(Inv[abs]) == null ? BagFit.Normal : BagFit.Unfit;

    private void CloseSealWindow()
    {
        if (!_sealShown) return;
        _sealShown = false;
        _sealSlot = -1;
        _sealCode = "";
        _sealPanel.Visible = false;
        _sealPad.Visible = false;
        DismissSealNotice();
        HideItemTooltip();
        if (_sealCompanion != null) DetachBagCompanion(_sealCompanion);
    }

    private void RefreshSealWindow()
    {
        if (_sealSlot >= 0 && (_sealSlot >= Inv.Length || Inv[_sealSlot].IsEmpty))
            _sealSlot = -1;

        var held = _sealSlot >= 0 ? Inv[_sealSlot] : default;
        _sealSocket.Set(held);

        _sealGold.Text = $"{Sheet.Gold:n0}";
        var action = SealActionFor(_sealMode, held.State);
        bool needsCode = !held.IsEmpty && SealNeedsCode(action);
        _sealCodeRow.Visible = needsCode;
        _sealCodeField.Text = new string('*', _sealCode.Length);

        _sealHeadline.Text = held.IsEmpty
            ? "Place an item in the socket."
            : action switch
            {
                ItemSealType.Seal => "This item will be sealed.",
                ItemSealType.Unseal => "The seal on this item will be lifted.",
                ItemSealType.Bind => "This item will be bound to you.",
                _ => "The binding on this item will be released.",
            };

        _sealPrompt.Text = held.IsEmpty ? "" : action switch
        {
            ItemSealType.Seal => $"{SealFee:n0} gold",
            ItemSealType.Unbind => UnbindCostLine(held),
            _ => "",
        };

        _sealConfirm.Disabled = held.IsEmpty || _sealWaiting || (needsCode && _sealCode.Length != SealCodeLength);
        RefreshBagFit();
    }

    private static bool SealNeedsCode(ItemSealType action) => action != ItemSealType.Bind;

    private string UnbindCostLine(ItemSlot held)
    {
        int stones = UnbindStonesFor(held.ItemId);
        if (stones <= 0) return "Free of charge";
        string cost = TextTemplate.Fill(
            ItemData.Text(UnbindStonesText, "Number of seal item needed to unbound: %d"), stones);
        return $"{cost} (you have {CarriedSealStones():n0})";
    }

    private static int UnbindStonesFor(int itemId) =>
        ItemData.Get(itemId) is { } def ? ItemData.UnbindStones(def, ItemData.ExtFor(itemId)) : 0;

    private int CarriedSealStones()
    {
        int total = 0;
        for (int abs = GridStart; abs < Inv.Length; abs++)
            if (Inv[abs].ItemId == SealStoneItem) total += Mathf.Max(1, (int)Inv[abs].Count);
        return total;
    }

    private const int TextCannotSeal = 15101;
    private const int TextDuplicateSerial = 10760;
    private const int TextSealBindStatus = 19017;

    private void TakeSealSocket(int bagIndex)
    {
        if (_sealWaiting || _sealNotice != null) return;
        int abs = GridStart + bagIndex;
        if (abs >= Inv.Length || Inv[abs].IsEmpty) return;
        if (SealSocketRefusal(Inv[abs]) is { } refusal)
        {
            CombatNotice(refusal);
            return;
        }
        _sealSlot = abs;
        RefreshSealWindow();
    }

    private static string? SealSocketRefusal(ItemSlot slot) => ItemData.ShownFlag(slot) switch
    {
        ItemFlag.CharacterSeal or ItemFlag.Duplicate =>
            ItemData.Text(TextDuplicateSerial, "Items with duplicated serial number cannot be upgraded."),
        ItemFlag.NotBound => ItemData.Text(TextSealBindStatus, "You cannot register an item on seal/bind status."),
        _ when (ItemData.Get(slot.ItemId)?.Countable ?? 0) != 0 || slot.ExpiresAt != 0 =>
            ItemData.Text(TextCannotSeal, "Item cannot be sealed."),
        _ => null,
    };

    private void ClearSealSocket()
    {
        if (_sealWaiting || _sealNotice != null) return;
        _sealSlot = -1;
        RefreshSealWindow();
    }

    private void AskSealConfirm()
    {
        if (_sealWaiting || _sealNotice != null || _sealConfirm.Disabled) return;
        if (_sealSlot < 0 || _sealSlot >= Inv.Length || Inv[_sealSlot].IsEmpty) return;

        var action = SealActionFor(_sealMode, Inv[_sealSlot].State);
        string question = action switch
        {
            ItemSealType.Seal => ItemData.Text(SealConfirmText,
                $"It costs {SealFee:n0} gold and the item can't be traded, upgraded or destroyed until unsealed. "
                + "Would you like to seal your item now?"),
            ItemSealType.Unseal => ItemData.Text(UnsealConfirmText, "Would you like to lift the seal?"),
            ItemSealType.Bind => "Bind this item to you?",
            _ => "Release the binding?",
        };
        int slot = _sealSlot;
        var item = Inv[slot];
        _sealNotice = Notice.Confirm(this, question, "Confirm", "Cancel",
            () =>
            {
                _sealNotice = null;
                SendSeal(slot, item);
            },
            () => _sealNotice = null,
            SealWindowTitle(_sealMode));
        Audio.PlayUi(Sfx.MsgBoxPop);
    }

    private void DismissSealNotice()
    {
        if (_sealNotice != null && GodotObject.IsInstanceValid(_sealNotice)) _sealNotice.Close();
        _sealNotice = null;
    }

    private void SendSeal(int slot, ItemSlot item)
    {
        if (_sealWaiting || !_sealShown) return;
        if (_sealSlot != slot || !Inv.Holds(slot, item))
        {
            CombatNotice(SealItemChangedText);
            RefreshSealWindow();
            return;
        }
        if (_sealMode == SealMode.Secret && _sealCode.Length != SealCodeLength)
        {
            CombatNotice(SealCodeChangedText);
            RefreshSealWindow();
            return;
        }

        _sealWaiting = true;
        RefreshSealWindow();
        Net.I.SendItemSeal(
            SealActionFor(_sealMode, item.State),
            item.ItemId,
            (byte)(slot - GridStart),
            _sealCode);
    }

    private void OnItemSeal(ItemSealType sealType, ItemSealResult result, int itemId, int srcPos)
    {
        _sealWaiting = false;
        if (sealType == ItemSealType.Bind && FinishBindBeforeEquip(result, srcPos)) return;
        int abs = srcPos >= 0 ? GridStart + srcPos : _sealSlot;

        if (result != ItemSealResult.Succeeded)
        {
            CombatLogAdd(SealRefusal(sealType, result), CombatLogKind.Status, WhRefusalTint);
            if (result == ItemSealResult.CodeLockedOut)
                CombatNotice("The VIP vault password stays locked until you log in again.");
            _sealCode = "";
            RefreshSealWindow();
            return;
        }

        if (Inv.Holds(abs, itemId)) ApplySealFlag(abs, sealType);
        CombatNotice(sealType switch
        {
            ItemSealType.Seal => "Item sealed.",
            ItemSealType.Unseal => "Seal lifted.",
            ItemSealType.Bind => "Item bound to you.",
            _ => "Binding released.",
        });
        _sealCode = "";
        RefreshSealWindow();
    }

    private void ApplySealFlag(int absSlot, ItemSealType sealType)
    {
        if (absSlot < 0 || absSlot >= Inv.Length || Inv[absSlot].IsEmpty) return;

        var slot = Inv[absSlot];
        slot.Flag = (byte)(sealType switch
        {
            ItemSealType.Seal => ItemFlag.Sealed,
            ItemSealType.Unseal => ItemFlag.Unsealed,
            ItemSealType.Bind => ItemFlag.Bound,
            _ => ItemFlag.NotBound,
        });
        Inv[absSlot] = slot;
        Net.I.MirrorInventorySlot(absSlot, slot);

        if (CharTabOpen()) RefreshInventoryUI();
    }

    private static string SealRefusal(ItemSealType sealType, ItemSealResult result)
    {
        bool secret = sealType is ItemSealType.Seal or ItemSealType.Unseal;
        return (sealType, result) switch
        {
            (_, ItemSealResult.TooSoon) =>
                ItemData.Text(SealTooSoonText, "Please try again. You may not repeat this function instantly."),
            (ItemSealType.Bind, _) => ItemData.Text(BindFailedText, "Item restoration failed."),
            (_, ItemSealResult.NoCodeSet) => ItemData.Text(SealNoVaultPasswordText,
                "There is no password set for the VIP Vault. Visit the Inn Hostess to set the VIP Vault password."),
            (_, ItemSealResult.CodeLockedOut) => ItemData.Text(SealVaultLockedText,
                "You have inserted wrong Password more than 3 times."),
            (_, ItemSealResult.WrongVaultPassword) => ItemData.Text(SealWrongVaultPasswordText, "Wrong VIP Key Password"),
            (ItemSealType.Unbind, ItemSealResult.WrongCode) =>
                ItemData.Text(SealWrongVaultPasswordText, "Wrong VIP Key Password"),
            (_, ItemSealResult.WrongCode) => ItemData.Text(SealWrongCodeText, "Wrong Security Code"),
            (_, ItemSealResult.MissingMaterial) when secret => ItemData.Text(SealPremiumOnlyText,
                "Item Sealing/Unsealing functions are available to Gold and Platinum Premium users ONLY."),
            (ItemSealType.Unbind, ItemSealResult.MissingMaterial) =>
                ItemData.Text(UnbindMissingStonesText, "Insufficient items to perform the seal."),
            (ItemSealType.Seal, ItemSealResult.NeedCoins) =>
                ItemData.Text(SealShortFeeText, "You are short on the seal lifting fee."),
            (ItemSealType.Unbind, ItemSealResult.Failed or ItemSealResult.NotSealable) =>
                ItemData.Text(UnbindNotSealableText, "This item cannot be sealed."),
            (ItemSealType.Seal, _) => ItemData.Text(SealFailedText, "Seal failed."),
            (ItemSealType.Unseal, _) => ItemData.Text(UnsealFailedText, "Seal lift failed."),
            _ => ItemData.Text(UnbindFailedText, "Sealing the item failed."),
        };
    }

    private void BuildSealWindow()
    {
        _sealLayer = new CanvasLayer { Layer = 75 };
        AddChild(_sealLayer);

        _sealPanel = new HudWindow("seal", SealWindowTitle(SealMode.Secret))
        {
            Visible = false,
        };
        _sealPanel.Closed += CloseSealWindow;
        _sealLayer.AddChild(_sealPanel);

        var root = new VBoxContainer { CustomMinimumSize = new Vector2(304, 0) };
        root.AddThemeConstantOverride("separation", 9);
        _sealPanel.Body.AddChild(root);

        var stage = UiTheme.Section();
        root.AddChild(stage);
        var stageBox = new VBoxContainer();
        stageBox.AddThemeConstantOverride("separation", 7);
        stage.AddChild(stageBox);

        var socketRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        socketRow.AddThemeConstantOverride("separation", 10);
        stageBox.AddChild(socketRow);

        _sealSocket = new WarehouseCell(0);
        _sealSocket.CustomMinimumSize = new Vector2(52, 52);
        _sealSocket.OnActivate += _ => ClearSealSocket();
        _sealSocket.OnHover += HoverWarehouseCell;
        _sealSocket.OnHoverEnd += HideItemTooltip;
        socketRow.AddChild(_sealSocket);

        var socketText = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        socketText.AddThemeConstantOverride("separation", 2);
        socketRow.AddChild(socketText);
        _sealHeadline = UiTheme.Text("", 13, UiTheme.TextHi);
        _sealHeadline.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _sealHeadline.CustomMinimumSize = new Vector2(214, 0);
        socketText.AddChild(_sealHeadline);
        _sealPrompt = UiTheme.Text("", 11, UiTheme.TextDim);
        _sealPrompt.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _sealPrompt.CustomMinimumSize = new Vector2(214, 0);
        socketText.AddChild(_sealPrompt);

        _sealCodeRow = new VBoxContainer();
        _sealCodeRow.AddThemeConstantOverride("separation", 4);
        stageBox.AddChild(_sealCodeRow);
        _sealCodeRow.AddChild(UiTheme.Text(
            SealCodePrompt, 11, UiTheme.TextDim, HorizontalAlignment.Center));

        _sealCodeField = new LineEdit
        {
            Editable = false,
            Alignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        _sealCodeField.AddThemeFontSizeOverride("font_size", 15);
        _sealCodeField.GuiInput += OnSealCodeFieldInput;
        _sealCodeRow.AddChild(_sealCodeField);

        BuildSealKeypad();
        _sealCodeRow.AddChild(_sealPad);

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 10);
        root.AddChild(buttons);
        _sealConfirm = UiTheme.SmallButton("Confirm", "");
        _sealConfirm.CustomMinimumSize = new Vector2(96, 28);
        _sealConfirm.Pressed += AskSealConfirm;
        buttons.AddChild(_sealConfirm);
        var cancel = UiTheme.SmallButton("Cancel", "");
        cancel.CustomMinimumSize = new Vector2(96, 28);
        cancel.Pressed += CloseSealWindow;
        buttons.AddChild(cancel);

        var bagSection = UiTheme.Section();
        root.AddChild(bagSection);
        var bagBox = new VBoxContainer();
        bagBox.AddThemeConstantOverride("separation", 6);
        bagSection.AddChild(bagBox);

        var footer = new HBoxContainer();
        bagBox.AddChild(footer);
        var goldLabel = UiTheme.Text("Gold", 12, UiTheme.TextLo);
        goldLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        footer.AddChild(goldLabel);
        _sealGold = UiTheme.Text("", 12, UiTheme.Gold, HorizontalAlignment.Right);
        footer.AddChild(_sealGold);
    }

    private void BuildSealKeypad()
    {
        _sealPad = new PanelContainer { Visible = false };
        _sealPad.AddThemeStyleboxOverride("panel", UiTheme.WindowPanel());

        var margin = new MarginContainer();
        UiTheme.Margins(margin, 8);
        _sealPad.AddChild(margin);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 4);
        margin.AddChild(box);

        var grid = new GridContainer { Columns = 4 };
        grid.AddThemeConstantOverride("h_separation", 4);
        grid.AddThemeConstantOverride("v_separation", 4);
        box.AddChild(grid);

        foreach (int digit in SealKeypadOrder())
            grid.AddChild(SealKey(digit.ToString(), () => PushSealDigit(digit)));

        grid.AddChild(SealKey("⌫", () =>
        {
            if (_sealCode.Length > 0) _sealCode = _sealCode[..^1];
            RefreshSealWindow();
        }));
        grid.AddChild(SealKey("C", () => { _sealCode = ""; RefreshSealWindow(); }));

        var done = UiTheme.SmallButton("Done", "");
        done.CustomMinimumSize = new Vector2(0, 30);
        done.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        done.Pressed += () => _sealPad.Visible = false;
        box.AddChild(done);
    }

    private static Button SealKey(string label, System.Action onPressed)
    {
        var key = UiTheme.SmallButton(label, "");
        key.CustomMinimumSize = new Vector2(52, 32);
        key.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        key.Pressed += () => onPressed();
        return key;
    }

    private static int[] SealKeypadOrder()
    {
        var order = new int[10];
        for (int i = 0; i < 10; i++) order[i] = i;
        for (int i = order.Length - 1; i > 0; i--)
        {
            int j = (int)(GD.Randi() % (uint)(i + 1));
            (order[i], order[j]) = (order[j], order[i]);
        }
        return order;
    }

    private void PushSealDigit(int digit)
    {
        if (_sealCode.Length >= SealCodeLength) return;
        _sealCode += (char)('0' + digit);
        RefreshSealWindow();
        if (_sealCode.Length == SealCodeLength) _sealPad.Visible = false;
    }

    private void OnSealCodeFieldInput(InputEvent ev)
    {
        if (ev is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            return;

        _sealPad.Visible = !_sealPad.Visible;
        _sealCodeField.AcceptEvent();
    }
}
