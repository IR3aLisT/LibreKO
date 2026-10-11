using System.Collections.Generic;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const int LootRuleAlways = 1;
    private const int LootRuleNever = 2;
    private const int LootRuleClear = 3;
    private const int LootPriceStep = 100;
    private const int LootPriceMax = 100_000_000;
    private const float LootRuleNameWidth = 200f;
    private const float LootRuleKindWidth = 56f;
    private static readonly string[] LootToggleColourStates =
        { "font_color", "font_pressed_color", "font_hover_color", "font_hover_pressed_color", "font_focus_color" };

    private static readonly (int Rarity, string Name, Color Colour)[] LootGradeRows =
    {
        (ItemData.Rarity.Regular, "Normal", new Color("ffffff")),
        (ItemData.Rarity.Magic, "Magic", new Color("0080ff")),
        (ItemData.Rarity.Rare, "Rare", new Color("ffff00")),
        (ItemData.Rarity.Craft, "Craft", new Color("00ff00")),
        (ItemData.Rarity.Unique, "Unique", new Color("dcc77c")),
        (ItemData.Rarity.Upgrade, "Upgrade", new Color("ff80ff")),
    };

    private static readonly (LootGroup Group, string Name)[] LootGroupRows =
    {
        (LootGroup.Coins, "Coins"),
        (LootGroup.Equipment, "Equipment"),
        (LootGroup.Quest, "Quest items"),
        (LootGroup.Other, "Other items (potions, scrolls, materials)"),
    };

    private LootFilter _lootFilter = new();
    private string _lootFilterOwner = "";
    private HudWindow? _lootFilterWindow;
    private VBoxContainer _lootRuleList = null!;
    private PopupMenu? _lootRuleMenu;
    private int _lootRuleItem;

    private LootFilter CurrentLootFilter()
    {
        string owner = Net.I.LastEnter.Name ?? "";
        if (owner != _lootFilterOwner)
        {
            _lootFilterOwner = owner;
            _lootFilter = owner.Length > 0 ? Config.LoadLootFilter(owner) : new LootFilter();
        }
        return _lootFilter;
    }

    private void SaveLootFilter()
    {
        if (_lootFilterOwner.Length > 0) Config.SaveLootFilter(_lootFilterOwner, _lootFilter);
        RefreshDropListTicks();
    }

    private static string LootGroupName(LootGroup group)
    {
        foreach (var (g, name) in LootGroupRows)
            if (g == group) return name;
        return group.ToString();
    }

    private static string LootGradeName(int rarity)
    {
        foreach (var (r, name, _) in LootGradeRows)
            if (r == rarity) return name;
        return rarity.ToString();
    }

    private bool LootFilterTakes(int itemId) =>
        CurrentLootFilter().Takes(itemId, LootFilter.GroupOf(itemId, Net.GoldItemId, ItemData.Get(itemId)),
            ItemData.ExtFor(itemId)?.MagicOrRare ?? ItemData.Rarity.Regular, ItemData.SellPrice(itemId));

    private void OpenLootFilter()
    {
        if (_lootFilterWindow == null || !IsInstanceValid(_lootFilterWindow)) BuildLootFilter();
        RefreshLootRules();
        _lootFilterWindow!.Visible = true;
    }

    private void BuildLootFilter()
    {
        var filter = CurrentLootFilter();
        var layer = new CanvasLayer { Layer = 74 };
        AddChild(layer);
        _lootFilterWindow = new HudWindow("loot_filter", "Loot Filter") { Visible = false };
        _lootFilterWindow.Closed += () => _lootFilterWindow.Visible = false;
        layer.AddChild(_lootFilterWindow);
        var body = _lootFilterWindow.Body;
        body.AddThemeConstantOverride("separation", 6);

        body.AddChild(LootToggle("Filter what auto-loot picks up", filter.Enabled, on => filter.Enabled = on));
        body.AddChild(UiTheme.SectionTitle("Item groups"));
        foreach (var (group, name) in LootGroupRows)
        {
            var g = group;
            body.AddChild(LootToggle(name, filter.Groups.Contains(g), on => { if (on) filter.Groups.Add(g); else filter.Groups.Remove(g); }));
        }

        body.AddChild(UiTheme.SectionTitle("Equipment grades"));
        var grades = new GridContainer { Columns = 3 };
        grades.AddThemeConstantOverride("h_separation", 12);
        foreach (var (rarity, name, colour) in LootGradeRows)
        {
            int r = rarity;
            var toggle = LootToggle(name, filter.Grades.Contains(r), on => { if (on) filter.Grades.Add(r); else filter.Grades.Remove(r); });
            foreach (var state in LootToggleColourStates) toggle.AddThemeColorOverride(state, colour);
            grades.AddChild(toggle);
        }
        body.AddChild(grades);

        var priceRow = new HBoxContainer();
        priceRow.AddThemeConstantOverride("separation", 8);
        var priceLabel = HudStyle.Label(13);
        priceLabel.Text = "Minimum sell price";
        priceRow.AddChild(priceLabel);
        var price = new SpinBox
        {
            MinValue = 0, MaxValue = LootPriceMax, Step = LootPriceStep,
            Value = filter.MinSellPrice, Suffix = "coins",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        price.ValueChanged += v => { filter.MinSellPrice = (int)v; SaveLootFilter(); };
        priceRow.AddChild(price);
        body.AddChild(priceRow);

        body.AddChild(UiTheme.SectionTitle("Item rules (right-click an item in a loot box)"));
        _lootRuleList = new VBoxContainer();
        _lootRuleList.AddThemeConstantOverride("separation", 2);
        body.AddChild(_lootRuleList);
    }

    private CheckBox LootToggle(string text, bool pressed, System.Action<bool> changed)
    {
        var box = new CheckBox { Text = text, ButtonPressed = pressed, FocusMode = Control.FocusModeEnum.None };
        box.AddThemeFontSizeOverride("font_size", 13);
        foreach (var state in LootToggleColourStates) box.AddThemeColorOverride(state, UiTheme.TextHi);
        box.Toggled += on => { changed(on); SaveLootFilter(); };
        return box;
    }

    private void RefreshLootRules()
    {
        foreach (var child in _lootRuleList.GetChildren()) child.QueueFree();
        var filter = CurrentLootFilter();
        int shown = 0;
        foreach (var (ids, take) in new[] { (filter.Always, true), (filter.Never, false) })
            foreach (int id in new List<int>(ids))
            {
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 8);
                var name = HudStyle.Label(12);
                name.Text = ItemData.DisplayName(id);
                name.CustomMinimumSize = new Vector2(LootRuleNameWidth, 0);
                name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                name.AddThemeColorOverride("font_color", ItemGrade.Tint(id));
                row.AddChild(name);
                var rule = HudStyle.Label(12);
                rule.Text = take ? "always" : "never";
                rule.CustomMinimumSize = new Vector2(LootRuleKindWidth, 0);
                rule.AddThemeColorOverride("font_color", take ? UiTheme.Good : UiTheme.Bad);
                row.AddChild(rule);
                int itemId = id;
                var remove = UiTheme.SmallButton("Remove", "");
                remove.Pressed += () => { filter.SetRule(itemId, null); SaveLootFilter(); RefreshLootRules(); };
                row.AddChild(remove);
                _lootRuleList.AddChild(row);
                shown++;
            }
        if (shown == 0)
        {
            var none = HudStyle.Label(12);
            none.Text = "No item rules yet.";
            _lootRuleList.AddChild(none);
        }
    }

    private void ShowLootRuleMenu(int itemId)
    {
        if (itemId == Net.GoldItemId) return;
        if (_lootRuleMenu == null || !IsInstanceValid(_lootRuleMenu))
        {
            _lootRuleMenu = new PopupMenu();
            _lootRuleMenu.IdPressed += OnLootRuleMenu;
            AddChild(_lootRuleMenu);
        }
        _lootRuleItem = itemId;
        _lootRuleMenu.Clear();
        _lootRuleMenu.AddItem("Always auto-loot this item", LootRuleAlways);
        _lootRuleMenu.AddItem("Never auto-loot this item", LootRuleNever);
        _lootRuleMenu.AddItem("Clear the rule for this item", LootRuleClear);
        PopupAtCursor(_lootRuleMenu, GetViewport().GetMousePosition());
    }

    private void OnLootRuleMenu(long id)
    {
        CurrentLootFilter().SetRule(_lootRuleItem, id switch
        {
            LootRuleAlways => true,
            LootRuleNever => false,
            _ => null,
        });
        SaveLootFilter();
        if (_lootFilterWindow != null && IsInstanceValid(_lootFilterWindow) && _lootFilterWindow.Visible) RefreshLootRules();
    }
}
