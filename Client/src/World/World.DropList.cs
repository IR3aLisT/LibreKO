using System.Collections.Generic;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const double DropListCacheSeconds = 180;
    private const float DropListIconSize = 24f;
    private const float DropListMaxHeight = 220f;
    private const float DropListButtonSize = 22f;
    private const string DropListIcon = "system/bag";

    private Button _dropListBtn = null!;
    private PanelContainer _dropPanel = null!;
    private Label _dropHeader = null!;
    private VBoxContainer _dropRows = null!;
    private readonly Dictionary<int, (double At, DropListReply List)> _dropCache = new();
    private int _dropTargetId = -1;
    private int _dropPendingId = -1;
    private DropListReply? _dropShown;
    private Label _dropLegend = null!;
    private const string DropLegendText = "Ticked items are picked up by auto-loot. Click a tick to change it for that item.";
    private const string DropLegendOffText = "Auto-loot filter is off: everything is picked up. Click a tick to set a rule.";
    private const string DropRulesFullText = "You already have the most item rules. Remove one first.";
    private static readonly Color DropRuleAlwaysColour = new("ffd75a");
    private static readonly Color DropRuleNeverColour = new("ff6a6a");
    private static readonly string[] DropTickStyleStates = { "normal", "hover", "pressed", "hover_pressed", "focus", "disabled" };

    private void BuildDropListUi(VBoxContainer box)
    {
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        box.AddChild(row);
        _dropListBtn = UiTheme.IconButton(UiIcons.Get(DropListIcon), "Drop list");
        _dropListBtn.CustomMinimumSize = new Vector2(DropListButtonSize, DropListButtonSize);
        _dropListBtn.Visible = false;
        _dropListBtn.Pressed += ToggleDropList;
        row.AddChild(_dropListBtn);

        _dropPanel = UiTheme.Section();
        _dropPanel.Visible = false;
        box.AddChild(_dropPanel);
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 4);
        _dropPanel.AddChild(body);
        _dropHeader = UiTheme.Text("", 12, UiTheme.TextHi);
        _dropHeader.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        body.AddChild(_dropHeader);
        _dropLegend = UiTheme.Text("", 11, UiTheme.TextLo);
        _dropLegend.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        body.AddChild(_dropLegend);
        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            CustomMinimumSize = new Vector2(0f, 0f),
        };
        body.AddChild(scroll);
        _dropRows = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _dropRows.AddThemeConstantOverride("separation", 2);
        scroll.AddChild(_dropRows);
        _dropRows.Resized += () => scroll.CustomMinimumSize = new Vector2(0f, Mathf.Min(_dropRows.Size.Y, DropListMaxHeight));
    }

    private void DropListInit() => Net.I.DropListEvent += OnDropList;

    private void DropListDispose() => Net.I.DropListEvent -= OnDropList;

    private void SyncDropListButton(int shownId)
    {
        if (_dropListBtn == null || !IsInstanceValid(_dropListBtn)) return;
        bool monster = shownId >= 0 && _ents.TryGetValue(shownId, out var e) && e.IsMonster && e.Attackable;
        if (_dropListBtn.Visible != monster) _dropListBtn.Visible = monster;
        if (shownId != _dropTargetId && _dropPanel.Visible) _dropPanel.Visible = false;
    }

    private void ToggleDropList()
    {
        if (_dropPanel.Visible) { _dropPanel.Visible = false; return; }
        int target = _targetHudId;
        if (target < 0 || !_ents.TryGetValue(target, out var e)) return;
        _dropTargetId = target;
        _dropPanel.Visible = true;
        if (_dropCache.TryGetValue(e.NpcId, out var cached) && Now() - cached.At < DropListCacheSeconds)
        {
            ShowDropList(cached.List);
            return;
        }
        _dropHeader.Text = "Loading...";
        ClearDropRows();
        _dropPendingId = target;
        Net.I.SendDropListRequest(target);
    }

    private DropListReply? CachedDrops(int targetId) =>
        targetId >= 0 && _ents.TryGetValue(targetId, out var e) && _dropCache.TryGetValue(e.NpcId, out var cached)
        && Now() - cached.At < DropListCacheSeconds ? cached.List with { TargetId = targetId } : null;

    private void RequestDropsFor(int targetId)
    {
        if (targetId < 0 || !_ents.TryGetValue(targetId, out var e) || !e.IsMonster) return;
        if (CachedDrops(targetId) != null) { _pluginBridge?.RaiseDrops(targetId); return; }
        Net.I.SendDropListRequest(targetId);
    }

    private void OnDropList(DropListReply list)
    {
        if (_ents.TryGetValue(list.TargetId, out var e) && list.Shown)
            _dropCache[e.NpcId] = (Now(), list);
        _pluginBridge?.RaiseDrops(list.TargetId);
        if (list.TargetId != _dropPendingId) return;
        _dropPendingId = -1;
        if (_dropPanel.Visible && _dropTargetId == list.TargetId) ShowDropList(list);
    }

    private void RefreshDropListTicks()
    {
        if (_dropShown is { } shown && _dropPanel != null && IsInstanceValid(_dropPanel) && _dropPanel.Visible)
            ShowDropList(shown);
    }

    private void ShowDropList(DropListReply list)
    {
        ClearDropRows();
        _dropShown = list;
        _dropLegend.Text = CurrentLootFilter().Enabled ? DropLegendText : DropLegendOffText;
        _dropLegend.Visible = list.Shown && list.Items.Count > 0;
        if (!list.Shown)
        {
            _dropHeader.Text = "No drop list for this target.";
            return;
        }
        string coins = list.CoinMax > 0 ? $"Coins {list.CoinMin:n0}-{list.CoinMax:n0}" : "No coins";
        _dropHeader.Text = list.Experience > 0 ? $"{coins}  ·  EXP {list.Experience:n0}" : coins;
        if (list.Items.Count == 0)
        {
            _dropRows.AddChild(UiTheme.Text("No items.", 12, UiTheme.TextLo));
            return;
        }
        foreach (var line in list.Items) _dropRows.AddChild(DropRow(line));
    }

    private Control DropRow(DropLine line)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        row.AddChild(DropLootTick(line.ItemId));
        var icon = new TextureRect
        {
            Texture = ItemData.Icon(line.ItemId),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(DropListIconSize, DropListIconSize),
        };
        row.AddChild(icon);
        var name = UiTheme.Text(ItemData.DisplayName(line.ItemId), 12, UiTheme.TextHi);
        name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        name.ClipText = true;
        row.AddChild(name);
        row.AddChild(UiTheme.Text(BandText(line.Band), 11, BandColour(line.Band), HorizontalAlignment.Right));
        row.MouseFilter = Control.MouseFilterEnum.Stop;
        row.MouseEntered += () => ShowItemTooltip(-1, TooltipItem(line.ItemId));
        row.MouseExited += HideItemTooltip;
        return row;
    }

    private CheckBox DropLootTick(int itemId)
    {
        var filter = CurrentLootFilter();
        var group = LootFilter.GroupOf(itemId, Net.GoldItemId, ItemData.Get(itemId));
        int rarity = ItemData.ExtFor(itemId)?.MagicOrRare ?? ItemData.Rarity.Regular;
        int price = ItemData.SellPrice(itemId);
        var reason = filter.Verdict(itemId, group, rarity, price);
        bool taken = reason is LootReason.Taken or LootReason.AlwaysRule;
        var tick = new CheckBox { ButtonPressed = taken, FocusMode = Control.FocusModeEnum.None };
        foreach (var state in DropTickStyleStates) tick.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        tick.TooltipText = DropLootReasonText(reason, group, rarity, price);
        if (reason == LootReason.AlwaysRule) tick.Modulate = DropRuleAlwaysColour;
        else if (reason == LootReason.NeverRule) tick.Modulate = DropRuleNeverColour;
        tick.Toggled += _ =>
        {
            if (!filter.Flip(itemId, group, rarity, price)) CombatNotice(DropRulesFullText);
            SaveLootFilter();
            Callable.From(RefreshDropListTicks).CallDeferred();
        };
        return tick;
    }

    private static string DropLootReasonText(LootReason reason, LootGroup group, int rarity, int price) => reason switch
    {
        LootReason.Taken => $"Auto-loot picks this up ({LootGroupName(group)}).",
        LootReason.AlwaysRule => "Auto-loot picks this up: your rule.",
        LootReason.NeverRule => "Auto-loot leaves this: your rule.",
        LootReason.GroupOff => $"Auto-loot leaves this: the {LootGroupName(group)} group is off.",
        LootReason.GradeOff => $"Auto-loot leaves this: {LootGradeName(rarity)} items are off.",
        _ => $"Auto-loot leaves this: it sells for {price:n0}.",
    };

    private void ClearDropRows()
    {
        foreach (Node child in _dropRows.GetChildren()) child.QueueFree();
    }

    private static string BandText(DropBand band) => band switch
    {
        DropBand.Common => "Common",
        DropBand.Uncommon => "Uncommon",
        DropBand.Rare => "Rare",
        _ => "Very rare",
    };

    private static Color BandColour(DropBand band) => band switch
    {
        DropBand.Common => UiTheme.TextLo,
        DropBand.Uncommon => UiTheme.Good,
        DropBand.Rare => UiTheme.Premium,
        _ => UiTheme.GoldVivid,
    };
}
