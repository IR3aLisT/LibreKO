using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using Godot;
using LibreKO.Domain;

namespace LibreKO;

public partial class World
{
    private const int GenieOtherFlagsOffset = GenieExtraFlagsOffset + 1;
    private const int GenieTownHpCountOffset = GenieOtherFlagsOffset + 1;
    private const int GenieTownMpCountOffset = GenieTownHpCountOffset + sizeof(ushort);
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
        foreach (var rule in GenieTownReturn.Rules)
        {
            var count = rule.Option is GenieOther.HpPotions or GenieOther.MpPotions ? new SpinBox() : null;
            _genieOther[rule.Option] = GenieOtherRow(misc, ItemData.Text(rule.LabelText, rule.Label), count);
            if (rule.Option == GenieOther.HpPotions) _genieTownHpCount = count!;
            if (rule.Option == GenieOther.MpPotions) _genieTownMpCount = count!;
        }
        _genieOther[GenieOther.AutoSpirit] = GenieOtherRow(misc,
            ItemData.Text(GenieTownReturn.AutoSpiritText, GenieTownReturn.AutoSpiritLabel), null);
    }

    private static CheckButton GenieOtherRow(Node parent, string text, SpinBox? count)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        parent.AddChild(row);
        var toggle = GenieOption(row, string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries)), false);
        toggle.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        if (count == null) return toggle;
        count.MinValue = GenieTownReturn.MinCount;
        count.MaxValue = GenieTownReturn.MaxCount;
        count.Step = 1;
        count.Value = GenieTownReturn.DefaultCount;
        row.AddChild(count);
        return toggle;
    }

    private GenieOther GenieOtherFlags()
    {
        var flags = GenieOther.None;
        foreach (var (option, toggle) in _genieOther)
            if (toggle.ButtonPressed) flags |= option;
        return flags;
    }

    private void WriteGenieTownOptions(byte[] bytes)
    {
        bytes[GenieOtherFlagsOffset] = (byte)GenieOtherFlags();
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(GenieTownHpCountOffset, sizeof(ushort)), (ushort)_genieTownHpCount.Value);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(GenieTownMpCountOffset, sizeof(ushort)), (ushort)_genieTownMpCount.Value);
    }

    private void ReadGenieTownOptions(byte[] bytes)
    {
        var flags = (GenieOther)bytes[GenieOtherFlagsOffset];
        foreach (var (option, toggle) in _genieOther)
            toggle.ButtonPressed = (flags & option) != 0;
        _genieTownHpCount.Value = GenieTownReturn.StoredOrDefault(
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(GenieTownHpCountOffset, sizeof(ushort))));
        _genieTownMpCount.Value = GenieTownReturn.StoredOrDefault(
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(GenieTownMpCountOffset, sizeof(ushort))));
    }

    private void ResetGenieTownWatch()
    {
        _genieSawParty = false;
        _genieSpiritAsked = false;
        _genieLowHpWarned = false;
    }

    private bool GenieTownTick()
    {
        var enabled = GenieOtherFlags();
        GenieAutoSpirit(enabled);
        if (InParty) _genieSawParty = true;
        int arrowId = SkillData.Get(_selfClass * ClassSkillScale + BasicRangedSkillSuffix)?.ConsumedItem ?? 0;
        var state = new GenieTownReturn.State(
            GeniePotionStock(HealTarget.Hp), (int)_genieTownHpCount.Value,
            GeniePotionStock(HealTarget.Mp), (int)_genieTownMpCount.Value,
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
