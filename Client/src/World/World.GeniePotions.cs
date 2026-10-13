using System.Collections.Generic;
using Godot;

namespace LibreKO;

public partial class World
{
    private const int GeniePetTarget = -1;
    private int _genieHpItem, _genieMpItem, _geniePetItem;
    private Button _genieHpSelect = null!, _genieMpSelect = null!, _geniePetSelect = null!;
    private PopupMenu _geniePotionPicker = null!;
    private int _geniePotionTarget;
    private double _geniePotionUiAt;

    private static int GeniePotionHealOf(int target) => target == GeniePetTarget ? HealTarget.Hp : target;

    private void BuildGeniePotionSelector(VBoxContainer parent, int target)
    {
        var button = new Button { Text = "Automatic (strongest)",
            CustomMinimumSize = new Vector2(210, 30), ExpandIcon = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            FocusMode = Control.FocusModeEnum.None };
        button.AddThemeConstantOverride("icon_max_width", 24);
        parent.AddChild(button);
        if (target == HealTarget.Hp) _genieHpSelect = button;
        else if (target == HealTarget.Mp) _genieMpSelect = button;
        else _geniePetSelect = button;
        button.Pressed += () => OpenGeniePotionPicker(target);
        if (_geniePotionPicker != null) return;
        _geniePotionPicker = new PopupMenu();
        _genieLayer.AddChild(_geniePotionPicker);
        _geniePotionPicker.IdPressed += id =>
        {
            SetGeniePotionItem(_geniePotionTarget, (int)id);
            RefreshGeniePotionSelectors();
        };
    }

    private int GeniePotionItem(int target) =>
        target == HealTarget.Hp ? _genieHpItem : target == HealTarget.Mp ? _genieMpItem : _geniePetItem;

    private void SetGeniePotionItem(int target, int id)
    {
        if (target == HealTarget.Hp) _genieHpItem = id;
        else if (target == HealTarget.Mp) _genieMpItem = id;
        else _geniePetItem = id;
        PluginNotifyGenie();
    }

    private Button GeniePotionButton(int target) =>
        target == HealTarget.Hp ? _genieHpSelect : target == HealTarget.Mp ? _genieMpSelect : _geniePetSelect;

    private void OpenGeniePotionPicker(int target)
    {
        _geniePotionTarget = target;
        _geniePotionPicker.Clear();
        _geniePotionPicker.AddItem("Automatic (strongest)", 0);
        foreach (int id in GeniePotionChoices(target))
            _geniePotionPicker.AddIconItem(ItemData.Icon(id), $"{ItemData.DisplayName(id)} ({CountInBackpack(id)})", id);
        _geniePotionPicker.Position = (Vector2I)GeniePotionButton(target).GetGlobalRect().End;
        _geniePotionPicker.Popup();
    }

    private List<int> GeniePotionChoices(int target)
    {
        var choices = new List<int>();
        var seen = new HashSet<int>();
        int heal = GeniePotionHealOf(target);
        for (int i = GridStart; i < Inv.Length; i++)
        {
            if (i >= InventoryConstants.CospreStart && i < InventoryConstants.MagicBagStart) continue;
            int id = Inv[i].ItemId;
            if (id <= 0 || !seen.Add(id) || ItemData.PotionHeal(id, heal) <= 0 || CountInBackpack(id) <= 0) continue;
            choices.Add(id);
        }
        return choices;
    }

    private void RefreshGeniePotionSelectors()
    {
        RefreshGeniePotionSelector(_genieHpSelect, _genieHpItem);
        RefreshGeniePotionSelector(_genieMpSelect, _genieMpItem);
        RefreshGeniePotionSelector(_geniePetSelect, _geniePetItem);
    }

    private void RefreshGeniePotionSelector(Button button, int id)
    {
        button.Icon = id > 0 ? ItemData.Icon(id) : null;
        button.Text = id > 0 ? $"{ItemData.DisplayName(id)} ({CountInBackpack(id)})" : "Automatic (strongest)";
        button.TooltipText = id > 0
            ? $"{ItemData.DisplayName(id)}\nUses only this potion. Stops when it runs out."
            : "Automatically chooses the strongest available potion.";
    }

    private int GeniePotionInUse(int target)
    {
        int selected = GeniePotionItem(target);
        return selected == 0 ? BestPotion(GeniePotionHealOf(target)) : selected;
    }

    private void UseGeniePotion(int target)
    {
        int id = GeniePotionInUse(target);
        if (id <= 0 || CountInBackpack(id) <= 0 || ItemData.PotionHeal(id, target) <= 0
            || !MaestroPotions.CanUse(id, Sheet.Gold)) return;
        UseHotItem(id);
    }
}
