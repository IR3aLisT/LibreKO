using System;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const string CharacterSealTitle = "Character Seal and Unseal";
    private const int CharacterSealBodyWidth = 380;
    private const float CharacterSealRowHeight = 36f;
    private const float CharacterSealKeySize = 40f;
    private const int CharacterSealKeyColumns = 5;
    private const float SealSheetCellSize = 40f;
    private const int SealSheetEquipColumns = 3;
    private const int SealSheetBagColumns = 7;
    private static readonly Color CharacterSealGoodColour = new(0.4f, 0.55f, 1f);
    private static readonly Color CharacterSealBadColour = new("ff3b3b");
    private static readonly Color CharacterSealLilac = new("9b9bff");
    private static readonly Color SealSheetKarusColour = new("ff6060");
    private static readonly Color SealSheetElmoradColour = new("807fff");

    private CanvasLayer _csLayer = null!;
    private HudWindow _csWindow = null!;
    private VBoxContainer _csSealingPage = null!, _csSelectPage = null!, _csCodePage = null!;
    private Label _csSelectTitle = null!, _csSelectPrompt = null!, _csCodeStars = null!;
    private Button _csSelectOk = null!;
    private readonly Button[] _csSlotButtons = new Button[CharacterRecordWire.SlotCount];
    private GridContainer _csKeys = null!;
    private HudWindow _csTrans = null!;
    private WarehouseCell _csSocket = null!;
    private Label _csTransHeadline = null!, _csTransGold = null!;
    private BagCompanion? _csCompanion;
    private HudWindow _csSheet = null!;
    private Label _csSheetName = null!, _csSheetLevel = null!, _csSheetRace = null!, _csSheetClass = null!;
    private Label _csSheetRebirth = null!, _csSheetLoyalty = null!, _csSheetGold = null!, _csSheetAchievements = null!;
    private readonly Label[] _csSheetStats = new Label[CharacterSealWire.StatCount];
    private readonly Label[] _csSheetSkills = new Label[CharacterSealWire.SkillTrees + 1];
    private readonly Label[] _csSheetSkillNames = new Label[CharacterSealWire.SkillTrees - 1];
    private readonly ItemSlotView[] _csSheetCells = new ItemSlotView[CharacterSealWire.SheetItems];

    private CharacterSeal.Mode _csMode = CharacterSeal.Mode.Seal;
    private CharacterSummary[] _csSlots = [];
    private int _csBagSlot = -1;
    private int _csPicked = -1;
    private string _csCode = "";
    private bool _csTransShown;
    private ulong _csSheetAskedAt;
    private int _csSheetId;

    private void CharacterSealInit()
    {
        _csLayer = new CanvasLayer { Layer = 75 };
        AddChild(_csLayer);
        BuildCharacterSealWindow();
        BuildCharacterSealTrans();
        BuildSealedSheet();
        Net.I.CharacterSealListEvent += OnCharacterSealList;
        Net.I.CharacterSealEvent += OnCharacterSealed;
        Net.I.CharacterUnsealEvent += OnCharacterUnsealed;
        Net.I.SealedCharacterSheetEvent += OnSealedSheet;
        Net.I.SealedAchievementsEvent += OnSealedAchievements;
        Net.I.InventorySlotEvent += OnCharacterSealSlot;
    }

    private void CharacterSealDispose()
    {
        Net.I.CharacterSealListEvent -= OnCharacterSealList;
        Net.I.CharacterSealEvent -= OnCharacterSealed;
        Net.I.CharacterUnsealEvent -= OnCharacterUnsealed;
        Net.I.SealedCharacterSheetEvent -= OnSealedSheet;
        Net.I.SealedAchievementsEvent -= OnSealedAchievements;
        Net.I.InventorySlotEvent -= OnCharacterSealSlot;
    }

    private static string SealText(int id, string fallback = "") => ItemData.Text(id, fallback);

    private void CharacterSealLog(int textId, Color colour, string fallback = "") =>
        CombatLogAdd(SealText(textId, fallback), CombatLogKind.Status, colour);

    private void OpenCharacterSeal()
    {
        CloseNpcDialog();
        ShowCharacterSealPage(_csSealingPage, CharacterSealTitle);
    }

    private void ShowCharacterSealPage(Control page, string title)
    {
        _csSealingPage.Visible = page == _csSealingPage;
        _csSelectPage.Visible = page == _csSelectPage;
        _csCodePage.Visible = page == _csCodePage;
        _csWindow.Title = title;
        _csWindow.Visible = true;
        _csWindow.ResetSize();
    }

    private void CloseCharacterSeal()
    {
        _csWindow.Visible = false;
        _csCode = "";
        _csPicked = -1;
    }

    private void BuildCharacterSealWindow()
    {
        _csWindow = new HudWindow("char_seal", CharacterSealTitle, CharacterSealBodyWidth) { Visible = false };
        _csWindow.Closed += CloseCharacterSeal;
        _csLayer.AddChild(_csWindow);
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 10);
        _csWindow.Body.AddChild(body);

        _csSealingPage = new VBoxContainer();
        _csSealingPage.AddThemeConstantOverride("separation", 10);
        body.AddChild(_csSealingPage);
        string help = SealText(CharacterSeal.HelpText);
        foreach (var mode in new[] { CharacterSeal.Mode.Seal, CharacterSeal.Mode.Unseal })
        {
            var (title, text) = CharacterSeal.HelpPanel(help, mode);
            _csSealingPage.AddChild(UiTheme.Text(title, 13, UiTheme.GoldBright, HorizontalAlignment.Center));
            var panel = UiTheme.Section();
            var label = UiTheme.Text(text, 12, UiTheme.TextHi);
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            label.CustomMinimumSize = new Vector2(CharacterSealBodyWidth - 30, 64);
            panel.AddChild(label);
            _csSealingPage.AddChild(panel);
        }
        _csSealingPage.AddChild(CharacterSealButtons(
            ("Seal", () => OpenCharacterSealTrans(CharacterSeal.Mode.Seal)),
            ("Unseal", () => OpenCharacterSealTrans(CharacterSeal.Mode.Unseal))));

        _csSelectPage = new VBoxContainer();
        _csSelectPage.AddThemeConstantOverride("separation", 8);
        body.AddChild(_csSelectPage);
        _csSelectTitle = UiTheme.Text("", 13, UiTheme.GoldBright, HorizontalAlignment.Center);
        _csSelectPage.AddChild(_csSelectTitle);
        var slots = UiTheme.Section();
        var slotBox = new VBoxContainer();
        slotBox.AddThemeConstantOverride("separation", 6);
        slots.AddChild(slotBox);
        for (int i = 0; i < _csSlotButtons.Length; i++)
        {
            int index = i;
            var button = new Button
            {
                Alignment = HorizontalAlignment.Left,
                FocusMode = Control.FocusModeEnum.None,
                ClipText = true,
                CustomMinimumSize = new Vector2(0, CharacterSealRowHeight),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            button.AddThemeFontSizeOverride("font_size", 13);
            button.AddThemeColorOverride("font_hover_color", UiTheme.GoldBright);
            button.AddThemeColorOverride("font_disabled_color", UiTheme.TextLo);
            button.AddThemeStyleboxOverride("hover", UiTheme.ListRow(true));
            button.AddThemeStyleboxOverride("pressed", UiTheme.ListRow(true));
            button.AddThemeStyleboxOverride("disabled", UiTheme.ListRow(false));
            button.Pressed += () => PickCharacterSealSlot(index);
            _csSlotButtons[i] = button;
            slotBox.AddChild(button);
        }
        _csSelectPage.AddChild(slots);
        _csSelectPrompt = UiTheme.Text("", 12, UiTheme.TextDim, HorizontalAlignment.Center);
        _csSelectPage.AddChild(_csSelectPrompt);
        var selectButtons = CharacterSealButtons(("Seal", ConfirmCharacterSealSlot), ("Cancel", CloseCharacterSeal));
        _csSelectOk = (Button)selectButtons.GetChild(0);
        _csSelectPage.AddChild(selectButtons);

        _csCodePage = new VBoxContainer();
        _csCodePage.AddThemeConstantOverride("separation", 8);
        body.AddChild(_csCodePage);
        _csCodePage.AddChild(UiTheme.Text("To Seal, Please Enter the Code", 13, UiTheme.GoldBright, HorizontalAlignment.Center));
        _csCodeStars = UiTheme.Text("", 18, UiTheme.TextHi, HorizontalAlignment.Center);
        var starsPanel = UiTheme.Section();
        starsPanel.CustomMinimumSize = new Vector2(0, 40);
        starsPanel.AddChild(_csCodeStars);
        _csCodePage.AddChild(starsPanel);
        _csKeys = new GridContainer { Columns = CharacterSealKeyColumns, SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        _csKeys.AddThemeConstantOverride("h_separation", 6);
        _csKeys.AddThemeConstantOverride("v_separation", 6);
        _csCodePage.AddChild(_csKeys);
        _csCodePage.AddChild(CharacterSealButtons(("Shuffle", ShuffleCharacterSealKeys), ("Delete", PopCharacterSealDigit)));
        _csCodePage.AddChild(CharacterSealButtons(("OK", SendCharacterSealCode), ("Cancel", CloseCharacterSeal)));
    }

    private static HBoxContainer CharacterSealButtons(params (string Label, Action Pressed)[] buttons)
    {
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 12);
        foreach (var (label, pressed) in buttons)
        {
            var button = UiTheme.SmallButton(label, "");
            button.CustomMinimumSize = new Vector2(120, 28);
            button.Pressed += pressed;
            row.AddChild(button);
        }
        return row;
    }

    private void ShuffleCharacterSealKeys()
    {
        foreach (var child in _csKeys.GetChildren()) child.QueueFree();
        foreach (int digit in SealKeypadOrder())
        {
            var key = UiTheme.SmallButton(digit.ToString(), "");
            key.CustomMinimumSize = new Vector2(CharacterSealKeySize, CharacterSealKeySize);
            key.Pressed += () => PushCharacterSealDigit(digit);
            _csKeys.AddChild(key);
        }
    }

    private void PushCharacterSealDigit(int digit)
    {
        if (_csCode.Length >= CharacterSeal.MaxCodeDigits) return;
        _csCode += (char)('0' + digit);
        _csCodeStars.Text = new string('*', _csCode.Length);
    }

    private void PopCharacterSealDigit()
    {
        if (_csCode.Length > 0) _csCode = _csCode[..^1];
        _csCodeStars.Text = new string('*', _csCode.Length);
    }

    private void OpenCharacterSealTrans(CharacterSeal.Mode mode)
    {
        _csMode = mode;
        _csBagSlot = -1;
        _csWindow.Visible = false;
        _csTrans.Visible = true;
        _csTransShown = true;
        _csCompanion ??= new BagCompanion(CharacterSealTakeFromBag, CharacterSealBagFit, _ => "", CloseCharacterSealTrans);
        AttachBagCompanion(_csCompanion);
        RefreshCharacterSealTrans();
    }

    private void CloseCharacterSealTrans()
    {
        if (!_csTransShown) return;
        _csTransShown = false;
        _csTrans.Visible = false;
        HideItemTooltip();
        if (_csCompanion != null) DetachBagCompanion(_csCompanion);
    }

    private bool CharacterSealTakeFromBag(int abs)
    {
        if (!InMainBag(abs)) return false;
        if (Inv[abs].IsEmpty) return true;
        if (Inv[abs].State == ItemFlag.Duplicate)
        {
            CharacterSealLog(CharacterSeal.DuplicateSerialText, CharacterSealLilac);
            return true;
        }
        if (CharacterSeal.Fits(_csMode, ItemData.Get(Inv[abs].ItemId)?.Kind ?? 0))
        {
            _csBagSlot = abs;
            RefreshCharacterSealTrans();
        }
        return true;
    }

    private BagFit CharacterSealBagFit(int abs) =>
        abs == _csBagSlot ? BagFit.Staged
        : CharacterSeal.Fits(_csMode, ItemData.Get(Inv[abs].ItemId)?.Kind ?? 0) ? BagFit.Normal
        : BagFit.Unfit;

    private void OnCharacterSealSlot(int abs, ItemSlot item)
    {
        if (_csTransShown) RefreshCharacterSealTrans();
    }

    private void RefreshCharacterSealTrans()
    {
        if (_csBagSlot >= 0 && (_csBagSlot >= Inv.Length || Inv[_csBagSlot].IsEmpty)) _csBagSlot = -1;
        var held = _csBagSlot >= 0 ? Inv[_csBagSlot] : default;
        _csSocket.Set(held);
        _csTransHeadline.Text = held.IsEmpty
            ? _csMode == CharacterSeal.Mode.Seal ? "Place a Character Seal Scroll in the slot." : "Place a Cypher Ring in the slot."
            : ItemData.Get(held.ItemId)?.Name ?? "";
        _csTransGold.Text = $"{Sheet.Gold:n0}";
        RefreshBagFit();
    }

    private void ConfirmCharacterSealTrans()
    {
        if (_csBagSlot < 0)
        {
            RefreshCharacterSealTrans();
            return;
        }
        int ask = _csMode == CharacterSeal.Mode.Seal ? CharacterSeal.SealAskText : CharacterSeal.UnsealAskText;
        Notice.Confirm(this, SealText(ask), "OK", "Cancel", () => Net.I.SendCharacterSealList(), title: CharacterSealTitle);
    }

    private void BuildCharacterSealTrans()
    {
        _csTrans = new HudWindow("char_seal_trans", "Character Seal/Unseal", CharacterSealBodyWidth) { Visible = false };
        _csTrans.Closed += CloseCharacterSealTrans;
        _csLayer.AddChild(_csTrans);
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 10);
        _csTrans.Body.AddChild(body);

        var stage = UiTheme.Section();
        body.AddChild(stage);
        var stageBox = new VBoxContainer();
        stageBox.AddThemeConstantOverride("separation", 8);
        stage.AddChild(stageBox);
        _csSocket = new WarehouseCell(0) { SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        _csSocket.CustomMinimumSize = new Vector2(52, 52);
        _csSocket.OnActivate += _ => { _csBagSlot = -1; RefreshCharacterSealTrans(); };
        _csSocket.OnHover += HoverWarehouseCell;
        _csSocket.OnHoverEnd += HideItemTooltip;
        stageBox.AddChild(_csSocket);
        _csTransHeadline = UiTheme.Text("", 13, UiTheme.TextHi, HorizontalAlignment.Center);
        stageBox.AddChild(_csTransHeadline);
        var notice = UiTheme.Text("You cannot unseal the character that was sealed from enemy nation.", 11, UiTheme.TextDim, HorizontalAlignment.Center);
        notice.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        notice.CustomMinimumSize = new Vector2(CharacterSealBodyWidth - 30, 0);
        stageBox.AddChild(notice);

        body.AddChild(CharacterSealButtons(("OK", ConfirmCharacterSealTrans), ("Cancel", CloseCharacterSealTrans)));

        var footer = new HBoxContainer();
        var goldLabel = UiTheme.Text("Gold", 12, UiTheme.TextLo);
        goldLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        footer.AddChild(goldLabel);
        _csTransGold = UiTheme.Text("", 12, UiTheme.Gold, HorizontalAlignment.Right);
        footer.AddChild(_csTransGold);
        body.AddChild(footer);
    }

    private void OnCharacterSealList(CharacterSealList list)
    {
        if (!list.Ok)
        {
            if (list.Code == CharacterSeal.CodeTooSoon) CharacterSealLog(CharacterSeal.TooSoonText, CharacterSealBadColour);
            return;
        }
        if (_csBagSlot < 0) return;
        _csSlots = list.Slots;
        _csPicked = -1;
        CloseCharacterSealTrans();
        bool seal = _csMode == CharacterSeal.Mode.Seal;
        _csSelectTitle.Text = seal ? "Select Character Slot" : "Select Slot to Unseal Character";
        _csSelectPrompt.Text = seal ? ">> Select Character to Seal <<" : "";
        _csSelectOk.Text = seal ? "Seal" : "Unseal";
        RefreshCharacterSealSlots();
        ShowCharacterSealPage(_csSelectPage, seal ? "Character Seal" : "Unseal Character");
    }

    private void RefreshCharacterSealSlots()
    {
        string empty = SealText(CharacterSeal.EmptySlotText, "Empty Slot");
        for (int i = 0; i < _csSlotButtons.Length; i++)
        {
            var slot = i < _csSlots.Length ? _csSlots[i] : new CharacterSummary();
            var button = _csSlotButtons[i];
            button.Text = CharacterSeal.SlotLine(slot, SealedClassName(slot.Class), empty);
            button.Disabled = !CharacterSeal.SlotPickable(_csMode, slot, Net.I.LastEnter.Name);
            button.AddThemeStyleboxOverride("normal", UiTheme.ListRow(i == _csPicked));
            button.AddThemeColorOverride("font_color", i == _csPicked ? UiTheme.GoldBright : UiTheme.TextHi);
        }
    }

    private static string SealedClassName(int cls) =>
        ItemData.Text(EquipRules.ClassNameTextId(cls % 100), CharacterClassCatalog.DisplayName(cls));

    private void PickCharacterSealSlot(int index)
    {
        _csPicked = index;
        RefreshCharacterSealSlots();
    }

    private void ConfirmCharacterSealSlot()
    {
        if (_csPicked < 0 || _csPicked >= _csSlots.Length)
        {
            CharacterSealLog(CharacterSeal.InvalidCharacterText, CharacterSealBadColour, "Invalid character");
            return;
        }
        if (_csBagSlot < 0 || _csBagSlot >= Inv.Length || Inv[_csBagSlot].IsEmpty) return;
        if (_csMode == CharacterSeal.Mode.Unseal)
        {
            Net.I.SendCharacterUnseal((byte)(_csBagSlot - GridStart), Inv[_csBagSlot].ItemId, (byte)_csPicked);
            CloseCharacterSeal();
            return;
        }
        _csCode = "";
        _csCodeStars.Text = "";
        ShuffleCharacterSealKeys();
        ShowCharacterSealPage(_csCodePage, "Character Seal");
    }

    private void SendCharacterSealCode()
    {
        if (_csCode.Length == 0 || _csPicked < 0 || _csPicked >= _csSlots.Length) return;
        if (_csBagSlot < 0 || _csBagSlot >= Inv.Length || Inv[_csBagSlot].IsEmpty) return;
        Net.I.SendCharacterSeal((byte)(_csBagSlot - GridStart), Inv[_csBagSlot].ItemId, _csSlots[_csPicked].Name, _csCode);
        CloseCharacterSeal();
    }

    private void OnCharacterSealed(CharacterSealOutcome outcome)
    {
        CloseCharacterSeal();
        if (outcome.Result != CharacterSealWire.Succeeded)
        {
            int text = CharacterSeal.SealRefusalText(outcome.Result, outcome.Code);
            string line = text == CharacterSeal.VoteWarningText
                ? TextTemplate.Fill(SealText(text), SealText(CharacterSeal.JuraidText, "Juraid"))
                : SealText(text);
            CombatLogAdd(line, CombatLogKind.Status, CharacterSealBadColour);
            if (text == CharacterSeal.VaultLockedText)
                Notice.Show(this, SealText(CharacterSeal.VaultLockedText), CharacterSealTitle);
            return;
        }
        int abs = GridStart + outcome.BagPos;
        if (abs >= GridStart && abs < Inv.Length)
        {
            var ring = Inv[abs];
            ring.ItemId = outcome.ItemId;
            ring.Count = 1;
            ring.UniqueId = outcome.UniqueId;
            Inv[abs] = ring;
            Net.I.MirrorInventorySlot(abs, ring);
            if (CharTabOpen()) RefreshInventoryUI();
        }
        CharacterSealLog(CharacterSeal.SealedText, CharacterSealGoodColour);
    }

    private void OnCharacterUnsealed(CharacterUnsealOutcome outcome)
    {
        CloseCharacterSeal();
        if (outcome.Result != CharacterSealWire.Succeeded)
        {
            CharacterSealLog(CharacterSeal.UnsealFailedText, CharacterSealBadColour);
            return;
        }
        int abs = GridStart + outcome.BagPos;
        if (abs >= GridStart && abs < Inv.Length)
        {
            Inv[abs] = default;
            Net.I.MirrorInventorySlot(abs, default);
            if (CharTabOpen()) RefreshInventoryUI();
        }
        CharacterSealLog(CharacterSeal.UnsealedText, CharacterSealGoodColour);
    }

    private static bool IsCypherRing(ItemSlot item) =>
        !item.IsEmpty && ItemData.Get(item.ItemId)?.Kind == CharacterSeal.RingKind;

    private bool ShowSealedCharacterSheet(ItemSlot item)
    {
        if (!IsCypherRing(item) || item.UniqueId == 0) return false;
        if (_csSheetId == item.UniqueId && _csSheet.Visible) return true;
        ulong now = Time.GetTicksMsec();
        if (_csSheetAskedAt != 0 && now - _csSheetAskedAt < CharacterSeal.SheetRequestCooldownMs) return true;
        _csSheetAskedAt = now;
        _csSheetId = item.UniqueId;
        Net.I.SendSealedCharacterSheet(item.UniqueId);
        return true;
    }

    private void BuildSealedSheet()
    {
        _csSheet = new HudWindow("char_seal_sheet", "Cypher Ring") { Visible = false };
        _csSheet.Closed += () => { _csSheet.Visible = false; HideItemTooltip(); };
        _csLayer.AddChild(_csSheet);
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 8);
        _csSheet.Body.AddChild(body);

        _csSheetName = UiTheme.Text("", 15, UiTheme.TextHi, HorizontalAlignment.Center);
        body.AddChild(_csSheetName);

        var top = new HBoxContainer();
        top.AddThemeConstantOverride("separation", 12);
        body.AddChild(top);

        var left = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        left.AddThemeConstantOverride("separation", 4);
        top.AddChild(left);
        var idPanel = UiTheme.Section();
        var idBox = new VBoxContainer();
        idBox.AddThemeConstantOverride("separation", 2);
        idPanel.AddChild(idBox);
        left.AddChild(idPanel);
        _csSheetLevel = SheetRow(idBox, "Lv");
        _csSheetRebirth = SheetRow(idBox, "RB:");
        _csSheetRace = SheetRow(idBox, SealText(CharacterSeal.RaceLabelText, "Race :"));
        _csSheetClass = SheetRow(idBox, SealText(CharacterSeal.JobLabelText, "Job :"));
        var achievements = UiTheme.SmallButton("Achievements", "");
        achievements.Pressed += AskSealedAchievements;
        left.AddChild(achievements);

        var statPanel = UiTheme.Section();
        var statGrid = new GridContainer { Columns = 4 };
        statGrid.AddThemeConstantOverride("h_separation", 8);
        statGrid.AddThemeConstantOverride("v_separation", 3);
        statPanel.AddChild(statGrid);
        left.AddChild(statPanel);
        string[] statNames = ["STR :", "HP :", "DEX :", "INT :", "MP :"];
        for (int i = 0; i < CharacterSealWire.StatCount; i++)
        {
            statGrid.AddChild(UiTheme.Text(statNames[i], 12, UiTheme.TextLo));
            _csSheetStats[i] = UiTheme.Text("", 12, UiTheme.TextHi);
            statGrid.AddChild(_csSheetStats[i]);
            if (i == 0)
            {
                statGrid.AddChild(UiTheme.Text("Point :", 12, UiTheme.TextLo));
                _csSheetSkills[0] = UiTheme.Text("", 12, UiTheme.TextHi);
                statGrid.AddChild(_csSheetSkills[0]);
            }
            else if (i <= _csSheetSkillNames.Length)
            {
                _csSheetSkillNames[i - 1] = UiTheme.Text("", 12, UiTheme.TextLo);
                statGrid.AddChild(_csSheetSkillNames[i - 1]);
                _csSheetSkills[i] = UiTheme.Text("", 12, UiTheme.TextHi);
                statGrid.AddChild(_csSheetSkills[i]);
            }
            else
            {
                statGrid.AddChild(UiTheme.Text("Master:", 12, UiTheme.TextLo));
                _csSheetSkills[i] = UiTheme.Text("", 12, UiTheme.TextHi);
                statGrid.AddChild(_csSheetSkills[i]);
            }
        }
        var wealth = UiTheme.Section();
        var wealthBox = new VBoxContainer();
        wealthBox.AddThemeConstantOverride("separation", 2);
        wealth.AddChild(wealthBox);
        left.AddChild(wealth);
        _csSheetLoyalty = SheetRow(wealthBox, "CONT");
        _csSheetGold = SheetRow(wealthBox, "Gold");

        var equip = new GridContainer { Columns = SealSheetEquipColumns };
        equip.AddThemeConstantOverride("h_separation", 4);
        equip.AddThemeConstantOverride("v_separation", 4);
        var equipPanel = UiTheme.Section();
        equipPanel.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        equipPanel.AddChild(equip);
        top.AddChild(equipPanel);
        var bag = new GridContainer { Columns = SealSheetBagColumns, SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        bag.AddThemeConstantOverride("h_separation", 4);
        bag.AddThemeConstantOverride("v_separation", 4);
        var bagPanel = UiTheme.Section();
        bagPanel.AddChild(bag);
        body.AddChild(bagPanel);
        for (int i = 0; i < _csSheetCells.Length; i++)
        {
            var cell = new ItemSlotView(SealSheetCellSize) { Index = i };
            cell.Hovered += view => ShowItemTooltip(-1, view.Item);
            cell.Unhovered += _ => HideItemTooltip();
            _csSheetCells[i] = cell;
            (i < CharacterSealWire.EquipItems ? equip : bag).AddChild(cell);
        }

        _csSheetAchievements = UiTheme.Text("", 11, UiTheme.TextDim);
        _csSheetAchievements.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _csSheetAchievements.Visible = false;
        body.AddChild(_csSheetAchievements);
    }

    private static Label SheetRow(VBoxContainer box, string label)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        var name = UiTheme.Text(label, 12, UiTheme.TextLo);
        name.CustomMinimumSize = new Vector2(52, 0);
        row.AddChild(name);
        var value = UiTheme.Text("", 12, UiTheme.TextHi);
        value.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(value);
        box.AddChild(row);
        return value;
    }

    private static readonly string[][] SealSheetSkillNames =
    [
        ["Attack:", "Defense:", "Passion:"],
        ["Archery:", "Dagger:", "Explore:"],
        ["Flame:", "Glacier:", "Lightning:"],
        ["Recovery:", "Unsuitable:", "Holy Spirit:"],
        ["Attack:", "Defense:", "Devil:"],
    ];

    private void OnSealedSheet(SealedCharacterSheet? sheet)
    {
        if (sheet == null)
        {
            CharacterSealLog(CharacterSeal.SheetFailedText, CharacterSealBadColour, "Failed.");
            return;
        }
        _csSheetName.Text = sheet.Name;
        _csSheetName.AddThemeColorOverride("font_color",
            sheet.Nation == Nations.Karus ? SealSheetKarusColour : SealSheetElmoradColour);
        _csSheetLevel.Text = sheet.Level.ToString();
        _csSheetRebirth.Text = Net.I.PetItems.TryGetValue(_csSheetId, out var ring) ? ring.Tail.ToString() : "0";
        _csSheetRace.Text = StarterStats.RaceName(sheet.Race);
        _csSheetClass.Text = SealedClassName(sheet.Class);
        for (int i = 0; i < CharacterSealWire.StatCount; i++)
            _csSheetStats[i].Text = CharacterSeal.StatLine(sheet.Stats[i], sheet.StatBonus[i]);
        _csSheetSkills[0].Text = sheet.FreePoints.ToString();
        int family = Math.Clamp(CharacterClassCatalog.Family(sheet.Class), 1, SealSheetSkillNames.Length) - 1;
        for (int i = 0; i < _csSheetSkillNames.Length; i++)
            _csSheetSkillNames[i].Text = SealSheetSkillNames[family][i];
        for (int i = 0; i < CharacterSealWire.SkillTrees; i++)
            _csSheetSkills[i + 1].Text = sheet.SkillTrees[i].ToString();
        _csSheetLoyalty.Text = $"{sheet.Loyalty:n0}";
        _csSheetGold.Text = $"{sheet.Gold:n0}";
        for (int i = 0; i < _csSheetCells.Length; i++) _csSheetCells[i].Set(sheet.Items[i]);
        _csSheetAchievements.Visible = false;
        _csSheet.Visible = true;
        _csSheet.ResetSize();
    }

    private void AskSealedAchievements()
    {
        if (_csSheetName.Text.Length > 0) Net.I.SendSealedAchievements(_csSheetName.Text);
    }

    private void OnSealedAchievements(int[] ids)
    {
        if (!_csSheet.Visible) return;
        var names = new System.Collections.Generic.List<string>();
        foreach (int id in ids)
            if (AchievementData.Get(id) is { } info && info.Name.Length > 0) names.Add(info.Name);
        _csSheetAchievements.Text = names.Count > 0 ? string.Join(", ", names) : "No achievements.";
        _csSheetAchievements.Visible = true;
        _csSheet.ResetSize();
    }
}
