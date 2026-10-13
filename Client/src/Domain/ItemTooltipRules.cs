using System;
using System.Collections.Generic;

namespace LibreKO.Domain;

public static class ItemTooltipRules
{
    public enum GradeTint { Unique, ReverseUnique, Upgrade, Reverse }

    public enum NoteTint { Bind, Stones, Sealed, TradeDone, TradeOnce }

    public readonly record struct GradeLine(int TextId, GradeTint Tint);

    public readonly record struct Note(int TextId, NoteTint Tint, bool WithStones = false);

    public const int NoWornSlot = -1;

    private const int UpgradeLowText = 4562;
    private const int UpgradeMiddleText = 4563;
    private const int UpgradeHighText = 4564;
    private const int UpgradeHighReverseText = 4582;
    private const int ReverseText = 4583;
    private const int UniqueText = 4585;
    private const int UniqueReverseChangeText = 4561;
    private const int ReverseUniqueText = 4584;
    private const int UniqueLowText = 30310;
    private const int UniqueMiddleText = 30311;
    private const int UniqueHighText = 30312;
    private const int UniqueHighReverseText = 30313;
    private const int UniqueEventText = 30314;

    private const int GradeLow = 1;
    private const int GradeMiddle = 2;
    private const int GradeHigh = 3;
    private const int GradeHighReverse = 4;
    private const int GradeReverse = 5;
    private const int GradeMiddleAlt = 7;
    private const int GradeHighReverseAlt = 8;
    private const int GradeUniqueReverseChange = 4;
    private const int GradeUniqueLow = 31;
    private const int GradeUniqueMiddle = 32;
    private const int GradeUniqueHigh = 33;
    private const int GradeUniqueHighReverse = 34;
    private const int GradeReverseUnique = 35;
    private const int GradeUniqueMiddleAlt = 37;
    private const int GradeUniqueHighReverseAlt = 38;
    private const int GradeUniqueEvent = 255;

    private const int ExtIdSpan = 1000;
    private const int OrgIdSpan = 1_000_000_000;
    private const int ReverseChangeExtFloor = 10;

    public static GradeLine? Grade(int rarity, int gradeClass, int itemId, int extId)
    {
        switch (rarity)
        {
            case ItemData.Rarity.Upgrade or ItemData.Rarity.Reverse:
                return gradeClass switch
                {
                    GradeLow => new GradeLine(UpgradeLowText, GradeTint.Upgrade),
                    GradeMiddle or GradeMiddleAlt => new GradeLine(UpgradeMiddleText, GradeTint.Upgrade),
                    GradeHigh => new GradeLine(UpgradeHighText, GradeTint.Upgrade),
                    GradeHighReverse or GradeHighReverseAlt =>
                        new GradeLine(HighOrReverseChange(itemId, extId), GradeTint.Upgrade),
                    GradeReverse => new GradeLine(ReverseText, GradeTint.Reverse),
                    _ => null,
                };
            case ItemData.Rarity.Unique or ItemData.Rarity.ReverseUnique:
                return gradeClass switch
                {
                    GradeUniqueReverseChange => new GradeLine(UniqueReverseChangeText, GradeTint.Unique),
                    GradeUniqueLow => new GradeLine(UniqueLowText, GradeTint.Unique),
                    GradeUniqueMiddle or GradeUniqueMiddleAlt => new GradeLine(UniqueMiddleText, GradeTint.Unique),
                    GradeUniqueHigh => new GradeLine(UniqueHighText, GradeTint.Unique),
                    GradeUniqueHighReverse or GradeUniqueHighReverseAlt =>
                        new GradeLine(UniqueHighReverseText, GradeTint.Unique),
                    GradeReverseUnique => new GradeLine(ReverseUniqueText, GradeTint.ReverseUnique),
                    GradeUniqueEvent => new GradeLine(UniqueEventText, GradeTint.Unique),
                    _ => new GradeLine(UniqueText, GradeTint.Unique),
                };
            default:
                return null;
        }
    }

    private static int HighOrReverseChange(int itemId, int extId) =>
        extId / ExtIdSpan == (int)((uint)itemId / OrgIdSpan) && extId % ExtIdSpan > ReverseChangeExtFloor
            ? UpgradeHighReverseText
            : UpgradeHighText;

    public static IEnumerable<string> SplitGradeText(string text)
    {
        int open = text.IndexOf('(');
        if (open <= 0)
        {
            yield return text;
            yield break;
        }
        yield return text[..open].TrimEnd();
        yield return text[open..];
    }

    private const int BindOnEquipText = 19015;
    private const int BoundText = 19016;
    private const int UnbindStonesText = 4613;
    private const int SealedText = 15117;
    private const int OneTimeTradeDoneText = 15122;
    private const int OneTimeTradeText = 15123;
    public const int OneTimeTradeRace = 74;
    private const byte OneTimeTradeDoneState = 16;

    public static IEnumerable<Note> Notes(byte state, int race)
    {
        if (state == (byte)ItemFlag.NotBound) yield return new Note(BindOnEquipText, NoteTint.Bind);
        if (state == (byte)ItemFlag.Bound) yield return new Note(BoundText, NoteTint.Bind);
        if (state is (byte)ItemFlag.NotBound or (byte)ItemFlag.Bound)
            yield return new Note(UnbindStonesText, NoteTint.Stones, WithStones: true);
        if (state is (byte)ItemFlag.Sealed or (byte)ItemFlag.SealedPremium) yield return new Note(SealedText, NoteTint.Sealed);
        if (race == OneTimeTradeRace)
            yield return state == OneTimeTradeDoneState
                ? new Note(OneTimeTradeDoneText, NoteTint.TradeDone)
                : new Note(OneTimeTradeText, NoteTint.TradeOnce);
    }

    private const int NoTradeIdFirst = 900_000_000;
    private const int NoTradeIdLast = 999_999_999;
    private const int NoTradeSoldStoredText = 4580;
    private const int NoTradeSoldText = 4581;
    private const int NoTradeSoldDestroyedText = 4595;
    private const int TradeAfter72HoursText = 4597;
    private const int NoTradeDestroyedText = 4596;
    private const int NoTradeSoldOrDestroyedText = 4600;
    private const int NoTradeAnythingText = 45020;

    private const int RaceNoTradeSellDestroy = 50;
    private const int RaceTradeAfter72Hours = 69;
    private const int RaceNoTradeDestroy = 70;
    private const int RaceNoTradeDestroyAlt = 71;
    private const int RaceNoTradeSellStore = 73;
    private const int RaceNoTradeSellDestroyAlt = 76;
    private const int RaceNoTradeSellStoreAlt = 77;

    public static int TradeNote(int itemId, int race)
    {
        int local = (int)((uint)itemId % OrgIdSpan);
        if (local is >= NoTradeIdFirst and <= NoTradeIdLast)
            return race is RaceNoTradeSellDestroy or RaceTradeAfter72Hours or RaceNoTradeDestroy
                ? NoTradeAnythingText
                : NoTradeSoldStoredText;
        return race switch
        {
            ItemData.QuestItemRace => NoTradeSoldText,
            RaceNoTradeSellDestroy or RaceNoTradeSellDestroyAlt => NoTradeSoldDestroyedText,
            RaceTradeAfter72Hours => TradeAfter72HoursText,
            RaceNoTradeDestroy or RaceNoTradeDestroyAlt => NoTradeDestroyedText,
            RaceNoTradeSellStore or RaceNoTradeSellStoreAlt => NoTradeSoldStoredText,
            OneTimeTradeRace => NoTradeSoldOrDestroyedText,
            _ => 0,
        };
    }

    private const int SlotOneHand = 0;
    private const int SlotRightOnly = 1;
    private const int SlotLeftOnly = 2;
    private const int SlotTwoHandRight = 3;
    private const int SlotTwoHandLeft = 4;
    private const int SlotPauldron = 5;
    private const int SlotPants = 6;
    private const int SlotHelmet = 7;
    private const int SlotGloves = 8;
    private const int SlotBoots = 9;
    private const int SlotEarring = 10;
    private const int SlotNecklace = 11;
    private const int SlotRing = 12;
    private const int SlotBelt = 14;
    private const int SlotPet = 20;
    private const int SlotTypeSpan = 100;
    public const int ShieldKind = 60;

    public static int CompareSlot(int slotType, Func<int, int?> wornKind)
    {
        bool Has(int slot) => wornKind(slot) != null;
        bool Shield(int slot) => wornKind(slot) == ShieldKind;
        bool Weapon(int slot) => Has(slot) && !Shield(slot);

        int slot = (slotType % SlotTypeSpan) switch
        {
            SlotOneHand or SlotTwoHandRight =>
                Has(InventoryConstants.RightHand) ? InventoryConstants.RightHand
                : Weapon(InventoryConstants.LeftHand) ? InventoryConstants.LeftHand
                : NoWornSlot,
            SlotRightOnly => InventoryConstants.RightHand,
            SlotLeftOnly => Shield(InventoryConstants.LeftHand) ? InventoryConstants.LeftHand : NoWornSlot,
            SlotTwoHandLeft =>
                Has(InventoryConstants.LeftHand)
                    ? (Shield(InventoryConstants.LeftHand) ? NoWornSlot : InventoryConstants.LeftHand)
                    : Has(InventoryConstants.RightHand) ? InventoryConstants.RightHand : NoWornSlot,
            SlotPauldron => InventoryConstants.Breast,
            SlotPants => InventoryConstants.Leg,
            SlotHelmet => InventoryConstants.Head,
            SlotGloves => InventoryConstants.Glove,
            SlotBoots => InventoryConstants.Foot,
            SlotEarring =>
                Has(InventoryConstants.RightEar) ? InventoryConstants.RightEar
                : Has(InventoryConstants.LeftEar) ? InventoryConstants.LeftEar
                : NoWornSlot,
            SlotNecklace => InventoryConstants.Neck,
            SlotRing =>
                Has(InventoryConstants.RightRing) ? InventoryConstants.RightRing
                : Has(InventoryConstants.LeftRing) ? InventoryConstants.LeftRing
                : NoWornSlot,
            SlotBelt => InventoryConstants.Waist,
            SlotPet => InventoryConstants.Pet,
            _ => NoWornSlot,
        };
        return slot != NoWornSlot && Has(slot) ? slot : NoWornSlot;
    }

    public const int SetRaceFirst = 101;
    public const int SetRaceLast = 199;
    public const int SetKeySpan = 10_000;
    private const int SetPositionShiftFrom = 5;

    public static bool IsSetRace(int race) => race is >= SetRaceFirst and <= SetRaceLast;

    public static int SetKey(int race, IEnumerable<int> wornSetSlots)
    {
        int mask = 0;
        foreach (int slot in wornSetSlots)
            mask |= 1 << (slot >= SetPositionShiftFrom ? slot - 1 : slot);
        return race * SetKeySpan + mask;
    }

    public static int PieceBit(int slotType) => 1 << (slotType % SlotTypeSpan);
}
