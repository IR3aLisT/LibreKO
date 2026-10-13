using System;
using System.Collections.Generic;
using System.Linq;

namespace LibreKO.Domain;

public enum TimedItemPlace : byte { Equipment, Inventory, Cospre, MagicBag, BagSlot, PetEquipment }

public enum TimeLeftUnit : byte { Days, Hours, Minutes, UnderAMinute }

public readonly record struct TimedItem(ItemSlot Slot, TimedItemPlace Place);

public readonly record struct TimeLeft(TimeLeftUnit Unit, long Amount, bool Urgent);

public static class TimedItems
{
    public const int RowsPerPage = 3;
    public const int TitleText = 44640;
    public const int HideTodayText = 44645;
    public const long UrgentDays = 3;

    private const int DaysText = 25252;
    private const int HoursText = 10739;
    private const int MinutesText = 10740;
    private const int UnderAMinuteText = 42051;
    private const int SecondsPerMinute = 60;
    private const int MinutesPerHour = 60;
    private const int HoursPerDay = 24;
    private const int MonthDayFactor = 100;
    private const int EquipmentText = 42054;
    private const int InventoryText = 42052;
    private const int CospreText = 42055;
    private const int MagicBagText = 42053;
    private const int PetEquipmentText = 42056;

    public static int? PlaceText(TimedItemPlace place) => place switch
    {
        TimedItemPlace.Equipment => EquipmentText,
        TimedItemPlace.Inventory => InventoryText,
        TimedItemPlace.Cospre => CospreText,
        TimedItemPlace.MagicBag => MagicBagText,
        TimedItemPlace.PetEquipment => PetEquipmentText,
        _ => null,
    };

    public static TimedItemPlace PlaceOf(ItemMove.Region region) => region switch
    {
        ItemMove.Region.Equip => TimedItemPlace.Equipment,
        ItemMove.Region.Grid => TimedItemPlace.Inventory,
        ItemMove.Region.Cospre => TimedItemPlace.Cospre,
        ItemMove.Region.BagSlot => TimedItemPlace.BagSlot,
        _ => TimedItemPlace.MagicBag,
    };

    public static List<TimedItem> Collect(int inventoryLength, Func<int, ItemSlot> slotAt, IReadOnlyList<ItemSlot>? petItems)
    {
        var found = new List<TimedItem>();
        for (int abs = 0; abs < inventoryLength; abs++)
        {
            var slot = slotAt(abs);
            if (slot.IsEmpty || slot.ExpiresAt <= 0) continue;
            found.Add(new TimedItem(slot, PlaceOf(ItemMove.RegionOf(abs))));
        }
        if (petItems != null)
            foreach (var slot in petItems)
                if (!slot.IsEmpty && slot.ExpiresAt > 0)
                    found.Add(new TimedItem(slot, TimedItemPlace.PetEquipment));
        return found.OrderBy(item => item.Slot.ExpiresAt).ToList();
    }

    public static TimeLeft Remaining(long expiresAt, long now)
    {
        if (expiresAt <= now) return new TimeLeft(TimeLeftUnit.UnderAMinute, 0, true);
        long minutes = (expiresAt - now) / SecondsPerMinute;
        long hours = minutes / MinutesPerHour;
        long days = hours / HoursPerDay;
        if (days > UrgentDays) return new TimeLeft(TimeLeftUnit.Days, days, false);
        if (days > 0) return new TimeLeft(TimeLeftUnit.Days, days, true);
        if (hours > 0) return new TimeLeft(TimeLeftUnit.Hours, hours, true);
        if (minutes > 0) return new TimeLeft(TimeLeftUnit.Minutes, minutes, true);
        return new TimeLeft(TimeLeftUnit.UnderAMinute, 0, true);
    }

    public static int TextId(TimeLeftUnit unit) => unit switch
    {
        TimeLeftUnit.Days => DaysText,
        TimeLeftUnit.Hours => HoursText,
        TimeLeftUnit.Minutes => MinutesText,
        _ => UnderAMinuteText,
    };

    public static int PageCount(int entries) => Math.Max(1, (entries + RowsPerPage - 1) / RowsPerPage);

    public static int DayKey(DateTime local) => local.Day + MonthDayFactor * local.Month;

    public static bool HiddenToday(int savedDayKey, DateTime local) => savedDayKey == DayKey(local);
}
