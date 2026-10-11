using System.Linq;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const int VipWhSlots = Net.VipWarehouseSlots;
    private const int VipWhColumns = 6;
    private const float VipWhCellSize = 44f;
    private const string VipWhHint = "Right-click to store or take out · drag to place";
    private const int VipPinTriesAllowed = 3;
    private const int VipExpiredText = 1655;
    private const int VipKeyFailedText = 1656;
    private const int VipNotAKeyText = 1657;
    private const int VipChoosePinText = 1658;
    private const int VipConfirmPinText = 1659;
    private const int VipPinSetText = 1661;
    private const int VipPinSetFailedText = 1662;
    private const int VipPinClearedText = 1663;
    private const int VipPinClearFailedText = 1664;
    private const int VipPinChangedText = 1665;
    private const int VipPinChangeFailedText = 1666;
    private const int VipEnterPinText = 1667;
    private const int VipWrongPinText = 1668;
    private const int VipPinLockedText = 1669;
    private const int VipPinDigitsText = 1670;
    private const int VipPinMismatchText = 1671;
    private const int VipRentalCapText = 1682;
    private const int VipKeyStaysText = 1651;
    private const int VipExtendAskText = 1653;
    private const int VipAddAskText = 1654;
    private const int VipExpiredUseText = 1673;
    private const int VipNoKeyUseText = 1674;
    private const int VipSetPinHintText = 1675;
    private const int VipChangePinHintText = 1676;
    private const int VipVaultKeyLook = 800_442_000;
    private const double VipClockSeconds = 1.0;
    private static readonly HashSet<int> VipVaultKeys = new() { 800_442_000, 931_695_000, 810_442_000, 998_019_000 };

    private static bool IsVipVaultKey(int itemId) => VipVaultKeys.Contains(itemId);

    private readonly ItemSlot[] _vipWh = new ItemSlot[VipWhSlots];
    private uint _vipWhExpiresAt;
    private bool _vipWhOpen;
    private bool _vipWhPinSet;
    private ItemSlotView _vipKeyCell = null!;
    private Button _vipSetPinBtn = null!, _vipClearPinBtn = null!;
    private Godot.Timer _vipWhClock = null!;
    private Notice? _vipKeyAsk;

    private CanvasLayer _vipWhLayer = null!;
    private HudWindow _vipWhPanel = null!;
    private bool _vipWhShown;
    private readonly ItemSlotView[] _vipWhCells = new ItemSlotView[VipWhSlots];
    private BagCompanion? _vipCompanion;
    private Label _vipWhExpiryLbl = null!, _vipWhSlotsNote = null!;
    private StatusLabel _vipWhStatus = null!;
    private QuantityPrompt _vipWhAmount = null!;

    private AcceptDialog _vipWhPinDlg = null!;
    private LineEdit _vipWhPinEdit = null!;
    private Label _vipWhPinPrompt = null!;
    private byte _vipWhPinSub;
    private string? _vipWhPinFirst;

    private struct VipWhPending { public byte Op; public bool Merge; public int InvAbs; public int VipIdx; public int VipTo; public int Count; }
    private VipWhPending _vipWhPending;
    private bool _vipWhInFlight;

    private void VipWarehouseInit()
    {
        BuildVipWarehousePanel();
        Net.I.VipWarehouseContentsEvent += OnVipWarehouseContents;
        Net.I.VipWarehouseResultEvent += OnVipWarehouseResult;
        Net.I.VipWarehouseExpiredEvent += OnVipWarehouseExpired;
        Net.I.VipWarehousePinPromptEvent += OnVipWarehousePinPrompt;
        Net.I.VipWarehousePinResultEvent += OnVipWarehousePinResult;
        Net.I.VipWarehousePinRejectedEvent += OnVipWarehousePinRejected;
        Net.I.VipVaultRenewedEvent += OnVipVaultRenewed;
        Net.I.VipVaultKeyExpiredEvent += OnVipVaultKeyExpired;
    }

    private void VipWarehouseDispose()
    {
        Net.I.VipWarehouseContentsEvent -= OnVipWarehouseContents;
        Net.I.VipWarehouseResultEvent -= OnVipWarehouseResult;
        Net.I.VipWarehouseExpiredEvent -= OnVipWarehouseExpired;
        Net.I.VipWarehousePinPromptEvent -= OnVipWarehousePinPrompt;
        Net.I.VipWarehousePinResultEvent -= OnVipWarehousePinResult;
        Net.I.VipWarehousePinRejectedEvent -= OnVipWarehousePinRejected;
        Net.I.VipVaultRenewedEvent -= OnVipVaultRenewed;
        Net.I.VipVaultKeyExpiredEvent -= OnVipVaultKeyExpired;
    }

    private void OnVipVaultRenewed(VipVaultKeyReply reply)
    {
        string? refusal = reply.Result switch
        {
            Net.VipRejected => ItemData.Text(VipKeyFailedText, "You have failed to use [VIP]Storage Key."),
            Net.VipNotAVaultKey => ItemData.Text(VipNotAKeyText, "It is not [VIP]Storage Key."),
            Net.VipRentalAtCap => ItemData.Text(VipRentalCapText, "You cannot extent the period if the storage has more than 60 days."),
            _ => null,
        };
        if (refusal != null) { CombatNotice(refusal); return; }
        if (reply.Result != Net.VipSuccess || reply.Storage != VipVaultContents.OwnStorage) return;
        _vipWhExpiresAt = reply.ExpiresAt;
        _vipWhOpen = true;
        if (_vipWhShown) RefreshVipWarehouse();
        else CombatNotice(ExpirationText((int)reply.ExpiresAt));
    }

    private void OnVipVaultKeyExpired()
    {
        _vipWhOpen = false;
        CloseVipWarehouse();
        CombatNotice(ItemData.Text(VipExpiredText, "VIP Storage has been expired."));
    }

    private void AskVipKey(int abs)
    {
        var key = Inv[abs];
        if (!IsVipVaultKey(key.ItemId) || (_vipKeyAsk != null && IsInstanceValid(_vipKeyAsk))) return;
        string ask = TextTemplate.Fill(_vipWhOpen
            ? ItemData.Text(VipExtendAskText, "Do you want to extend \n%d VIP Storage?")
            : ItemData.Text(VipAddAskText, "Do you want to add \n%d VIP Storage?"), VipVaultContents.OwnStorage + 1);
        string note = ItemData.Text(VipKeyStaysText, "You cannot remove used [VIP]Storage Key.");
        _vipKeyAsk = Notice.Confirm(this, ask + "\n" + note, "Yes", "No", () =>
        {
            if (Inv[abs].ItemId != key.ItemId) return;
            Net.I.SendVipVaultUseKey(key.ItemId, (byte)(abs - GridStart));
        }, title: "VIP Vault");
    }

    private string VipLockedText() => _vipWhExpiresAt > 0
        ? ItemData.Text(VipExpiredUseText, "You cannot use it due to expiration.")
        : ItemData.Text(VipNoKeyUseText, "You cannot use it without [VIP]Storage Key.");

    private void RefuseLockedVault() => _vipWhStatus.Status(VipLockedText(), bad: true);

    private void BuildVipWarehousePanel()
    {
        _vipWhLayer = new CanvasLayer { Layer = 74 };
        AddChild(_vipWhLayer);

        _vipWhPanel = new HudWindow("vipwarehouse", "VIP Vault") { Visible = false };
        _vipWhPanel.Closed += CloseVipWarehouse;
        _vipWhLayer.AddChild(_vipWhPanel);

        _vipWhAmount = new QuantityPrompt(76);
        AddChild(_vipWhAmount);

        var root = _vipWhPanel.Body;
        root.AddThemeConstantOverride("separation", 6);
        root.AddChild(ServiceKit.Section("Stored items", UiIcons.Get("system/package"), out _vipWhSlotsNote));

        var well = ServiceKit.Well();
        root.AddChild(well);
        var grid = new GridContainer { Columns = VipWhColumns };
        grid.AddThemeConstantOverride("h_separation", 4);
        grid.AddThemeConstantOverride("v_separation", 4);
        well.AddChild(grid);
        for (int i = 0; i < VipWhSlots; i++)
        {
            var cell = new ItemSlotView(VipWhCellSize) { Index = i, Veiled = true };
            cell.RightClicked += c => AskVipWithdraw(c.Index, -1);
            cell.Hovered += c => ShowItemTooltip(-1, c.Item);
            cell.Unhovered += _ => HideItemTooltip();
            cell.DragOut = c => _vipWhOpen ? (Variant)new Godot.Collections.Dictionary { { "companionFrom", c.Index } } : default;
            cell.CanDrop = (c, data) => CanDropOnVipVault(c.Index, data);
            cell.Dropped = (c, data) => DropOnVipVault(c.Index, data);
            _vipWhCells[i] = cell;
            grid.AddChild(cell);
        }

        var expiryRow = new HBoxContainer();
        expiryRow.AddThemeConstantOverride("separation", 8);
        _vipKeyCell = new ItemSlotView(VipWhCellSize);
        _vipKeyCell.Hovered += c => ShowItemTooltip(-1, c.Item);
        _vipKeyCell.Unhovered += _ => HideItemTooltip();
        expiryRow.AddChild(_vipKeyCell);
        var el = UiTheme.Text("Expires in", 12, UiTheme.TextLo);
        el.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        el.VerticalAlignment = VerticalAlignment.Center;
        expiryRow.AddChild(el);
        _vipWhExpiryLbl = UiTheme.Text("—", 13, UiTheme.Gold, HorizontalAlignment.Right);
        _vipWhExpiryLbl.VerticalAlignment = VerticalAlignment.Center;
        expiryRow.AddChild(_vipWhExpiryLbl);
        root.AddChild(expiryRow);

        _vipWhStatus = new StatusLabel { Hint = VipWhHint };
        root.AddChild(_vipWhStatus);

        var footer = new FooterBand();
        _vipSetPinBtn = UiTheme.SmallButton("Set PIN", "");
        _vipSetPinBtn.Pressed += () => ShowVipPinDialog(_vipWhPinSet ? Net.VipWhChangePinSub : Net.VipWhSetPinSub,
            ItemData.Text(VipChoosePinText, "Please select a 4 digit password."));
        _vipClearPinBtn = UiTheme.SmallButton("Clear PIN", "Remove the vault PIN");
        _vipClearPinBtn.Pressed += () => Net.I.SendVipWarehouseCancelPin();
        footer.Right.AddChild(_vipSetPinBtn);
        footer.Right.AddChild(_vipClearPinBtn);
        root.AddChild(footer);

        _vipWhClock = new Godot.Timer { WaitTime = VipClockSeconds };
        _vipWhClock.Timeout += TickVipWhClock;
        _vipWhLayer.AddChild(_vipWhClock);

        _vipWhPinDlg = new AcceptDialog { Title = "VIP Vault PIN", Unresizable = true };
        _vipWhPinDlg.GetOkButton().Text = "Confirm";
        var pinBox = new VBoxContainer();
        pinBox.AddThemeConstantOverride("separation", 8);
        _vipWhPinPrompt = UiTheme.Text("Enter your 4-digit PIN:", 13, UiTheme.TextHi);
        pinBox.AddChild(_vipWhPinPrompt);
        _vipWhPinEdit = new LineEdit
        {
            PlaceholderText = "1234",
            MaxLength = 4,
            Secret = true,
            CustomMinimumSize = new Vector2(160, 0),
        };
        _vipWhPinEdit.TextSubmitted += _ => SubmitVipPin();
        pinBox.AddChild(_vipWhPinEdit);
        _vipWhPinDlg.AddChild(pinBox);
        _vipWhPinDlg.Confirmed += SubmitVipPin;
        _vipWhLayer.AddChild(_vipWhPinDlg);
    }

    private void OpenVipVaultFromBag()
    {
        if (_vipWhShown || !VipVaultAccess.ButtonOpens(_zone)) return;
        ToggleVipWarehouse();
    }

    private void OpenVipVaultByCommand()
    {
        if (_vipWhShown || !VipVaultAccess.CommandOpens(_zone)) return;
        ToggleVipWarehouse();
    }

    private void ToggleVipWarehouse()
    {
        if (_vipWhShown) { CloseVipWarehouse(); return; }
        _vipWhInFlight = false;
        Net.I.SendVipWarehouseOpen();
    }

    private void OpenVipWarehouse()
    {
        _vipWhStatus.ResetStatus();
        _vipWhPanel.Visible = true;
        _vipWhShown = true;
        _vipCompanion ??= new BagCompanion(VipTakeFromBag, VipBagFit, _ => "", CloseVipWarehouse, VipWithdrawInto);
        AttachBagCompanion(_vipCompanion);
        _vipWhClock.Start();
        RefreshVipWarehouse();
    }

    private bool VipTakeFromBag(int abs)
    {
        if (!InMainBag(abs)) return false;
        if (IsVipVaultKey(Inv[abs].ItemId)) AskVipKey(abs);
        else VipDepositSlot(abs, -1);
        return true;
    }

    private bool VipWithdrawInto(int vipIdx, int bagAbs)
    {
        AskVipWithdraw(vipIdx, bagAbs);
        return true;
    }

    private BagFit VipBagFit(int abs) =>
        UseRefusal(ItemUse.VipVault, Inv[abs]) == ItemUseRules.Allowed ? BagFit.Normal : BagFit.Unfit;

    private void CloseVipWarehouse()
    {
        _vipWhAmount.Close();
        if (!_vipWhShown) return;
        _vipWhShown = false;
        _vipWhPanel.Visible = false;
        _vipWhClock.Stop();
        HideItemTooltip();
        if (_vipCompanion != null) DetachBagCompanion(_vipCompanion);
        PluginNotifyVipVault();
    }

    private void OnVipWarehouseContents(VipVaultContents contents)
    {
        _vipWhExpiresAt = contents.ExpiresAt;
        _vipWhOpen = contents.Open;
        _vipWhPinSet = contents.PinSet;
        for (int i = 0; i < VipWhSlots && i < contents.Slots.Length; i++) _vipWh[i] = contents.Slots[i];
        if (!_vipWhShown) OpenVipWarehouse();
        else RefreshVipWarehouse();
    }

    private void OnVipWarehouseExpired()
    {
        string text = ItemData.Text(VipExpiredText, "VIP Storage has been expired.");
        CombatNotice(text);
        if (_vipWhShown) { _vipWhStatus.Status(text, bad: true); RefreshVipWarehouse(); }
    }

    private void RefreshVipWarehouse()
    {
        for (int i = 0; i < VipWhSlots; i++)
        {
            _vipWhCells[i].Set(_vipWh[i]);
            _vipWhCells[i].Locked = !_vipWhOpen;
        }
        _vipKeyCell.Set(_vipWhExpiresAt > 0
            ? new ItemSlot { ItemId = VipVaultKeyLook, Count = 1, ExpiresAt = (int)_vipWhExpiresAt }
            : default);
        _vipKeyCell.Locked = !_vipWhOpen && _vipWhExpiresAt > 0;
        _vipWhExpiryLbl.Text = VipExpiryText();
        int used = _vipWh.Count(s => !s.IsEmpty);
        _vipWhSlotsNote.Text = $"Slots used {used} / {VipWhSlots}";
        _vipWhSlotsNote.AddThemeColorOverride("font_color", used >= VipWhSlots ? UiTheme.Bad : UiTheme.TextDim);
        _vipSetPinBtn.Text = _vipWhPinSet ? "Change PIN" : "Set PIN";
        _vipSetPinBtn.TooltipText = _vipWhPinSet
            ? ItemData.Text(VipChangePinHintText, "To change and cancel the password, please click the blue lock.")
            : ItemData.Text(VipSetPinHintText, "Please click the lock to set your VIP vault password.");
        _vipClearPinBtn.Visible = _vipWhPinSet;
        if (!_vipWhOpen) RefuseLockedVault();
        RefreshBagFit();
        PluginNotifyVipVault();
    }

    private void TickVipWhClock()
    {
        if (!_vipWhShown) return;
        if (_vipWhOpen && VipSecondsLeft() <= 0)
        {
            _vipWhOpen = false;
            RefreshVipWarehouse();
            return;
        }
        _vipWhExpiryLbl.Text = VipExpiryText();
    }

    private long VipSecondsLeft() => _vipWhExpiresAt - DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private string VipExpiryText()
    {
        if (_vipWhExpiresAt == 0) return "—";
        long seconds = VipSecondsLeft();
        if (!_vipWhOpen || seconds <= 0) return "expired";
        var left = TimeSpan.FromSeconds(seconds);
        if (left.Days >= 1) return $"{left.Days}d {left.Hours}h";
        if (left.Hours >= 1) return $"{left.Hours}h {left.Minutes}m";
        if (left.Minutes >= 1) return $"{left.Minutes}m {left.Seconds}s";
        return $"{left.Seconds}s";
    }

    private bool FitsInVipVault(int vipIdx, int itemId, int count) =>
        _vipWh[vipIdx].IsEmpty
        || (_vipWh[vipIdx].ItemId == itemId && IsStackable(itemId) && _vipWh[vipIdx].Count + count <= Inventory.StackMax);

    private int VipVaultDestination(int itemId, int count, out bool merge)
    {
        merge = false;
        if (IsStackable(itemId))
            for (int i = 0; i < VipWhSlots; i++)
                if (_vipWh[i].ItemId == itemId && _vipWh[i].Count + count <= Inventory.StackMax)
                { merge = true; return i; }
        for (int i = 0; i < VipWhSlots; i++) if (_vipWh[i].IsEmpty) return i;
        return -1;
    }

    private bool CanDropOnVipVault(int vipIdx, Variant data)
    {
        if (!_vipWhOpen || data.VariantType != Variant.Type.Dictionary) return false;
        var d = data.AsGodotDictionary();
        if (d.ContainsKey("invFrom")) return InMainBag(d["invFrom"].AsInt32());
        return d.ContainsKey("companionFrom") && d["companionFrom"].AsInt32() != vipIdx;
    }

    private void DropOnVipVault(int vipIdx, Variant data)
    {
        var d = data.AsGodotDictionary();
        if (d.ContainsKey("invFrom"))
        {
            int abs = d["invFrom"].AsInt32();
            if (IsVipVaultKey(Inv[abs].ItemId)) AskVipKey(abs);
            else VipDepositSlot(abs, vipIdx);
        }
        else if (d.ContainsKey("companionFrom")) MoveInsideVipVault(d["companionFrom"].AsInt32(), vipIdx);
    }

    private void VipDepositSlot(int abs, int target)
    {
        if (StorageTransferBusy || !InMainBag(abs) || Inv[abs].IsEmpty) return;
        if (!_vipWhOpen) { RefuseLockedVault(); return; }
        var slot = Inv[abs];
        if (UseRefusal(ItemUse.VipVault, slot) is not ItemUseRules.Allowed and var vaultRefusal)
        {
            _vipWhStatus.Status(ItemData.Text(vaultRefusal, "This item is non-storable"), bad: true);
            return;
        }
        if (IsStackable(slot.ItemId) && slot.Count > 1)
            _vipWhAmount.Open(ItemData.Icon(slot.ItemId), $"Store {ItemData.DisplayName(slot.ItemId)}",
                $"You carry {slot.Count:n0}", slot.Count, slot.Count, n => VipDepositCount(abs, (int)n, target), "Store");
        else
            VipDepositCount(abs, Mathf.Max(1, (int)slot.Count), target);
    }

    private void VipDepositCount(int abs, int count, int target)
    {
        if (StorageTransferBusy || !InMainBag(abs) || Inv[abs].IsEmpty || count <= 0) return;
        var slot = Inv[abs];
        count = Mathf.Min(count, slot.Count > 0 ? slot.Count : 1);
        int vipIdx;
        bool merge;
        if (target >= 0)
        {
            if (!FitsInVipVault(target, slot.ItemId, count)) { _vipWhStatus.Status("That slot is taken.", bad: true); return; }
            vipIdx = target;
            merge = !_vipWh[target].IsEmpty;
        }
        else
        {
            vipIdx = VipVaultDestination(slot.ItemId, count, out merge);
            if (vipIdx < 0) { _vipWhStatus.Status("The vault is full.", bad: true); return; }
        }
        _vipWhPending = new VipWhPending { Op = Net.VipWhInputSub, Merge = merge, InvAbs = abs, VipIdx = vipIdx, Count = count };
        _vipWhInFlight = true;
        Net.I.SendVipWarehouseInput(slot.ItemId, (byte)(abs - GridStart), (byte)vipIdx, count);
    }

    private void AskVipWithdraw(int vipIdx, int target)
    {
        if (StorageTransferBusy || vipIdx < 0 || vipIdx >= VipWhSlots || _vipWh[vipIdx].IsEmpty) return;
        if (!_vipWhOpen) { RefuseLockedVault(); return; }
        var slot = _vipWh[vipIdx];
        if (IsStackable(slot.ItemId) && slot.Count > 1)
            _vipWhAmount.Open(ItemData.Icon(slot.ItemId), $"Take out {ItemData.DisplayName(slot.ItemId)}",
                $"Stored {slot.Count:n0}", slot.Count, slot.Count, n => VipWithdrawCount(vipIdx, (int)n, target), "Take out");
        else
            VipWithdrawCount(vipIdx, Mathf.Max(1, (int)slot.Count), target);
    }

    private void VipWithdrawCount(int vipIdx, int count, int target)
    {
        if (StorageTransferBusy || vipIdx < 0 || vipIdx >= VipWhSlots || _vipWh[vipIdx].IsEmpty || count <= 0) return;
        var slot = _vipWh[vipIdx];
        count = Mathf.Min(count, slot.Count > 0 ? slot.Count : 1);
        int dest;
        bool merge;
        if (target >= 0)
        {
            if (!FitsInBag(target, slot.ItemId, count)) { _vipWhStatus.Status("That slot is taken.", bad: true); return; }
            dest = target;
            merge = !Inv[target].IsEmpty;
        }
        else
        {
            dest = BagDestination(slot.ItemId, count, out merge);
            if (dest < 0) { _vipWhStatus.Status("Your bags are full.", bad: true); return; }
        }
        _vipWhPending = new VipWhPending { Op = Net.VipWhOutputSub, Merge = merge, InvAbs = dest, VipIdx = vipIdx, Count = count };
        _vipWhInFlight = true;
        Net.I.SendVipWarehouseOutput(slot.ItemId, (byte)vipIdx, (byte)(dest - GridStart), count);
    }

    private void MoveInsideVipVault(int from, int to)
    {
        if (StorageTransferBusy || from == to || from < 0 || to < 0 || from >= VipWhSlots || to >= VipWhSlots || _vipWh[from].IsEmpty) return;
        if (!_vipWhOpen) { RefuseLockedVault(); return; }
        if (!_vipWh[to].IsEmpty) { _vipWhStatus.Status("That slot is taken.", bad: true); return; }
        _vipWhPending = new VipWhPending { Op = Net.VipWhMoveSub, VipIdx = from, VipTo = to, Count = _vipWh[from].Count };
        _vipWhInFlight = true;
        Net.I.SendVipWarehouseMoveInside(_vipWh[from].ItemId, (byte)from, (byte)to);
    }

    private void OnVipWarehouseResult(byte op, byte result)
    {
        if (!_vipWhInFlight) return;
        _vipWhInFlight = false;
        if (result == Net.VipExpired)
        {
            _vipWhOpen = false;
            if (_vipWhShown) RefreshVipWarehouse();
            return;
        }
        if (result != Net.VipSuccess)
        {
            _vipWhStatus.Status(op == Net.VipWhOutputSub ? ItemData.Text(WhNoRoomText, "Your bags are full.") : "Transfer failed.", bad: true);
            if (_vipWhShown) RefreshVipWarehouse();
            return;
        }

        var p = _vipWhPending;
        if (p.Op == Net.VipWhMoveSub)
        {
            int itemId = _vipWh[p.VipIdx].ItemId;
            _vipWh[p.VipTo] = _vipWh[p.VipIdx];
            _vipWh[p.VipIdx] = default;
            _vipWhStatus.Status($"Moved {ItemData.DisplayName(itemId)}.", bad: false);
        }
        else if (p.Op == Net.VipWhInputSub)
        {
            var bag = Inv[p.InvAbs];
            int itemId = bag.ItemId;
            MoveStack(ref bag, ref _vipWh[p.VipIdx], p.Count, p.Merge);
            Inv[p.InvAbs] = bag;
            Net.I.MirrorInventorySlot(p.InvAbs, Inv[p.InvAbs]);
            if (CharTabOpen()) RefreshInventoryUI();
            _vipWhStatus.Status($"Stored {WhItemLine(itemId, p.Count)}.", bad: false);
        }
        else
        {
            var bag = Inv[p.InvAbs];
            int itemId = _vipWh[p.VipIdx].ItemId;
            MoveStack(ref _vipWh[p.VipIdx], ref bag, p.Count, p.Merge);
            Inv[p.InvAbs] = bag;
            Net.I.MirrorInventorySlot(p.InvAbs, Inv[p.InvAbs]);
            if (CharTabOpen()) RefreshInventoryUI();
            _vipWhStatus.Status($"Took out {WhItemLine(itemId, p.Count)}.", bad: false);
        }
        if (_vipWhShown) RefreshVipWarehouse();
    }

    private void OnVipWarehousePinPrompt()
    {
        ShowVipPinDialog(Net.VipWhEnterPinSub, ItemData.Text(VipEnterPinText, "Please insert 4 digits for Password."));
    }

    private void ShowVipPinDialog(byte sub, string prompt)
    {
        _vipWhPinSub = sub;
        _vipWhPinFirst = null;
        RepromptVipPin(prompt);
    }

    private void RepromptVipPin(string prompt)
    {
        _vipWhPinPrompt.Text = prompt;
        _vipWhPinEdit.Text = "";
        _vipWhPinDlg.PopupCentered();
        _vipWhPinEdit.GrabFocus();
    }

    private void SubmitVipPin()
    {
        string pin = _vipWhPinEdit.Text.Trim();
        if (pin.Length != 4 || !IsAllDigits(pin))
        {
            RepromptVipPin(ItemData.Text(VipPinDigitsText, "You have to insert 4 digits Password."));
            return;
        }
        if (_vipWhPinSub is Net.VipWhSetPinSub or Net.VipWhChangePinSub)
        {
            if (_vipWhPinFirst == null)
            {
                _vipWhPinFirst = pin;
                RepromptVipPin(ItemData.Text(VipConfirmPinText, "Please reenter your password to confirm."));
                return;
            }
            if (_vipWhPinFirst != pin)
            {
                _vipWhPinFirst = null;
                RepromptVipPin(ItemData.Text(VipPinMismatchText, "The password is not same as initial one."));
                return;
            }
        }
        switch (_vipWhPinSub)
        {
            case Net.VipWhSetPinSub:   Net.I.SendVipWarehouseSetPin(pin); break;
            case Net.VipWhChangePinSub: Net.I.SendVipWarehouseChangePin(pin); break;
            default:                   Net.I.SendVipWarehouseEnterPin(pin); break;
        }
        _vipWhPinFirst = null;
        _vipWhPinEdit.Text = "";
        _vipWhPinDlg.Hide();
    }

    private void OnVipWarehousePinRejected(int failures)
    {
        if (failures < VipPinTriesAllowed)
        {
            ShowVipPinDialog(Net.VipWhEnterPinSub,
                TextTemplate.Fill(ItemData.Text(VipWrongPinText, "You have inserted wrong Password. (%d times)"), failures));
            return;
        }
        _vipWhPinDlg.Hide();
        CloseVipWarehouse();
        CombatLogAdd(ItemData.Text(VipPinLockedText,
            "You have inserted wrong Password more than 3 times."), CombatLogKind.Status, WhRefusalTint);
        CombatNotice("The VIP vault stays locked until you log in again.");
    }

    private void OnVipWarehousePinResult(byte sub, bool ok)
    {
        switch (sub)
        {
            case Net.VipWhEnterPinSub:
                if (!ok) ShowVipPinDialog(Net.VipWhEnterPinSub, ItemData.Text(VipEnterPinText, "Please insert 4 digits for Password."));
                return;
            case Net.VipWhSetPinSub:
                CombatNotice(ok ? ItemData.Text(VipPinSetText, "Password has set.\nVIP Storage will be closed.")
                    : ItemData.Text(VipPinSetFailedText, "Password setting has failed."));
                break;
            case Net.VipWhChangePinSub:
                CombatNotice(ok ? ItemData.Text(VipPinChangedText, "Password change has set.\nVIP Storage will be closed.")
                    : ItemData.Text(VipPinChangeFailedText, "Password change has failed."));
                break;
            case Net.VipWhCancelPinSub:
                CombatNotice(ok ? ItemData.Text(VipPinClearedText, "Password cancellation has set.")
                    : ItemData.Text(VipPinClearFailedText, "Password cancellation has failed."));
                if (ok) { _vipWhPinSet = false; if (_vipWhShown) RefreshVipWarehouse(); }
                return;
        }
        if (!ok) return;
        _vipWhPinSet = true;
        CloseVipWarehouse();
    }

    private static bool IsAllDigits(string s)
    {
        foreach (char c in s) if (c < '0' || c > '9') return false;
        return true;
    }
}
