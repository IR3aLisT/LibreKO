using System.Collections.Generic;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const int EquipViewTitleText = 44765;
    private const int EquipViewLevelText = 44766;
    private const int EquipViewAttackText = 44768;
    private const int EquipViewDefenceText = 44769;
    private const int EquipViewStrText = 44771;
    private const int EquipViewHpText = 44772;
    private const int EquipViewDexText = 44773;
    private const int EquipViewMpText = 44774;
    private const int EquipViewIntText = 44775;
    private const int EquipViewResistText = 44776;
    private const int EquipViewFireText = 44777;
    private const int EquipViewIceText = 44778;
    private const int EquipViewLightningText = 44779;
    private const int EquipViewMagicText = 44780;
    private const int EquipViewCurseText = 44781;
    private const int EquipViewPoisonText = 44782;
    private const int EquipViewNoteText = 44783;
    private const int EquipViewNotHereText = 44785;
    private const int EquipViewNoItemText = 44787;
    private const int EquipViewSelfText = 44789;

    private const int EquipViewWidth = 540;
    private const int EquipViewCostumeDrop = 55;
    private const float EquipViewCell = 44f;
    private const int EquipViewCellGap = 8;
    private const float EquipViewIdentityWidth = 170f;
    private const float EquipViewIconInset = 3f;
    private const string EquipViewNoValue = "-";

    private CanvasLayer _equipViewLayer = null!;
    private HudWindow _equipViewPanel = null!;
    private Label _equipViewName = null!, _equipViewClass = null!, _equipViewNation = null!, _equipViewStatus = null!;
    private readonly Dictionary<int, TextureRect> _equipViewIcons = new();
    private readonly Dictionary<int, ItemSlot> _equipViewItems = new();
    private readonly Dictionary<string, Label> _equipViewValues = new();
    private bool _equipViewShown;
    private LibreKO.Plugins.GameEquipView? _equipViewModel;
    private readonly List<LibreKO.Plugins.GameEquipValue> _equipViewModelValues = new();
    private string _equipViewPending = "";

    private void EquipViewInit()
    {
        _equipViewLayer = new CanvasLayer { Layer = 78 };
        AddChild(_equipViewLayer);

        _equipViewPanel = new HudWindow("equipview", ItemData.Text(EquipViewTitleText, "Equipment/Ability"),
            new Vector2(360, 120), EquipViewWidth) { Visible = false };
        _equipViewPanel.Closed += CloseEquipView;
        _equipViewLayer.AddChild(_equipViewPanel);

        var root = _equipViewPanel.Body;
        root.AddThemeConstantOverride("separation", 10);

        var top = new HBoxContainer();
        top.AddThemeConstantOverride("separation", 18);
        root.AddChild(top);

        var identity = new VBoxContainer { CustomMinimumSize = new Vector2(EquipViewIdentityWidth, 0) };
        identity.AddThemeConstantOverride("separation", 4);
        _equipViewNation = UiTheme.Text("", 12, UiTheme.TextLo);
        _equipViewName = UiTheme.Text("", 15, UiTheme.TextHi);
        _equipViewName.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _equipViewClass = UiTheme.Text("", 12, UiTheme.TextLo);
        identity.AddChild(_equipViewNation);
        identity.AddChild(_equipViewName);
        identity.AddChild(_equipViewClass);
        top.AddChild(identity);

        var gear = EquipViewGrid();
        for (int slot = 0; slot < InventoryConstants.SlotMax; slot++) gear.AddChild(EquipViewCellFor(slot));
        top.AddChild(gear);

        var costume = EquipViewGrid();
        foreach (int slot in EquipViewLayout.CostumeCells) costume.AddChild(EquipViewCellFor(slot));
        var costumeColumn = new MarginContainer();
        costumeColumn.AddThemeConstantOverride("margin_top", EquipViewCostumeDrop);
        costumeColumn.AddChild(costume);
        top.AddChild(costumeColumn);

        var stats = new GridContainer { Columns = EquipViewLayout.Columns * 2 };
        stats.AddThemeConstantOverride("h_separation", 14);
        stats.AddThemeConstantOverride("v_separation", 4);
        EquipViewStatRow(stats, (EquipViewLevelText, "Level"), (EquipViewAttackText, "Attack"), (EquipViewDexText, "DEX"));
        EquipViewStatRow(stats, (EquipViewHpText, "HP"), (EquipViewDefenceText, "Defence"), (0, "MAG"));
        EquipViewStatRow(stats, (EquipViewMpText, "MP"), (EquipViewStrText, "STR"), (EquipViewIntText, "INT"));
        EquipViewStatRow(stats, (0, "SP"), (0, "STA"), (-1, ""));
        root.AddChild(stats);

        root.AddChild(UiTheme.SectionTitle(ItemData.Text(EquipViewResistText, "Resistance")));
        var resist = new GridContainer { Columns = EquipViewLayout.Columns * 2 };
        resist.AddThemeConstantOverride("h_separation", 14);
        resist.AddThemeConstantOverride("v_separation", 4);
        EquipViewStatRow(resist, (EquipViewFireText, "Fire"), (EquipViewIceText, "Ice"), (EquipViewLightningText, "Lightning"));
        EquipViewStatRow(resist, (EquipViewMagicText, "Magic"), (EquipViewCurseText, "Curse"), (EquipViewPoisonText, "Poison"));
        root.AddChild(resist);

        var note = UiTheme.Text(ItemData.Text(EquipViewNoteText, "The corresponding ability value is the value with items and buffs applied."),
            11, UiTheme.TextDim);
        note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        root.AddChild(note);

        _equipViewStatus = UiTheme.Text("", 12, UiTheme.TextLo);
        root.AddChild(_equipViewStatus);

        Net.I.EquipmentViewEvent += OnEquipmentView;
        AttachEquipViewPlugin();
    }

    private static GridContainer EquipViewGrid()
    {
        var grid = new GridContainer { Columns = EquipViewLayout.Columns };
        grid.AddThemeConstantOverride("h_separation", EquipViewCellGap);
        grid.AddThemeConstantOverride("v_separation", EquipViewCellGap);
        return grid;
    }

    private Control EquipViewCellFor(int slot)
    {
        var cell = new Panel { CustomMinimumSize = new Vector2(EquipViewCell, EquipViewCell) };
        cell.AddThemeStyleboxOverride("panel", UiTheme.Slot());
        var icon = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        icon.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.KeepSize, (int)EquipViewIconInset);
        cell.AddChild(icon);
        cell.MouseEntered += () =>
        {
            if (_equipViewItems.TryGetValue(slot, out var item)) ShowItemTooltip(-1, item);
        };
        cell.MouseExited += HideItemTooltip;
        _equipViewIcons[slot] = icon;
        return cell;
    }

    private void EquipViewStatRow(GridContainer grid, params (int Text, string Fallback)[] cells)
    {
        foreach (var (text, fallback) in cells)
        {
            var key = UiTheme.Text(text > 0 ? ItemData.Text(text, fallback) : fallback, 12, UiTheme.TextLo);
            var value = UiTheme.Text("", 12, UiTheme.TextHi);
            value.CustomMinimumSize = new Vector2(72, 0);
            grid.AddChild(key);
            grid.AddChild(value);
            if (fallback.Length > 0) _equipViewValues[fallback] = value;
        }
    }

    private void PluginNotifyEquipView() => Callable.From(LibreKO.Plugins.PluginHost.Game.RaiseEquipView).CallDeferred();

    private void SetEquipViewValue(string key, string value)
    {
        int at = _equipViewModelValues.FindIndex(v => v.Key == key);
        var entry = new LibreKO.Plugins.GameEquipValue(key, EquipViewCaption(key), value);
        if (at >= 0) _equipViewModelValues[at] = entry;
        else _equipViewModelValues.Add(entry);
        if (_equipViewValues.TryGetValue(key, out var label)) label.Text = value;
    }

    private void EquipViewDispose()
    {
        Net.I.EquipmentViewEvent -= OnEquipmentView;
    }

    private void AttachEquipViewPlugin() => LibreKO.Plugins.PluginHost.Game.AttachEquipView(new PluginEquipViewBridge(this));

    private void RequestEquipmentView(string name)
    {
        _equipViewPending = name;
        ClearEquipView();
        _equipViewName.Text = name;
        _equipViewStatus.Text = "Requesting…";
        _equipViewStatus.AddThemeColorOverride("font_color", UiTheme.TextLo);
        _equipViewPanel.Visible = true;
        _equipViewShown = true;
        PluginNotifyEquipView();
        Net.I.SendEquipmentViewRequest(name);
    }

    private void CloseEquipView()
    {
        HideItemTooltip();
        _equipViewShown = false;
        _equipViewPanel.Visible = false;
        PluginNotifyEquipView();
    }

    private void ClearEquipView()
    {
        _equipViewItems.Clear();
        foreach (var icon in _equipViewIcons.Values)
        {
            icon.Texture = null;
            foreach (var child in icon.GetChildren())
                if (child is UpgradeBadge badge) badge.QueueFree();
        }
        foreach (var value in _equipViewValues.Values) value.Text = "";
        _equipViewName.Text = "";
        _equipViewClass.Text = "";
        _equipViewNation.Text = "";
        _equipViewModelValues.Clear();
        _equipViewModel = null;
    }

    private static string EquipViewCaption(string key) => key switch
    {
        "Level" => ItemData.Text(EquipViewLevelText, "Level"),
        "HP" => ItemData.Text(EquipViewHpText, "HP"),
        "MP" => ItemData.Text(EquipViewMpText, "MP"),
        "SP" => "SP",
        "Attack" => ItemData.Text(EquipViewAttackText, "Attack"),
        "Defence" => ItemData.Text(EquipViewDefenceText, "Defence"),
        "STR" => ItemData.Text(EquipViewStrText, "STR"),
        "STA" => "STA",
        "DEX" => ItemData.Text(EquipViewDexText, "DEX"),
        "MAG" => "MAG",
        "INT" => ItemData.Text(EquipViewIntText, "INT"),
        "Fire" => ItemData.Text(EquipViewFireText, "Fire"),
        "Ice" => ItemData.Text(EquipViewIceText, "Ice"),
        "Lightning" => ItemData.Text(EquipViewLightningText, "Lightning"),
        "Magic" => ItemData.Text(EquipViewMagicText, "Magic"),
        "Curse" => ItemData.Text(EquipViewCurseText, "Curse"),
        "Poison" => ItemData.Text(EquipViewPoisonText, "Poison"),
        _ => key,
    };

    private sealed class PluginEquipViewBridge : LibreKO.Plugins.IGameEquipView
    {
        private readonly World _w;

        public PluginEquipViewBridge(World w) => _w = w;

        public bool IsOpen => _w._equipViewShown;
        public LibreKO.Plugins.GameEquipView? View => _w._equipViewModel;
        public string Title => ItemData.Text(EquipViewTitleText, "Equipment/Ability");
        public string Note => ItemData.Text(EquipViewNoteText, "These values include items and buffs.");
        public string ResistTitle => ItemData.Text(EquipViewResistText, "Resistance");

        public void ShowTooltip(int slot)
        {
            if (_w._equipViewItems.TryGetValue(slot, out var item)) _w.ShowItemTooltip(-1, item);
        }

        public void HideTooltip() => _w.HideItemTooltip();
        public void Close() => _w.CloseEquipView();

        event System.Action? LibreKO.Plugins.IGameEquipView.Changed { add { } remove { } }
    }

    private void OnEquipmentView(Net.EquipmentViewResult result, Net.EquipmentView view)
    {
        if (!_equipViewShown) return;

        if (result != Net.EquipmentViewResult.Accepted)
        {
            ClearEquipView();
            _equipViewName.Text = _equipViewPending;
            _equipViewStatus.Text = result switch
            {
                Net.EquipmentViewResult.NotInSameRegion => ItemData.Text(EquipViewNotHereText, "The user does not exist in the same region."),
                Net.EquipmentViewResult.CannotChooseYourself => ItemData.Text(EquipViewSelfText, "You cannot choose yourself"),
                Net.EquipmentViewResult.NoViewEquipmentItem => ItemData.Text(EquipViewNoItemText, "There is not enough View Equipment Item."),
                _ => $"{_equipViewPending} could not be found.",
            };
            _equipViewStatus.AddThemeColorOverride("font_color", UiTheme.Bad);
            return;
        }

        ClearEquipView();
        _equipViewStatus.Text = "";
        _equipViewName.Text = view.Name;
        _equipViewName.AddThemeColorOverride("font_color", NationColor(view.Nation));
        _equipViewClass.Text = CharacterClassCatalog.DisplayName(view.Class);
        _equipViewNation.Text = NationName(view.Nation);

        foreach (var w in view.Worn)
        {
            if (!_equipViewIcons.TryGetValue(w.Slot, out var icon)) continue;
            _equipViewItems[w.Slot] = new ItemSlot { ItemId = w.ItemId, Count = 1, Durability = w.Durability };
            icon.Texture = ItemData.Icon(w.ItemId);
            UpgradeBadge.Show(icon, w.ItemId);
        }

        SetEquipViewValue("Level", EquipViewLayout.LevelLine(view.Level, view.RebirthLevel));
        SetEquipViewValue("HP", view.MaxHp.ToString("N0"));
        SetEquipViewValue("MP", view.MaxMp.ToString("N0"));
        SetEquipViewValue("SP", EquipViewNoValue);
        SetEquipViewValue("Attack", view.Attack.ToString());
        SetEquipViewValue("Defence", view.Defence.ToString());
        SetEquipViewValue("STR", EquipViewLayout.StatLine(view.Str, view.StrBonus));
        SetEquipViewValue("STA", EquipViewLayout.StatLine(view.Sta, view.StaBonus));
        SetEquipViewValue("DEX", EquipViewLayout.StatLine(view.Dex, view.DexBonus));
        SetEquipViewValue("MAG", EquipViewLayout.StatLine(view.Magic, view.MagicBonus));
        SetEquipViewValue("INT", EquipViewLayout.StatLine(view.Intel, view.IntelBonus));
        SetEquipViewValue("Fire", view.FireR.ToString());
        SetEquipViewValue("Ice", view.IceR.ToString());
        SetEquipViewValue("Lightning", view.LightningR.ToString());
        SetEquipViewValue("Magic", view.MagicR.ToString());
        SetEquipViewValue("Curse", view.CurseR.ToString());
        SetEquipViewValue("Poison", view.PoisonR.ToString());
        var items = new Dictionary<int, Texture2D?>();
        foreach (var (slot, item) in _equipViewItems) items[slot] = ItemData.Icon(item.ItemId);
        _equipViewModel = new LibreKO.Plugins.GameEquipView(view.Name, NationColor(view.Nation),
            CharacterClassCatalog.DisplayName(view.Class), view.Nation, _equipViewModelValues.ToArray(), items, "");
        PluginNotifyEquipView();
    }
}
