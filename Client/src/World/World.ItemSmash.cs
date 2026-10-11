using System.Collections.Generic;
using System.Linq;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const string ItemSmashFile = "res://assets/items/item_smash.json";
    private const string ItemSmashTitle = "Disassemble";
    private const double SmashSpinInterval = 0.05;
    private const int SmashGridColumns = 3;
    private const int TextSmashPressStart = 6711;

    private CanvasLayer _smashLayer = null!;
    private HudWindow _smashPanel = null!;
    private BagCompanion? _smashCompanion;
    private Label _smashMessage = null!;
    private Label _smashSubMessage = null!;
    private Button _smashStartBtn = null!;
    private Button _smashStopBtn = null!;
    private Button _smashCancelBtn = null!;
    private UpgradeSocket _smashSocket = null!;
    private readonly UpgradeSocket[] _smashResults = new UpgradeSocket[ItemSmash.ResultSlots];
    private SmashTable? _smashTable;

    private bool _smashShown;
    private bool _smashSpinning;
    private bool _smashBusy;
    private int _smashNpcTemplate;
    private int _smashNpcId = ItemCombine.NoCraftsman;
    private int _smashSlot = -1;
    private double _smashSpinClock;
    private IReadOnlyList<int> _smashPool = System.Array.Empty<int>();

    private void ItemSmashInit()
    {
        BuildSmashPanel();
        Net.I.ItemSmashReplyEvent += OnItemSmashReply;
    }

    private void ItemSmashDispose()
    {
        Net.I.ItemSmashReplyEvent -= OnItemSmashReply;
    }

    private SmashTable SmashData => _smashTable ??= LoadSmashTable();

    private static SmashTable LoadSmashTable()
    {
        using var file = Godot.FileAccess.Open(ItemSmashFile, Godot.FileAccess.ModeFlags.Read);
        if (file == null) return SmashTable.Empty;
        var root = Json.ParseString(file.GetAsText()).AsGodotDictionary();
        var classes = new Dictionary<int, SmashClass>();
        foreach (var (grade, row) in root["classes"].AsGodotDictionary())
        {
            var entry = row.AsGodotDictionary();
            classes[int.Parse(grade.AsString())] = new SmashClass(entry["band"].AsInt32(), entry["rolls"].AsInt32());
        }
        var bands = new Dictionary<int, IReadOnlyList<int>>();
        foreach (var (band, items) in root["bands"].AsGodotDictionary())
            bands[int.Parse(band.AsString())] = items.AsGodotArray().Select(item => item.AsInt32()).ToList();
        return new SmashTable(classes, bands);
    }

    private void BuildSmashPanel()
    {
        _smashLayer = new CanvasLayer { Layer = 75 };
        AddChild(_smashLayer);

        _smashPanel = new HudWindow("itemsmash", ItemSmashTitle, bodyMinWidth: 460) { Visible = false };
        _smashPanel.Closed += CloseItemSmash;
        _smashLayer.AddChild(_smashPanel);

        var section = UiTheme.Section();
        _smashPanel.Body.AddChild(section);
        var altar = new VBoxContainer();
        altar.AddThemeConstantOverride("separation", 10);
        section.AddChild(altar);

        var bench = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        bench.AddThemeConstantOverride("separation", 16);
        altar.AddChild(bench);

        _smashSocket = new UpgradeSocket("", 64);
        _smashSocket.Cleared += CancelItemSmash;
        _smashSocket.Hovered += held => ShowItemTooltip(_smashSlot, held);
        _smashSocket.Unhovered += HideItemTooltip;
        bench.AddChild(BuildUpgradeColumn("Item", _smashSocket));
        bench.AddChild(UiTheme.Text("=", 26, UiTheme.GoldDark));

        var grid = new GridContainer { Columns = SmashGridColumns };
        grid.AddThemeConstantOverride("h_separation", 6);
        grid.AddThemeConstantOverride("v_separation", 6);
        for (int i = 0; i < _smashResults.Length; i++)
        {
            var socket = new UpgradeSocket("", 48, interactive: false);
            socket.Hovered += held => ShowItemTooltip(-1, held);
            socket.Unhovered += HideItemTooltip;
            _smashResults[i] = socket;
            grid.AddChild(socket);
        }
        bench.AddChild(BuildUpgradeColumn("Pieces", grid));

        _smashMessage = UiTheme.Text("", 14, UiTheme.TextHi, HorizontalAlignment.Center);
        _smashMessage.CustomMinimumSize = new Vector2(0, 30);
        _smashMessage.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        altar.AddChild(_smashMessage);

        _smashSubMessage = UiTheme.Text("", 12, UiTheme.TextLo, HorizontalAlignment.Center);
        _smashSubMessage.CustomMinimumSize = new Vector2(0, 22);
        _smashSubMessage.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        altar.AddChild(_smashSubMessage);

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 8);
        altar.AddChild(buttons);
        _smashStartBtn = PieceButton("Start", StartItemSmash);
        buttons.AddChild(_smashStartBtn);
        _smashStopBtn = PieceButton("Stop", StopItemSmash);
        buttons.AddChild(_smashStopBtn);
        _smashCancelBtn = PieceButton("Cancel", CancelItemSmash);
        buttons.AddChild(_smashCancelBtn);

        ClearSmashBench();
    }

    private void OpenItemSmash(int npcTemplate, int talkedTo)
    {
        _smashNpcTemplate = npcTemplate;
        _smashNpcId = talkedTo >= 0 ? talkedTo : SmashNpc();
        _smashBusy = false;
        CloseVendor();
        ClearSmashBench();
        foreach (var socket in _smashResults) socket.Clear();
        _smashPanel.Visible = true;
        _smashShown = true;
        _smashCompanion ??= new BagCompanion(SmashTakeFromBag, SmashBagFit, _ => "", CloseItemSmash);
        AttachBagCompanion(_smashCompanion);
    }

    private int SmashNpc() => _self == null
        ? ItemCombine.NoCraftsman
        : ItemCombine.Craftsman(
            _ents.Where(e => !e.Value.Dead).Select(e => (e.Key, e.Value.NpcId, FlatDistance(_self.Position, e.Value.Body.Position))),
            _smashNpcTemplate);

    private void CloseItemSmash()
    {
        if (!_smashShown) return;
        _smashShown = false;
        _smashSpinning = false;
        _smashBusy = false;
        _smashPanel.Visible = false;
        HideItemTooltip();
        if (_smashCompanion != null) DetachBagCompanion(_smashCompanion);
    }

    private void ClearSmashBench()
    {
        _smashSlot = -1;
        _smashSpinning = false;
        _smashPool = System.Array.Empty<int>();
        _smashSocket?.Clear();
        SetSmashMessage(ItemData.Text(ItemSmash.PutItemText, "Please put an item to disassemble."), false);
        _smashSubMessage.Text = "";
        RefreshSmashActions();
        RefreshBagFit();
    }

    private bool SmashTakeFromBag(int abs)
    {
        if (!InMainBag(abs)) return false;
        PlaceSmashItem(abs);
        return true;
    }

    private BagFit SmashBagFit(int abs) =>
        abs == _smashSlot ? BagFit.Staged
        : SmashCheckOf(Inv[abs]) == SmashCheck.Allowed ? BagFit.Normal
        : BagFit.Unfit;

    private static SmashCheck SmashCheckOf(ItemSlot slot)
    {
        if (slot.IsEmpty || ItemData.Get(slot.ItemId) is not { } def) return SmashCheck.NotGear;
        return ItemSmash.Check(slot.ItemId, def.Kind, def.Race, ItemData.GradeClass(def, ItemData.ExtFor(slot.ItemId)), slot.State);
    }

    private void PlaceSmashItem(int abs)
    {
        if (_smashBusy || _smashSpinning || abs >= Inv.Length || Inv[abs].IsEmpty) return;
        if (abs == _smashSlot)
        {
            ClearSmashBench();
            return;
        }
        var check = SmashCheckOf(Inv[abs]);
        if (check == SmashCheck.Ignored) return;
        if (ItemSmash.CheckText(check) is { } refusal)
        {
            SetSmashMessage(ItemData.Text(refusal, ""), true);
            return;
        }
        var item = Inv[abs];
        _smashSlot = abs;
        _smashSocket.Set(item.ItemId, item.Count, item.Durability);
        foreach (var socket in _smashResults) socket.Clear();
        SetSmashMessage(ItemData.Text(TextSmashPressStart, "Press Start."), false);
        _smashSubMessage.Text = ItemData.DisplayName(item.ItemId);
        RefreshSmashActions();
        RefreshBagFit();
    }

    private void StartItemSmash()
    {
        if (!CanStartSmash()) return;
        SetSmashMessage(ItemData.Text(ItemSmash.OkStartsText, "When you press the OK button, disassembly begins."), false);
        Notice.Confirm(this,
            ItemData.Text(ItemSmash.WarningText, "Once you disassemble an item, it cannot be restored nor repurchased."),
            "OK", "Cancel", BeginSmashSpin, title: ItemData.Text(ItemSmash.WarningTitleText, ItemSmashTitle));
    }

    private void BeginSmashSpin()
    {
        if (!CanStartSmash() || ItemData.Get(Inv[_smashSlot].ItemId) is not { } def) return;
        if (SmashData.ClassOf(ItemData.GradeClass(def, ItemData.ExtFor(Inv[_smashSlot].ItemId))) is not { } smashClass)
        {
            ClearSmashBench();
            return;
        }
        _smashPool = SmashData.RewardsOf(smashClass.Band);
        _smashSpinning = true;
        _smashSpinClock = 0;
        SetSmashMessage(ItemData.Text(ItemSmash.PressStopText, "Press Stop."), false);
        Audio.PlayUi(ItemSmash.SpinSound);
        RefreshSmashActions();
    }

    private void StopItemSmash()
    {
        if (!_smashSpinning || _smashBusy || _smashSlot < 0) return;
        _smashSpinning = false;
        _smashBusy = true;
        RefreshSmashActions();
        Net.I.SendItemSmash(Inv[_smashSlot].ItemId, _smashSlot - GridStart, _smashNpcId);
    }

    private void CancelItemSmash()
    {
        if (_smashBusy) return;
        ClearSmashBench();
        foreach (var socket in _smashResults) socket.Clear();
    }

    private void ItemSmashTick(double delta)
    {
        if (!_smashShown || !_smashSpinning || _smashPool.Count == 0) return;
        _smashSpinClock += delta;
        if (_smashSpinClock < SmashSpinInterval) return;
        _smashSpinClock = 0;
        int slot = GD.RandRange(0, _smashResults.Length - 1);
        _smashResults[slot].Set(_smashPool[GD.RandRange(0, _smashPool.Count - 1)], 1, 0);
    }

    private void OnItemSmashReply(ItemSmashReply reply)
    {
        if (!_smashShown) return;
        _smashBusy = false;
        _smashSpinning = false;
        foreach (var socket in _smashResults) socket.Clear();

        if (reply.Result != ItemSmash.Succeeded)
        {
            string text = ItemSmash.ReplyText(reply.Result) is { } textId ? ItemData.Text(textId, "") : "";
            ClearSmashBench();
            if (reply.Result == ItemSmash.WrongNpc)
            {
                if (text.Length > 0) CombatLogAdd(text, CombatLogKind.Status, WhRefusalTint);
                CloseItemSmash();
                return;
            }
            if (text.Length > 0) SetSmashMessage(text, true);
            return;
        }

        int abs = GridStart + reply.BagSlot;
        if (abs < Inv.Length && Inv[abs].ItemId == reply.ItemId)
        {
            Inv[abs] = default;
            Net.I.MirrorInventorySlot(abs, default);
        }
        ClearSmashBench();
        for (int i = 0; i < reply.Pieces.Count && i < _smashResults.Length; i++)
            _smashResults[i].Set(reply.Pieces[i].ItemId, (short)reply.Pieces[i].Count, 0);
        _smashSubMessage.Text = string.Join(", ", reply.Pieces.Select(piece => ItemData.DisplayName(piece.ItemId)));
        if (CharTabOpen()) RefreshInventoryUI();
    }

    private void SetSmashMessage(string text, bool warn)
    {
        _smashMessage.Text = text;
        _smashMessage.AddThemeColorOverride("font_color", warn ? UiTheme.Bad : UiTheme.TextHi);
    }

    private bool CanStartSmash() =>
        _smashShown && !_smashBusy && !_smashSpinning && _smashSlot >= 0 && _smashSlot < Inv.Length && !Inv[_smashSlot].IsEmpty;

    private void RefreshSmashActions()
    {
        if (_smashStartBtn == null) return;
        _smashStartBtn.Disabled = !CanStartSmash();
        _smashStopBtn.Disabled = !_smashSpinning || _smashBusy;
        _smashCancelBtn.Disabled = _smashBusy;
    }
}
