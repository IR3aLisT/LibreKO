using System.Collections.Generic;
using System.Linq;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const int ClanWhSlots = Net.ClanWhNetSlots;
    private const int ClanWhPageSize = 24;
    private const int ClanWhPages = ClanWhSlots / ClanWhPageSize;
    private const int ClanWhColumns = 6;
    private const float ClanWhCellSize = 44f;
    private const string ClanWhHint = "Right-click to store or take out · drag to place";
    private const string ClanWhRightsNote = "Anyone may store; the chief or vice-chief takes out.";
    private const string ClanWhNotInClanText = "You are not in a clan.";
    private const string ClanWhNoRightsText = "Only the chief or vice-chief can take items out.";
    private const string ClanWhNotStorableText = "This item cannot be stored in the clan bank.";

    private readonly ItemSlot[] _clanWh = new ItemSlot[ClanWhSlots];
    private int _clanWhMoney;
    private int _clanWhPage;

    private CanvasLayer _clanWhLayer = null!;
    private HudWindow _clanWhPanel = null!;
    private bool _clanWhShown;
    private readonly ItemSlotView[] _clanWhCells = new ItemSlotView[ClanWhPageSize];
    private BagCompanion? _clanCompanion;
    private ServiceTabs _clanWhPageTabs = null!;
    private Label _clanWhSlotsNote = null!;
    private StatusLabel _clanWhStatus = null!;
    private MoneyPlaque _clanWhStored = null!;
    private Button _clanWhDepositBtn = null!, _clanWhWithdrawBtn = null!;
    private QuantityPrompt _clanWhAmount = null!;

    private struct ClanWhPending { public byte Op; public bool Gold; public bool Merge; public int InvAbs; public int WhIdx; public int WhTo; public int Count; }
    private ClanWhPending _clanWhPending;
    private bool _clanWhInFlight;

    private bool ClanCanWithdraw => MyClan.IsChief || MyClan.IsViceChief;

    private void ClanWarehouseInit()
    {
        BuildClanWarehousePanel();
        Net.I.ClanWhContentsEvent += OnClanWhContents;
        Net.I.ClanWhResultEvent += OnClanWhResult;
        Net.I.GoldChangeEvent += OnClanWhGold;
    }

    private void ClanWarehouseDispose()
    {
        Net.I.ClanWhContentsEvent -= OnClanWhContents;
        Net.I.ClanWhResultEvent -= OnClanWhResult;
        Net.I.GoldChangeEvent -= OnClanWhGold;
    }

    private void OnClanWhGold(int g) { if (_clanWhShown) RefreshClanWhGold(); }

    private void BuildClanWarehousePanel()
    {
        _clanWhLayer = new CanvasLayer { Layer = 74 };
        AddChild(_clanWhLayer);

        _clanWhPanel = new HudWindow("clanwarehouse", "Clan Warehouse") { Visible = false };
        _clanWhPanel.Closed += CloseClanWarehouse;
        _clanWhLayer.AddChild(_clanWhPanel);

        _clanWhAmount = new QuantityPrompt(76);
        AddChild(_clanWhAmount);

        var root = _clanWhPanel.Body;
        root.AddThemeConstantOverride("separation", 6);
        root.AddChild(ServiceKit.Section("Stored items", UiIcons.Get("system/package"), out _clanWhSlotsNote));

        var well = ServiceKit.Well();
        root.AddChild(well);
        var grid = new GridContainer { Columns = ClanWhColumns };
        grid.AddThemeConstantOverride("h_separation", 4);
        grid.AddThemeConstantOverride("v_separation", 4);
        well.AddChild(grid);
        for (int i = 0; i < ClanWhPageSize; i++)
        {
            var cell = new ItemSlotView(ClanWhCellSize) { Index = i, Veiled = true, PicksAmount = true };
            cell.RightClicked += c => AskClanWithdraw(ClanWhAbs(c.Index), -1);
            cell.Hovered += c => ShowItemTooltip(-1, c.Item);
            cell.Unhovered += _ => HideItemTooltip();
            cell.Wheeled += (_, step) => TurnClanWhPage(step);
            cell.DragOut = c => new Godot.Collections.Dictionary { { "companionFrom", ClanWhAbs(c.Index) } };
            cell.CanDrop = (c, data) => CanDropOnClanWh(ClanWhAbs(c.Index), data);
            cell.Dropped = (c, data) => DropOnClanWh(ClanWhAbs(c.Index), data);
            _clanWhCells[i] = cell;
            grid.AddChild(cell);
        }

        _clanWhPageTabs = new ServiceTabs();
        var pages = new List<string>();
        for (int p = 0; p < ClanWhPages; p++) pages.Add($"{p + 1}");
        _clanWhPageTabs.SetTabs(pages, 0);
        _clanWhPageTabs.Selected += page =>
        {
            _clanWhPage = page;
            RefreshClanWarehouse();
        };
        root.AddChild(_clanWhPageTabs);

        _clanWhStatus = new StatusLabel { Hint = ClanWhHint };
        root.AddChild(_clanWhStatus);

        var footer = new FooterBand();
        _clanWhStored = new MoneyPlaque("Clan gold");
        footer.Left.AddChild(_clanWhStored);
        _clanWhDepositBtn = UiTheme.SmallButton("Deposit", "Put gold into the clan bank");
        _clanWhDepositBtn.Pressed += () => AskClanGoldTransfer(deposit: true);
        _clanWhWithdrawBtn = UiTheme.SmallButton("Withdraw", "Take gold out of the clan bank");
        _clanWhWithdrawBtn.Pressed += () => AskClanGoldTransfer(deposit: false);
        footer.Right.AddChild(_clanWhDepositBtn);
        footer.Right.AddChild(_clanWhWithdrawBtn);
        root.AddChild(footer);
    }

    private int ClanWhAbs(int cell) => _clanWhPage * ClanWhPageSize + cell;

    private void ToggleClanWarehouse()
    {
        if (_clanWhShown) { CloseClanWarehouse(); return; }
        if (!MyClan.InClan) { CombatNotice(ClanWhNotInClanText); return; }
        _clanWhInFlight = false;
        _clanWhPage = 0;
        _clanWhPageTabs.Select(0, notify: false);
        _clanWhStatus.ResetStatus();
        System.Array.Clear(_clanWh, 0, _clanWh.Length);
        _clanWhMoney = 0;
        _clanWhPanel.Visible = true;
        _clanWhShown = true;
        _clanCompanion ??= new BagCompanion(ClanTakeFromBag, ClanBagFit, _ => "", CloseClanWarehouse, ClanWithdrawInto);
        AttachBagCompanion(_clanCompanion);
        Net.I.SendClanWhOpen();
        RefreshClanWarehouse();
    }

    private bool ClanTakeFromBag(int abs)
    {
        if (!InMainBag(abs)) return false;
        ClanDepositSlot(abs, -1);
        return true;
    }

    private bool ClanWithdrawInto(int whIdx, int bagAbs, int? amount)
    {
        AskClanWithdraw(whIdx, bagAbs, amount);
        return true;
    }

    private BagFit ClanBagFit(int abs) =>
        UseRefusal(ItemUse.ClanBank, Inv[abs]) == ItemUseRules.Allowed ? BagFit.Normal : BagFit.Unfit;

    private void CloseClanWarehouse()
    {
        _clanWhAmount.Close();
        if (!_clanWhShown) return;
        _clanWhShown = false;
        _clanWhPanel.Visible = false;
        HideItemTooltip();
        if (_clanCompanion != null) DetachBagCompanion(_clanCompanion);
    }

    private void OnClanWhContents(int money, ItemSlot[] slots)
    {
        _clanWhMoney = money;
        for (int i = 0; i < ClanWhSlots && i < slots.Length; i++) _clanWh[i] = slots[i];
        if (_clanWhShown) RefreshClanWarehouse();
    }

    private void TurnClanWhPage(int step)
    {
        int next = Mathf.Clamp(_clanWhPage + step, 0, ClanWhPages - 1);
        if (next == _clanWhPage) return;
        _clanWhPage = next;
        _clanWhPageTabs.Select(next, notify: false);
        RefreshClanWarehouse();
    }

    private void RefreshClanWarehouse()
    {
        for (int i = 0; i < ClanWhPageSize; i++)
            _clanWhCells[i].Set(_clanWh[ClanWhAbs(i)]);
        int used = _clanWh.Count(s => !s.IsEmpty);
        _clanWhSlotsNote.Text = $"Slots used {used} / {ClanWhSlots}";
        _clanWhSlotsNote.AddThemeColorOverride("font_color", used >= ClanWhSlots ? UiTheme.Bad : UiTheme.TextDim);
        _clanWhStatus.Hint = ClanCanWithdraw ? ClanWhHint : ClanWhRightsNote;
        RefreshClanWhGold();
        RefreshBagFit();
    }

    private void RefreshClanWhGold()
    {
        _clanWhStored.Value = _clanWhMoney;
        _clanWhDepositBtn.Disabled = Sheet.Gold <= 0;
        _clanWhWithdrawBtn.Disabled = _clanWhMoney <= 0 || !ClanCanWithdraw;
        _clanWhWithdrawBtn.TooltipText = ClanCanWithdraw ? "Take gold out of the clan bank" : ClanWhNoRightsText;
    }

    private bool FitsInClanWh(int whIdx, int itemId, int count) =>
        _clanWh[whIdx].IsEmpty
        || (_clanWh[whIdx].ItemId == itemId && IsStackable(itemId) && _clanWh[whIdx].Count + count <= Inventory.StackMax);

    private int ClanWhDestination(int itemId, int count, out bool merge)
    {
        merge = false;
        if (IsStackable(itemId))
            for (int i = 0; i < ClanWhSlots; i++)
                if (_clanWh[i].ItemId == itemId && _clanWh[i].Count + count <= Inventory.StackMax)
                { merge = true; return i; }
        for (int i = 0; i < ClanWhSlots; i++) if (_clanWh[i].IsEmpty) return i;
        return -1;
    }

    private bool CanDropOnClanWh(int whIdx, Variant data)
    {
        if (data.VariantType != Variant.Type.Dictionary) return false;
        var d = data.AsGodotDictionary();
        if (d.ContainsKey("invFrom")) return InMainBag(d["invFrom"].AsInt32());
        return d.ContainsKey("companionFrom") && d["companionFrom"].AsInt32() != whIdx;
    }

    private void DropOnClanWh(int whIdx, Variant data)
    {
        var d = data.AsGodotDictionary();
        int? amount = DragLayer.ChosenAmount(d);
        if (d.ContainsKey("invFrom")) ClanDepositSlot(d["invFrom"].AsInt32(), whIdx, amount);
        else if (d.ContainsKey("companionFrom")) MoveInsideClanWh(d["companionFrom"].AsInt32(), whIdx, amount);
    }

    private void ClanDepositSlot(int abs, int target, int? amount = null)
    {
        if (StorageTransferBusy || !InMainBag(abs) || Inv[abs].IsEmpty) return;
        var slot = Inv[abs];
        if (UseRefusal(ItemUse.ClanBank, slot) is not ItemUseRules.Allowed and var bankRefusal)
        {
            _clanWhStatus.Status(ItemData.Text(bankRefusal, ClanWhNotStorableText), bad: true);
            return;
        }
        if (amount == null && AsksStackAmount(slot))
            _clanWhAmount.Open(ItemData.Icon(slot.ItemId), $"Store {ItemData.DisplayName(slot.ItemId)}",
                $"You carry {slot.Count:n0}", slot.Count, slot.Count, n => ClanDepositCount(abs, (int)n, target), "Store");
        else
            ClanDepositCount(abs, amount ?? Mathf.Max(1, (int)slot.Count), target);
    }

    private void ClanDepositCount(int abs, int count, int target)
    {
        if (StorageTransferBusy || !InMainBag(abs) || Inv[abs].IsEmpty || count <= 0) return;
        var slot = Inv[abs];
        count = Mathf.Min(count, slot.Count > 0 ? slot.Count : 1);
        int whIdx;
        bool merge;
        if (target >= 0)
        {
            if (!FitsInClanWh(target, slot.ItemId, count)) { _clanWhStatus.Status("That slot is taken.", bad: true); return; }
            whIdx = target;
            merge = !_clanWh[target].IsEmpty;
        }
        else
        {
            whIdx = ClanWhDestination(slot.ItemId, count, out merge);
            if (whIdx < 0) { _clanWhStatus.Status("The clan bank is full.", bad: true); return; }
        }
        _clanWhPending = new ClanWhPending { Op = Net.ClanWhInputSub, Merge = merge, InvAbs = abs, WhIdx = whIdx, Count = count };
        _clanWhInFlight = true;
        Net.I.SendClanWhInput(slot.ItemId, (byte)(whIdx / ClanWhPageSize),
            (byte)(abs - GridStart), (byte)(whIdx % ClanWhPageSize), count);
    }

    private void AskClanWithdraw(int whIdx, int target, int? amount = null)
    {
        if (StorageTransferBusy || whIdx < 0 || whIdx >= ClanWhSlots || _clanWh[whIdx].IsEmpty) return;
        if (!ClanCanWithdraw) { _clanWhStatus.Status(ClanWhNoRightsText, bad: true); return; }
        var slot = _clanWh[whIdx];
        if (amount == null && AsksStackAmount(slot))
            _clanWhAmount.Open(ItemData.Icon(slot.ItemId), $"Take out {ItemData.DisplayName(slot.ItemId)}",
                $"Stored {slot.Count:n0}", slot.Count, slot.Count, n => ClanWithdrawCount(whIdx, (int)n, target), "Take out");
        else
            ClanWithdrawCount(whIdx, amount ?? Mathf.Max(1, (int)slot.Count), target);
    }

    private void ClanWithdrawCount(int whIdx, int count, int target)
    {
        if (StorageTransferBusy || whIdx < 0 || whIdx >= ClanWhSlots || _clanWh[whIdx].IsEmpty || count <= 0) return;
        var slot = _clanWh[whIdx];
        count = Mathf.Min(count, slot.Count > 0 ? slot.Count : 1);
        int dest;
        bool merge;
        if (target >= 0)
        {
            if (!FitsInBag(target, slot.ItemId, count)) { _clanWhStatus.Status("That slot is taken.", bad: true); return; }
            dest = target;
            merge = !Inv[target].IsEmpty;
        }
        else
        {
            dest = BagDestination(slot.ItemId, count, out merge);
            if (dest < 0) { _clanWhStatus.Status("Your bags are full.", bad: true); return; }
        }
        _clanWhPending = new ClanWhPending { Op = Net.ClanWhOutputSub, Merge = merge, InvAbs = dest, WhIdx = whIdx, Count = count };
        _clanWhInFlight = true;
        Net.I.SendClanWhOutput(slot.ItemId, (byte)(whIdx / ClanWhPageSize),
            (byte)(whIdx % ClanWhPageSize), (byte)(dest - GridStart), count);
    }

    private void MoveInsideClanWh(int from, int to, int? amount)
    {
        if (StorageTransferBusy || from == to || from < 0 || to < 0 || from >= ClanWhSlots || to >= ClanWhSlots || _clanWh[from].IsEmpty) return;
        if (!ClanCanWithdraw) { _clanWhStatus.Status(ClanWhNoRightsText, bad: true); return; }
        if (IsPartOfStack(amount, _clanWh[from])) { _clanWhStatus.Status(StorageWholeStackText, bad: true); return; }
        if (!_clanWh[to].IsEmpty) { _clanWhStatus.Status("That slot is taken.", bad: true); return; }
        _clanWhPending = new ClanWhPending { Op = Net.ClanWhMoveSub, WhIdx = from, WhTo = to, Count = _clanWh[from].Count };
        _clanWhInFlight = true;
        Net.I.SendClanWhMoveInside(_clanWh[from].ItemId, (byte)(from / ClanWhPageSize), (byte)(from % ClanWhPageSize),
            (byte)(to / ClanWhPageSize), (byte)(to % ClanWhPageSize));
    }

    private long ClanGoldRoom(bool deposit) => CharacterSheet.CoinMax - (long)(deposit ? _clanWhMoney : Sheet.Gold);

    private void AskClanGoldTransfer(bool deposit)
    {
        if (_clanWhInFlight) return;
        if (!deposit && !ClanCanWithdraw) { _clanWhStatus.Status(ClanWhNoRightsText, bad: true); return; }
        long have = deposit ? Sheet.Gold : _clanWhMoney;
        if (have <= 0) return;
        long max = Math.Min(have, ClanGoldRoom(deposit));
        if (max <= 0)
        {
            _clanWhStatus.Status(deposit ? "The clan bank can't hold more gold." : "You can't carry more gold.", bad: true);
            return;
        }
        string note = ItemData.Text(GoldCapNoteText, "You cannot enter if it's more than 2.1 billion.");
        _clanWhAmount.Open(null, deposit ? "Deposit gold" : "Withdraw gold",
            deposit ? $"You carry {have:n0}" : $"Stored {have:n0}", max, max, n => ClanGoldTransfer(deposit, (int)n),
            deposit ? "Deposit" : "Withdraw", _ => note);
    }

    private void ClanGoldTransfer(bool deposit, int amount)
    {
        if (StorageTransferBusy || amount <= 0) return;
        if (deposit && amount > Sheet.Gold) { _clanWhStatus.Status("Not enough carried gold.", bad: true); return; }
        if (!deposit && amount > _clanWhMoney) { _clanWhStatus.Status("Not enough clan gold.", bad: true); return; }
        _clanWhPending = new ClanWhPending { Op = deposit ? Net.ClanWhInputSub : Net.ClanWhOutputSub, Gold = true, Count = amount };
        _clanWhInFlight = true;
        if (deposit) Net.I.SendClanWhInput(Net.GoldItemId, 0, 0, 0, amount);
        else Net.I.SendClanWhOutput(Net.GoldItemId, 0, 0, 0, amount);
    }

    private static string ClanWhRefusalText(byte result) => result switch
    {
        Net.ClanWhNotInClan => ClanWhNotInClanText,
        Net.ClanWhNoRights => ClanWhNoRightsText,
        Net.ClanWhNotStorable => ClanWhNotStorableText,
        _ => "Transfer failed.",
    };

    private void OnClanWhResult(byte op, byte result)
    {
        if (op == Net.ClanWhOpenSub)
        {
            string refusal = ClanWhRefusalText(result);
            CombatNotice(refusal);
            if (result == Net.ClanWhNotInClan) CloseClanWarehouse();
            else if (_clanWhShown) _clanWhStatus.Status(refusal, bad: true);
            return;
        }

        if (!_clanWhInFlight) return;
        _clanWhInFlight = false;
        if (result != Net.ClanWhSucceeded)
        {
            _clanWhStatus.Status(result == Net.ClanWhFailed && op == Net.ClanWhOutputSub
                ? ItemData.Text(WhNoRoomText, "Your bags are full.") : ClanWhRefusalText(result), bad: true);
            if (_clanWhShown) RefreshClanWarehouse();
            return;
        }

        var p = _clanWhPending;
        if (p.Gold)
        {
            if (p.Op == Net.ClanWhInputSub) { Sheet.Spend(p.Count); _clanWhMoney += p.Count; }
            else { _clanWhMoney -= p.Count; Sheet.Receive(p.Count); }
            Net.I.RaiseGold(Sheet.Gold);
            _clanWhStatus.Status(p.Op == Net.ClanWhInputSub ? $"Deposited {p.Count:n0} gold." : $"Withdrew {p.Count:n0} gold.", bad: false);
        }
        else if (p.Op == Net.ClanWhMoveSub)
        {
            int itemId = _clanWh[p.WhIdx].ItemId;
            _clanWh[p.WhTo] = _clanWh[p.WhIdx];
            _clanWh[p.WhIdx] = default;
            _clanWhStatus.Status($"Moved {ItemData.DisplayName(itemId)}.", bad: false);
        }
        else if (p.Op == Net.ClanWhInputSub)
        {
            var bag = Inv[p.InvAbs];
            int itemId = bag.ItemId;
            MoveStack(ref bag, ref _clanWh[p.WhIdx], p.Count, p.Merge);
            Inv[p.InvAbs] = bag;
            Net.I.MirrorInventorySlot(p.InvAbs, Inv[p.InvAbs]);
            if (CharTabOpen()) RefreshInventoryUI();
            _clanWhStatus.Status($"Stored {WhItemLine(itemId, p.Count)}.", bad: false);
        }
        else
        {
            var bag = Inv[p.InvAbs];
            int itemId = _clanWh[p.WhIdx].ItemId;
            MoveStack(ref _clanWh[p.WhIdx], ref bag, p.Count, p.Merge);
            Inv[p.InvAbs] = bag;
            Net.I.MirrorInventorySlot(p.InvAbs, Inv[p.InvAbs]);
            if (CharTabOpen()) RefreshInventoryUI();
            _clanWhStatus.Status($"Took out {WhItemLine(itemId, p.Count)}.", bad: false);
        }
        if (_clanWhShown) RefreshClanWarehouse();
    }
}
