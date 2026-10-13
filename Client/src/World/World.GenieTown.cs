using System;
using System.Collections.Generic;
using Godot;
using LibreKO.Domain;

namespace LibreKO;

public partial class World
{
    private const int GenieOtherTitleText = 31033;
    private const int RogueFamily = 2;
    private const int BasicRangedSkillSuffix = 3;
    private const int ClassSkillScale = 1000;
    private const int SpiritOfGenieItem = 810378000;

    private readonly Dictionary<GenieOther, CheckButton> _genieOther = new();
    private SpinBox _genieTownHpCount = null!, _genieTownMpCount = null!;
    private bool _genieSawParty;
    private bool _genieSpiritAsked;
    private bool _genieLowHpWarned;

    private void BuildGenieTownOptions(VBoxContainer misc)
    {
        misc.AddChild(UiTheme.SectionTitle(ItemData.Text(GenieOtherTitleText, "Set Others")));
        foreach (var other in GenieTownReturn.OtherRows)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            misc.AddChild(row);
            var option = other.Option;
            var toggle = GenieOption(row, GenieOtherLabel(other), on => _genie.Set(option, on));
            toggle.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _genieOther[option] = toggle;
            if (option == GenieOther.HpPotions)
                _genieTownHpCount = GenieCount(row, v => _genie.TownHpCount = v);
            else if (option == GenieOther.MpPotions)
                _genieTownMpCount = GenieCount(row, v => _genie.TownMpCount = v);
        }
    }

    private static string GenieOtherLabel(GenieTownReturn.OtherRow row)
    {
        string text = row.Text > 0 ? ItemData.Text(row.Text, row.Label) : row.Label;
        return string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private SpinBox GenieCount(Node row, Action<int> write)
    {
        var count = new SpinBox { MinValue = GenieTownReturn.MinCount, MaxValue = GenieTownReturn.MaxCount, Step = 1 };
        count.ValueChanged += value =>
        {
            if (_genieSyncing) return;
            write((int)value);
            GenieSetupEdited();
        };
        row.AddChild(count);
        return count;
    }

    private void SyncGenieTownControls()
    {
        foreach (var (option, toggle) in _genieOther) toggle.SetPressedNoSignal(_genie.Has(option));
        _genieTownHpCount.SetValueNoSignal(_genie.TownHpCount);
        _genieTownMpCount.SetValueNoSignal(_genie.TownMpCount);
    }

    private void ResetGenieTownWatch()
    {
        _genieSawParty = false;
        _genieSpiritAsked = false;
        _genieLowHpWarned = false;
    }

    private bool GenieTownTick()
    {
        var enabled = _genie.Other;
        GenieAutoSpirit(enabled);
        if (InParty) _genieSawParty = true;
        int arrowId = SkillData.Get(_selfClass * ClassSkillScale + BasicRangedSkillSuffix)?.ConsumedItem ?? 0;
        var state = new GenieTownReturn.State(
            GeniePotionStock(HealTarget.Hp), _genie.TownHpCount,
            GeniePotionStock(HealTarget.Mp), _genie.TownMpCount,
            NeedsArrows: arrowId > 0 && CharacterClassCatalog.Family(_selfClass) == RogueFamily && HoldsBow(),
            Arrows: arrowId > 0 ? CountInBackpack(arrowId) : 0,
            WasInParty: _genieSawParty, InParty: InParty, PartyOthersAllDead: PartyOthersAllDead(),
            WeaponBroken: WeaponBroken());
        var triggered = GenieTownReturn.Triggered(enabled, state);
        if (triggered == GenieOther.None) return false;
        if (!GenieTownReturn.CanMoveToTown(Vitals.Hp, Vitals.MaxHp))
        {
            if (!_genieLowHpWarned) CombatNotice(ItemData.Text(GenieTownReturn.LowHpText, GenieTownReturn.LowHpMessage));
            _genieLowHpWarned = true;
            return false;
        }
        foreach (var rule in GenieTownReturn.Rules)
            if (rule.Option == triggered) CombatNotice(ItemData.Text(rule.MessageText, rule.Message));
        StopAdvancedGenie();
        TownRecallPress();
        return true;
    }

    private void GenieAutoSpirit(GenieOther enabled)
    {
        bool hasSpirit = CountInBackpack(SpiritOfGenieItem) > 0;
        if (!GenieTownReturn.WantsSpirit(enabled, _geniePoints, hasSpirit))
        {
            if (_geniePoints != GenieTownReturn.AutoSpiritAtPoints) _genieSpiritAsked = false;
            return;
        }
        if (_genieSpiritAsked) return;
        _genieSpiritAsked = true;
        Net.I.SendGenieSystem(Net.GenieUseSpiritPotion);
    }

    private bool HoldsBow()
    {
        foreach (int slot in new[] { InventoryConstants.RightHand, InventoryConstants.LeftHand })
            if (!Inv[slot].IsEmpty
                && ItemData.Get(Inv[slot].ItemId)?.Kind is WeaponAnimation.Bow or WeaponAnimation.Crossbow or WeaponAnimation.LongBow)
                return true;
        return false;
    }

    private bool WeaponBroken()
    {
        foreach (int slot in new[] { InventoryConstants.RightHand, InventoryConstants.LeftHand })
            if (!Inv[slot].IsEmpty && Inv[slot].Durability <= 0 && ItemData.Get(Inv[slot].ItemId) is { } def
                && WeaponAnimation.IsWeapon(def.Kind))
                return true;
        return false;
    }

    private bool PartyOthersAllDead()
    {
        if (!InParty) return false;
        int others = 0;
        foreach (var member in PartyMembers)
        {
            if (member.CharId == _myId) continue;
            others++;
            if (member.Hp > 0) return false;
        }
        return others > 0;
    }

    private int GeniePotionStock(int target)
    {
        int selected = target == HealTarget.Hp ? _genieHpItem : _genieMpItem;
        if (selected > 0) return CountInBackpack(selected);
        int stock = 0;
        var counted = new HashSet<int>();
        for (int abs = GridStart; abs < Inv.Length; abs++)
        {
            int id = Inv[abs].ItemId;
            if (id <= 0 || !counted.Add(id) || ItemData.PotionHeal(id, target) <= 0) continue;
            stock += CountInBackpack(id);
        }
        return stock;
    }
}
