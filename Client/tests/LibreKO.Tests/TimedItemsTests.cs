using System;
using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class TimedItemsTests
{
    private const long Now = 1_800_000_000;
    private const long Minute = 60;
    private const long Hour = 60 * Minute;
    private const long Day = 24 * Hour;

    [Theory]
    [InlineData(5 * Day, TimeLeftUnit.Days, 5, false)]
    [InlineData(4 * Day + 2 * Hour, TimeLeftUnit.Days, 4, false)]
    [InlineData(3 * Day + 23 * Hour, TimeLeftUnit.Days, 3, true)]
    [InlineData(Day, TimeLeftUnit.Days, 1, true)]
    [InlineData(23 * Hour, TimeLeftUnit.Hours, 23, true)]
    [InlineData(59 * Minute, TimeLeftUnit.Minutes, 59, true)]
    [InlineData(30, TimeLeftUnit.UnderAMinute, 0, true)]
    [InlineData(-Day, TimeLeftUnit.UnderAMinute, 0, true)]
    public void TimeLeftFollowsTheRetailSteps(long left, TimeLeftUnit unit, long amount, bool urgent)
    {
        Assert.Equal(new TimeLeft(unit, amount, urgent), TimedItems.Remaining(Now + left, Now));
    }

    [Fact]
    public void OnlyTimedItemsAreListedSoonestFirstWithTheirPlace()
    {
        var slots = new ItemSlot[InventoryConstants.InventoryTotal];
        slots[InventoryConstants.RightHand] = new ItemSlot { ItemId = 1, ExpiresAt = 300 };
        slots[InventoryConstants.InventoryStart + 4] = new ItemSlot { ItemId = 2, ExpiresAt = 100 };
        slots[InventoryConstants.InventoryStart + 5] = new ItemSlot { ItemId = 3 };
        slots[InventoryConstants.CospreStart] = new ItemSlot { ItemId = 4, ExpiresAt = 200 };
        slots[InventoryConstants.MagicBagStart + 1] = new ItemSlot { ItemId = 5, ExpiresAt = 400 };
        var pet = new[] { new ItemSlot { ItemId = 6, ExpiresAt = 50 }, default };

        var list = TimedItems.Collect(slots.Length, abs => slots[abs], pet);

        Assert.Equal(new[] { 6, 2, 4, 1, 5 }, list.ConvertAll(item => item.Slot.ItemId));
        Assert.Equal(new[] { TimedItemPlace.PetEquipment, TimedItemPlace.Inventory, TimedItemPlace.Cospre,
                TimedItemPlace.Equipment, TimedItemPlace.MagicBag },
            list.ConvertAll(item => item.Place));
    }

    [Fact]
    public void BagSlotsHaveNoPlaceLine()
    {
        Assert.Null(TimedItems.PlaceText(TimedItemPlace.BagSlot));
        Assert.NotNull(TimedItems.PlaceText(TimedItemPlace.Inventory));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    [InlineData(7, 3)]
    public void PagesHoldThreeRows(int entries, int pages)
    {
        Assert.Equal(pages, TimedItems.PageCount(entries));
    }

    [Fact]
    public void DoNotShowTodayLastsForTheDayOnly()
    {
        var today = new DateTime(2026, 10, 9, 8, 0, 0);
        int saved = TimedItems.DayKey(today);
        Assert.Equal(1009, saved);
        Assert.True(TimedItems.HiddenToday(saved, today.AddHours(15)));
        Assert.False(TimedItems.HiddenToday(saved, today.AddDays(1)));
    }
}
