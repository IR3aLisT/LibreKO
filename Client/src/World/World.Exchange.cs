using System.Collections.Generic;
using System.Linq;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private CanvasLayer _exLayer = null!;
    private HudWindow _exPanel = null!;
    private const int TradeOfferSlots = 12;
    private const int TradeGridColumns = 4;
    private const float TradeCellSize = 42f;
    private const int TextTradeOwnGenie = 31082;
    private const int TextTradePartnerGenie = 31083;
    private const int TextTradeBusy = 3504;
    private const int TextTradeDeclined = 6311;
    private const int TextTradeDuplicate = 10761;
    private static readonly Color TradeBusyColour = new("9b9bff");
    private static readonly Color TradeConfirmedColour = new("7ddc6f");

    private readonly ItemSlotView[] _exMineCells = new ItemSlotView[TradeOfferSlots];
    private readonly ItemSlotView[] _exTheirsCells = new ItemSlotView[TradeOfferSlots];
    private Label _exMineGold = null!, _exTheirsGold = null!, _exStatus = null!;
    private Label _exMineHeader = null!, _exTheirsHeader = null!, _exMineState = null!, _exTheirsState = null!;
    private Label _exPurse = null!, _exRoom = null!;
    private BagCompanion? _exCompanion;
    private readonly HashSet<int> _genieUsers = new();
    private Button _exConfirmBtn = null!, _exCancelBtn = null!;
    private LineEdit _exGoldEdit = null!;
    private ConfirmationDialog _exAskDialog = null!;

    private bool _exShown;
    private bool _exRequestPending;
    private bool _exConfirmedByMe, _exConfirmedByPartner;
    private int _exPartnerId = -1;
    private string _exPartnerName = "Player";
    private int _exMyGoldOffer, _exTheirGoldOffer;

    private bool _exAddInFlight;
    private PendingExAdd _exPending;
    private readonly List<ExOfferItem> _exMyOffer = new();
    private readonly List<ExOfferItem> _exTheirOffer = new();

    private CanvasLayer _exWaitLayer = null!;
    private Label _exWaitLabel = null!;
    private bool _exWaiting;

    private CanvasLayer _exAmountLayer = null!;
    private TextureRect _exAmountIcon = null!;
    private Label _exAmountName = null!, _exAmountHint = null!;
    private SpinBox _exAmountSpin = null!;
    private bool _exAmountShown;
    private int _exAmountSlot = -1;
    private int _exAmountItem;
    private int _exAmountMax = 1;

    private struct PendingExAdd { public bool IsGold; public int ItemId; public int SourceAbs; public int Count; public short Dura; public int UniqueId; }
    private struct ExOfferItem { public int ItemId; public int Count; public short Dura; public int SourceAbs; public int UniqueId; }

    private const float TradeRange = 8f;
    private const int ExchangeUntradableText = 7701;

    private bool TradeHoldsInput => _exShown || _exWaiting || _exRequestPending;

    private void ExchangeInit()
    {
        BuildExchangePanel();
        Net.I.TradeBlockedEvent += OnTradeBlocked;
        Net.I.ExchangeRequestEvent += OnExchangeRequest;
        Net.I.ExchangeAgreeEvent += OnExchangeAgree;
        Net.I.ExchangeAddResultEvent += OnExchangeAddResult;
        Net.I.ExchangeOtherAddEvent += OnExchangeOtherAdd;
        Net.I.ExchangeOtherDecideEvent += OnExchangeOtherDecide;
        Net.I.ExchangeDoneEvent += OnExchangeDone;
        Net.I.ExchangeCancelEvent += OnExchangeCancel;
        Net.I.ExchangeDuplicateEvent += OnExchangeDuplicate;
    }

    private void OnTradeBlocked(string text) => Notice.Show(this, text);

    private void ExchangeDispose()
    {
        Net.I.TradeBlockedEvent -= OnTradeBlocked;
        Net.I.ExchangeRequestEvent -= OnExchangeRequest;
        Net.I.ExchangeAgreeEvent -= OnExchangeAgree;
        Net.I.ExchangeAddResultEvent -= OnExchangeAddResult;
        Net.I.ExchangeOtherAddEvent -= OnExchangeOtherAdd;
        Net.I.ExchangeOtherDecideEvent -= OnExchangeOtherDecide;
        Net.I.ExchangeDoneEvent -= OnExchangeDone;
        Net.I.ExchangeCancelEvent -= OnExchangeCancel;
        Net.I.ExchangeDuplicateEvent -= OnExchangeDuplicate;
    }

    private void BuildExchangePanel()
    {
        _exLayer = new CanvasLayer { Layer = 75 };
        AddChild(_exLayer);

        _exPanel = new HudWindow("exchange", "Trade") { Visible = false };
        _exPanel.Closed += () => { if (_exShown) AbortExchange(local: true); };
        _exLayer.AddChild(_exPanel);

        var root = _exPanel.Body;
        root.AddThemeConstantOverride("separation", 8);

        var cols = new HBoxContainer();
        cols.AddThemeConstantOverride("separation", 16);
        root.AddChild(cols);
        cols.AddChild(BuildOfferGrid(_exMineCells, mine: true, out _exMineHeader, out _exMineState, out _exMineGold));
        cols.AddChild(BuildOfferGrid(_exTheirsCells, mine: false, out _exTheirsHeader, out _exTheirsState, out _exTheirsGold));

        root.AddChild(new HSeparator());

        _exPurse = UiTheme.Text("", 12, UiTheme.Gold);
        root.AddChild(_exPurse);
        var goldRow = new HBoxContainer();
        goldRow.AddThemeConstantOverride("separation", 6);
        var goldLbl = HudStyle.Label(13); goldLbl.Text = "Gold:";
        goldRow.AddChild(goldLbl);
        _exGoldEdit = new LineEdit { PlaceholderText = "amount", CustomMinimumSize = new Vector2(110, 0) };
        _exGoldEdit.TextSubmitted += _ => OnAddGold();
        goldRow.AddChild(_exGoldEdit);
        var allGoldBtn = new Button { Text = "All", FocusMode = Control.FocusModeEnum.None };
        allGoldBtn.Pressed += () => _exGoldEdit.Text = Sheet.Gold.ToString();
        goldRow.AddChild(allGoldBtn);
        var addGoldBtn = new Button { Text = "Add gold", FocusMode = Control.FocusModeEnum.None };
        addGoldBtn.Pressed += OnAddGold;
        goldRow.AddChild(addGoldBtn);
        root.AddChild(goldRow);

        _exRoom = UiTheme.Text("", 12, UiTheme.TextLo);
        root.AddChild(_exRoom);

        root.AddChild(new HSeparator());
        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 8);
        _exConfirmBtn = new Button { Text = "Confirm", FocusMode = Control.FocusModeEnum.None };
        _exConfirmBtn.Pressed += OnExchangeConfirm;
        footer.AddChild(_exConfirmBtn);
        _exCancelBtn = new Button { Text = "Cancel", FocusMode = Control.FocusModeEnum.None };
        _exCancelBtn.Pressed += () => AbortExchange(local: true);
        footer.AddChild(_exCancelBtn);
        _exStatus = HudStyle.Label(13, HorizontalAlignment.Right);
        _exStatus.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        footer.AddChild(_exStatus);
        root.AddChild(footer);

        _exAskDialog = new ConfirmationDialog { Title = "Trade request" };
        _exAskDialog.GetOkButton().Text = "Accept";
        _exAskDialog.GetCancelButton().Text = "Decline";
        _exAskDialog.Confirmed += () => AnswerExchangeRequest(true);
        _exAskDialog.Canceled += () => AnswerExchangeRequest(false);
        _exLayer.AddChild(_exAskDialog);

        BuildExchangeWaitPanel();
        BuildExchangeAmountPrompt();
    }

    private void BuildExchangeWaitPanel()
    {
        _exWaitLayer = new CanvasLayer { Layer = 76, Visible = false };
        AddChild(_exWaitLayer);

        var centre = new CenterContainer();
        centre.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        centre.MouseFilter = Control.MouseFilterEnum.Ignore;
        _exWaitLayer.AddChild(centre);

        var box = new PanelContainer();
        box.AddThemeStyleboxOverride("panel", UiTheme.WindowPanel());
        centre.AddChild(box);

        var margin = new MarginContainer();
        UiTheme.Margins(margin, 16, 13, 16, 13);
        box.AddChild(margin);

        var root = new VBoxContainer { CustomMinimumSize = new Vector2(280, 0) };
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);

        _exWaitLabel = UiTheme.Text("", 13, UiTheme.TextHi, HorizontalAlignment.Center);
        _exWaitLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _exWaitLabel.CustomMinimumSize = new Vector2(280, 0);
        root.AddChild(_exWaitLabel);

        var buttons = new HBoxContainer();
        buttons.Alignment = BoxContainer.AlignmentMode.Center;
        root.AddChild(buttons);
        var cancel = new Button { Text = "Cancel", FocusMode = Control.FocusModeEnum.None };
        cancel.Pressed += CancelExchangeRequest;
        buttons.AddChild(cancel);
    }

    private void BuildExchangeAmountPrompt()
    {
        _exAmountLayer = new CanvasLayer { Layer = 76, Visible = false };
        AddChild(_exAmountLayer);

        var dim = new ColorRect { Color = new Color(0f, 0f, 0f, 0.45f) };
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        dim.MouseFilter = Control.MouseFilterEnum.Stop;
        _exAmountLayer.AddChild(dim);

        var centre = new CenterContainer();
        centre.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        centre.MouseFilter = Control.MouseFilterEnum.Ignore;
        _exAmountLayer.AddChild(centre);

        var box = new PanelContainer();
        box.AddThemeStyleboxOverride("panel", UiTheme.WindowPanel());
        centre.AddChild(box);

        var margin = new MarginContainer();
        UiTheme.Margins(margin, 14, 12, 14, 12);
        box.AddChild(margin);

        var root = new VBoxContainer { CustomMinimumSize = new Vector2(300, 0) };
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);

        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 10);
        root.AddChild(head);
        _exAmountIcon = new TextureRect
        {
            CustomMinimumSize = new Vector2(38, 38),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        head.AddChild(_exAmountIcon);
        var headText = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        headText.AddThemeConstantOverride("separation", -2);
        head.AddChild(headText);
        _exAmountName = UiTheme.Text("", 14, UiTheme.TextHi);
        headText.AddChild(_exAmountName);
        _exAmountHint = UiTheme.Text("", 11, UiTheme.TextLo);
        headText.AddChild(_exAmountHint);

        var amountRow = new HBoxContainer();
        amountRow.AddThemeConstantOverride("separation", 8);
        root.AddChild(amountRow);
        var amountLbl = UiTheme.Text("Quantity", 13, UiTheme.TextLo);
        amountLbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        amountRow.AddChild(amountLbl);
        _exAmountSpin = new SpinBox
        {
            MinValue = 1,
            MaxValue = Inventory.StackMax,
            Step = 1,
            Value = 1,
            CustomMinimumSize = new Vector2(110, 0),
        };
        _exAmountSpin.GetLineEdit().AddThemeFontSizeOverride("font_size", 13);
        amountRow.AddChild(_exAmountSpin);

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 8);
        buttons.Alignment = BoxContainer.AlignmentMode.End;
        root.AddChild(buttons);
        var cancel = new Button { Text = "Cancel", FocusMode = Control.FocusModeEnum.None };
        cancel.Pressed += CloseExchangeAmount;
        buttons.AddChild(cancel);
        var ok = new Button { Text = "Offer", FocusMode = Control.FocusModeEnum.None };
        ok.Pressed += ConfirmExchangeAmount;
        buttons.AddChild(ok);
    }

    private Control BuildOfferGrid(ItemSlotView[] cells, bool mine, out Label header, out Label state, out Label count)
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 4);
        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 8);
        header = UiTheme.Text("", 13, UiTheme.GoldBright);
        header.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        head.AddChild(header);
        state = UiTheme.Text("", 11, UiTheme.TextLo, HorizontalAlignment.Right);
        head.AddChild(state);
        box.AddChild(head);
        var grid = new GridContainer { Columns = TradeGridColumns };
        grid.AddThemeConstantOverride("h_separation", 3);
        grid.AddThemeConstantOverride("v_separation", 3);
        for (int i = 0; i < cells.Length; i++)
        {
            var cell = new ItemSlotView(TradeCellSize) { Index = i, Veiled = true };
            cell.Hovered += view => ShowItemTooltip(-1, view.Item);
            cell.Unhovered += _ => HideItemTooltip();
            cell.RightClicked += view => ShowSealedCharacterSheet(view.Item);
            if (mine)
            {
                cell.CanDrop = (_, data) => CanDropOnOffer(data);
                cell.Dropped = (_, data) => DropOnOffer(data);
            }
            cells[i] = cell;
            grid.AddChild(cell);
        }
        box.AddChild(grid);
        count = UiTheme.Text("", 12, UiTheme.Gold);
        box.AddChild(count);
        return box;
    }

    private void TryTradeNearest()
    {
        if (_exShown) { SetExStatus("Already trading.", true); return; }
        if (_selfDead) return;
        if (TryBrowseNearestMerchant()) return;
        int bestId = -1; float bestD = TradeRange;
        foreach (var kv in _ents)
        {
            var e = kv.Value;
            if (e.IsNpc || e.Dead || kv.Key == _myId) continue;
            float d = e.Body.Position.DistanceTo(_self.Position);
            if (d < bestD) { bestD = d; bestId = kv.Key; }
        }
        if (bestId < 0) { CombatNotice("No player nearby to trade with."); return; }
        BeginTradeRequest(bestId, _ents.TryGetValue(bestId, out var pe) ? pe.Name : "Player");
    }

    private void BeginTradeRequest(int charId, string name)
    {
        if (Net.I.GenieRunning)
        {
            TradeRefusalLine(TextTradeOwnGenie, "You cannot trade while using Genie.");
            return;
        }
        if (_genieUsers.Contains(charId))
        {
            TradeRefusalLine(TextTradePartnerGenie, "You cannot trade with users who are using Genie.");
            return;
        }
        if (_ents.TryGetValue(charId, out var partner)
            && partner.Nation != Net.I.Nation
            && !Net.I.CurrentZoneAbility.CanTrade)
        {
            CombatNotice("The two nations cannot trade here.");
            return;
        }

        _exPartnerId = charId;
        _exPartnerName = name;
        Net.I.SendExchangeRequest(charId);
        ShowExchangeWait($"Waiting for {name} to accept the trade…");
    }

    private void ShowExchangeWait(string text)
    {
        _exWaitLabel.Text = text;
        _exWaitLayer.Visible = true;
        _exWaiting = true;
    }

    private void HideExchangeWait()
    {
        if (!_exWaiting) return;
        _exWaiting = false;
        _exWaitLayer.Visible = false;
    }

    private void CancelExchangeRequest()
    {
        if (!_exWaiting) return;
        HideExchangeWait();
        Net.I.SendExchangeCancel();
        ResetExchangeState();
        _exPartnerId = -1;
        CombatNotice("Trade request cancelled.");
    }

    private void OnExchangeRequest(int requesterCharId)
    {
        if (_exRequestPending) return;
        if (Net.I.GenieRunning || _genieUsers.Contains(requesterCharId))
        {
            if (Net.I.GenieRunning) TradeRefusalLine(TextTradeOwnGenie, "You cannot trade while using Genie.");
            else TradeRefusalLine(TextTradePartnerGenie, "You cannot trade with users who are using Genie.");
            Net.I.SendExchangeAgree(false);
            return;
        }
        if (_tradeRequestsBlocked)
        {
            CommandRefusal(CommandCatalog.TradeRequestsRefusedText, "Has blocked trade requests.");
            Net.I.SendExchangeAgree(false);
            return;
        }
        if (_exShown || _exWaiting)
        {
            CombatLogAdd(ItemData.Text(TextTradeBusy, "A trade request has been received from another user while you were trading."),
                CombatLogKind.Status, TradeBusyColour);
            Net.I.SendExchangeAgree(false);
            return;
        }
        CloseVendor();
        CloseWarehouse();
        _exPartnerId = requesterCharId;
        _exPartnerName = _ents.TryGetValue(requesterCharId, out var e) ? e.Name : "Player";
        _exRequestPending = true;
        _exAskDialog.DialogText = $"{_exPartnerName} wants to trade.\nAccept?";
        _exAskDialog.PopupCentered();
        CombatNotice($"{_exPartnerName} wants to trade. (Press T to trade)");
    }

    private void AnswerExchangeRequest(bool accept)
    {
        if (!_exRequestPending) return;
        _exRequestPending = false;
        Net.I.SendExchangeAgree(accept);
        if (accept) OpenExchange();
    }

    private void OnExchangeAgree(bool accepted)
    {
        HideExchangeWait();
        if (accepted) OpenExchange();
        else
        {
            TradeRefusalLine(TextTradeDeclined, "The user has declined the request for a trade.");
            ResetExchangeState();
        }
    }

    private void OpenExchange()
    {
        ResetExchangeState();
        _exPanel.Title = $"Trade — {_exPartnerName}";
        _exConfirmBtn.Disabled = false;
        _exConfirmBtn.Text = "Confirm";
        SetExStatus("", false);
        _exMineHeader.Text = "You offer";
        _exTheirsHeader.Text = $"{_exPartnerName} offers";
        _exPanel.Visible = true;
        _exShown = true;
        _exCompanion ??= new BagCompanion(TradeTakeFromBag, TradeBagFit, _ => "", () => AbortExchange(local: true));
        AttachBagCompanion(_exCompanion);
        RefreshExchangeOffers();
    }

    private bool TradeTakeFromBag(int abs)
    {
        if (!InMainBag(abs)) return false;
        OfferSlot(abs);
        return true;
    }

    private bool CanDropOnOffer(Variant data) =>
        _exShown
        && data.VariantType == Variant.Type.Dictionary
        && data.AsGodotDictionary().ContainsKey("invFrom")
        && InMainBag(data.AsGodotDictionary()["invFrom"].AsInt32());

    private void DropOnOffer(Variant data)
    {
        var d = data.AsGodotDictionary();
        OfferSlot(d["invFrom"].AsInt32(), DragLayer.ChosenAmount(d));
    }

    private BagFit TradeBagFit(int abs) =>
        UseRefusal(ItemUse.Trade, Inv[abs]) == ItemUseRules.Allowed ? BagFit.Normal : BagFit.Unfit;

    private void TradeRefusalLine(int textId, string fallback) =>
        CombatLogAdd(ItemData.Text(textId, fallback), CombatLogKind.Status, WhRefusalTint);

    private void OnExchangeDuplicate()
    {
        string text = ItemData.Text(TextTradeDuplicate, "This item is a duplicated item.");
        CombatLogAdd(text, CombatLogKind.Status, WhRefusalTint);
        if (_exShown) SetExStatus(text, true);
    }

    private void AbortExchange(bool local)
    {
        if (!_exShown) return;
        if (local) Net.I.SendExchangeCancel();
        RestoreMyOffer();
        CloseExchangeWindow();
    }

    private void CloseExchangeWindow()
    {
        HideItemTooltip();
        _exShown = false;
        _exPanel.Visible = false;
        ResetExchangeState();
        PluginNotifyTrade();
        if (_exCompanion != null) DetachBagCompanion(_exCompanion);
    }

    private void ResetExchangeState()
    {
        _exMyOffer.Clear();
        _exTheirOffer.Clear();
        _exMyGoldOffer = 0;
        _exTheirGoldOffer = 0;
        _exConfirmedByMe = false;
        _exConfirmedByPartner = false;
        _exAddInFlight = false;
        CloseExchangeAmount();
        if (_exGoldEdit != null) _exGoldEdit.Text = "";
    }

    private void RefreshExchangeOffers()
    {
        FillOfferGrid(_exMineCells, _exMyOffer);
        FillOfferGrid(_exTheirsCells, _exTheirOffer);
        _exMineGold.Text = OfferSummary(_exMyOffer.Count, _exMyGoldOffer);
        _exTheirsGold.Text = OfferSummary(_exTheirOffer.Count, _exTheirGoldOffer);
        SetOfferState(_exMineState, _exConfirmedByMe);
        SetOfferState(_exTheirsState, _exConfirmedByPartner);
        _exPurse.Text = $"You have {Sheet.Gold:n0} gold.";
        RefreshTradeRoom();
        RefreshBagFit();
        PluginNotifyTrade();
    }

    private static string OfferSummary(int items, int gold) =>
        gold > 0 ? $"{items} / {TradeOfferSlots} items   + {gold:n0} gold" : $"{items} / {TradeOfferSlots} items";

    private static void SetOfferState(Label state, bool confirmed)
    {
        state.Text = confirmed ? "Confirmed" : "Not confirmed";
        state.AddThemeColorOverride("font_color", confirmed ? TradeConfirmedColour : UiTheme.TextLo);
    }

    private static void FillOfferGrid(ItemSlotView[] cells, List<ExOfferItem> offer)
    {
        for (int i = 0; i < cells.Length; i++)
            cells[i].Set(i < offer.Count
                ? new ItemSlot { ItemId = offer[i].ItemId, Count = (short)offer[i].Count, Durability = offer[i].Dura, UniqueId = offer[i].UniqueId }
                : default);
    }

    private void RefreshTradeRoom()
    {
        int free = 0;
        for (int abs = GridStart; abs < GridStart + GridCount && abs < Inv.Length; abs++)
            if (Inv[abs].IsEmpty) free++;
        long weight = CarriedWeight() + _exTheirOffer.Sum(o => OfferWeight(o)) - _exMyOffer.Sum(o => OfferWeight(o));
        var room = new TradeRoom(free, _exTheirOffer.Count, weight, Sheet.MaxWeight);
        _exRoom.Text = Sheet.MaxWeight > 0
            ? $"Free slots: {free} (incoming {room.Incoming})   Weight after trade: {weight / 10f:0.0} / {Sheet.MaxWeight / 10f:0.0}"
            : $"Free slots: {free} (incoming {room.Incoming})";
        _exRoom.AddThemeColorOverride("font_color", room.Fits ? UiTheme.TextLo : UiTheme.Bad);
    }

    private static long OfferWeight(ExOfferItem offer) =>
        ItemData.Get(offer.ItemId) is { } def
            ? (long)def.Weight * ItemData.CarriedUnits(def, new ItemSlot { ItemId = offer.ItemId, Count = (short)offer.Count, Durability = offer.Dura })
            : 0;

    private void OfferSlot(int absSlot, int? amount = null)
    {
        if (_exConfirmedByMe) { SetExStatus("You already confirmed.", true); return; }
        if (_exAddInFlight || _exConfirmedByPartner) return;
        if (absSlot < 0 || absSlot >= Inv.Length || Inv[absSlot].IsEmpty) return;
        var slot = Inv[absSlot];
        if (UseRefusal(ItemUse.Trade, slot) is not ItemUseRules.Allowed and var tradeRefusal)
        {
            SetExStatus(ItemData.Text(tradeRefusal, "You cannot trade this item."), true);
            return;
        }
        if (!CanOfferItem(slot)) return;
        int have = Mathf.Max(1, (int)slot.Count);

        if (amount is { } chosen)
        {
            OfferSlotAmount(absSlot, slot.ItemId, chosen);
            return;
        }

        if (have > 1 && (ItemData.Get(slot.ItemId)?.Countable ?? 0) != 0)
        {
            OpenExchangeAmount(absSlot, have);
            return;
        }

        OfferSlotAmount(absSlot, slot.ItemId, have);
    }

    private bool CanOfferItem(ItemSlot slot)
    {
        if (!ExchangeOffer.IsOfferable(slot, ItemData.Get(slot.ItemId)))
        {
            SetExStatus(ItemData.Text(ExchangeUntradableText, "That item can't be traded."), true);
            return false;
        }
        if (!ExchangeOffer.HasRoomFor(_exMyOffer.ConvertAll(o => o.ItemId), slot.ItemId, IsCountableItem))
        {
            SetExStatus($"Offer is full ({ExchangeOffer.ItemSlots} items).", true);
            return false;
        }
        return true;
    }

    private static bool IsCountableItem(int itemId) => (ItemData.Get(itemId)?.Countable ?? 0) != 0;

    private void OfferSlotAmount(int absSlot, int itemId, int count)
    {
        if (!_exShown || _exConfirmedByMe || _exConfirmedByPartner || _exAddInFlight) return;
        if (absSlot < 0 || absSlot >= Inv.Length || Inv[absSlot].IsEmpty) return;
        var slot = Inv[absSlot];
        if (slot.ItemId != itemId || count < 1 || count > Mathf.Max(1, (int)slot.Count))
        {
            SetExStatus("The item or quantity changed. Please try again.", true);
            return;
        }
        if (!CanOfferItem(slot)) return;

        _exPending = new PendingExAdd { IsGold = false, ItemId = slot.ItemId, SourceAbs = absSlot, Count = count, Dura = slot.Durability, UniqueId = slot.UniqueId };
        _exAddInFlight = true;
        Net.I.SendExchangeAddItem((byte)(absSlot - GridStart), slot.ItemId, count);
    }

    private void OpenExchangeAmount(int absSlot, int max)
    {
        HideItemTooltip();
        _exAmountSlot = absSlot;
        _exAmountMax = max;
        int itemId = Inv[absSlot].ItemId;
        _exAmountItem = itemId;
        _exAmountIcon.Texture = ItemData.Icon(itemId);
        _exAmountName.Text = ItemData.DisplayName(itemId);
        _exAmountHint.Text = $"You have {max:n0}";
        _exAmountSpin.MaxValue = max;
        _exAmountSpin.Value = max;
        _exAmountSpin.GetLineEdit().Text = max.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _exAmountLayer.Visible = true;
        _exAmountShown = true;
        _exAmountSpin.GetLineEdit().GrabFocus();
        _exAmountSpin.GetLineEdit().SelectAll();
    }

    private void CloseExchangeAmount()
    {
        if (!_exAmountShown) return;
        _exAmountShown = false;
        _exAmountLayer.Visible = false;
        _exAmountSlot = -1;
    }

    private void ConfirmExchangeAmount()
    {
        if (!_exAmountShown || _exAmountSlot < 0) return;
        if (!ExchangeOffer.TryParseAmount(_exAmountSpin.GetLineEdit().Text, _exAmountMax, out int count))
        {
            _exAmountHint.Text = $"Valid quantity: 1–{_exAmountMax:n0}.";
            _exAmountSpin.GetLineEdit().GrabFocus();
            _exAmountSpin.GetLineEdit().SelectAll();
            return;
        }
        int slotAbs = _exAmountSlot, itemId = _exAmountItem;
        CloseExchangeAmount();
        OfferSlotAmount(slotAbs, itemId, count);
    }

    private void OnAddGold()
    {
        if (_exConfirmedByMe) { SetExStatus("You already confirmed.", true); return; }
        if (_exAddInFlight || _exConfirmedByPartner) return;
        if (!int.TryParse(_exGoldEdit.Text.Trim(), out int amount) || amount <= 0) { SetExStatus("Enter a gold amount.", true); return; }
        AddTradeGold(amount);
    }

    private void AddTradeGold(int amount)
    {
        if (_exConfirmedByMe) { SetExStatus("You already confirmed.", true); return; }
        if (_exAddInFlight || amount <= 0) return;
        if (amount > Sheet.Gold) { SetExStatus("Not enough gold.", true); return; }

        _exPending = new PendingExAdd { IsGold = true, Count = amount };
        _exAddInFlight = true;
        Net.I.SendExchangeAddGold(amount);
    }

    private void OnExchangeAddResult(bool committed)
    {
        if (!_exAddInFlight) return;
        _exAddInFlight = false;
        if (!committed) { SetExStatus("That item can't be traded.", true); return; }

        if (_exPending.IsGold)
        {
            _exMyGoldOffer += _exPending.Count;
            Sheet.Spend(_exPending.Count);
            Net.I.RaiseGold(Sheet.Gold);
            _exGoldEdit.Text = "";
        }
        else
        {
            _exMyOffer.Add(new ExOfferItem { ItemId = _exPending.ItemId, Count = _exPending.Count, Dura = _exPending.Dura, SourceAbs = _exPending.SourceAbs, UniqueId = _exPending.UniqueId });
            int abs = _exPending.SourceAbs;
            if (abs >= 0 && abs < Inv.Length)
            {
                int left = Inv[abs].Count - _exPending.Count;
                if (left > 0)
                {
                    var kept = Inv[abs];
                    kept.Count = (short)left;
                    Inv[abs] = kept;
                }
                else Inv[abs] = default;
                Net.I.MirrorInventorySlot(abs, Inv[abs]);
            }
            if (CharTabOpen()) RefreshInventoryUI();
        }
        RefreshExchangeOffers();
        SetExStatus("", false);
    }

    private void OnExchangeOtherAdd(int itemId, int count, short dura, int uniqueId)
    {
        if (!_exShown) return;
        if (itemId == Net.ExchangeGoldItem) _exTheirGoldOffer += count;
        else _exTheirOffer.Add(new ExOfferItem { ItemId = itemId, Count = count, Dura = dura, UniqueId = uniqueId });
        RefreshExchangeOffers();
    }

    private void OnExchangeConfirm()
    {
        if (!_exShown || _exConfirmedByMe) return;
        if (_exAddInFlight || _exAmountShown) { SetExStatus("Finish adding your offer before confirming.", true); return; }
        _exConfirmedByMe = true;
        _exConfirmBtn.Disabled = true;
        _exConfirmBtn.Text = "Confirmed";
        SetExStatus("Waiting for partner…", false);
        RefreshExchangeOffers();
        Net.I.SendExchangeDecide();
    }

    private void OnExchangeOtherDecide()
    {
        if (!_exShown) return;
        _exConfirmedByPartner = true;
        CloseExchangeAmount();
        RefreshExchangeOffers();
        SetExStatus(_exConfirmedByMe ? "Finalising…" : "Partner confirmed — press Confirm.", false);
    }

    private void OnExchangeDone(bool ok, int money, List<(byte DstPos, ItemSlot Slot)> received)
    {
        if (!_exShown) return;
        if (!ok)
        {
            SetExStatus("Trade failed (bags full / overweight).", true);
            RestoreMyOffer();
            CloseExchangeWindow();
            return;
        }
        foreach (var (dstPos, slot) in received)
        {
            int abs = GridStart + dstPos;
            if (abs >= 0 && abs < Inv.Length)
            {
                Inv[abs] = slot;
                Net.I.MirrorInventorySlot(abs, slot);
            }
            Floaters?.Item(slot.ItemId, slot.Count);
        }
        int goldGained = money - Sheet.Gold;
        if (goldGained > 0) Floaters?.Gold(goldGained);
        Sheet.SetGold(money);
        if (CharTabOpen()) RefreshInventoryUI();
        CombatNotice($"Trade with {_exPartnerName} complete.");
        CloseExchangeWindow();
    }

    private void OnExchangeCancel()
    {
        if (_exWaiting)
        {
            HideExchangeWait();
            ResetExchangeState();
            CombatNotice($"{_exPartnerName} is not available to trade.");
            return;
        }
        if (_exRequestPending)
        {
            _exRequestPending = false;
            _exAskDialog.Hide();
            ResetExchangeState();
            CombatNotice("The trade request was withdrawn.");
            return;
        }
        if (!_exShown) return;
        CombatNotice("The trade was cancelled.");
        RestoreMyOffer();
        CloseExchangeWindow();
    }

    private void RestoreMyOffer()
    {
        foreach (var o in _exMyOffer)
        {
            int abs = o.SourceAbs;
            bool sameStack = abs >= GridStart && abs < Inv.Length
                             && !Inv[abs].IsEmpty && Inv[abs].ItemId == o.ItemId;
            if (!sameStack && (abs < GridStart || abs >= Inv.Length || !Inv[abs].IsEmpty))
                abs = Inv.FirstFreeGridSlot();
            if (abs < 0) continue;

            if (sameStack)
            {
                var merged = Inv[abs];
                merged.Count = (short)Mathf.Min(Inventory.StackMax, merged.Count + o.Count);
                Inv[abs] = merged;
            }
            else
                Inv[abs] = new ItemSlot { ItemId = o.ItemId, Count = (short)o.Count, Durability = o.Dura };
            Net.I.MirrorInventorySlot(abs, Inv[abs]);
        }
        if (_exMyGoldOffer > 0) { Sheet.Receive(_exMyGoldOffer); Net.I.RaiseGold(Sheet.Gold); }
        _exMyOffer.Clear();
        _exMyGoldOffer = 0;
        if (CharTabOpen()) RefreshInventoryUI();
    }

    private void SetExStatus(string text, bool warn)
    {
        _exStatus.Text = text;
        _exStatus.AddThemeColorOverride("font_color", warn ? new Color("ff6a6a") : Colors.White);
    }
}
