using System;
using System.Collections.Generic;
using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class EventScheduleTests
{
    private static readonly DateTime Wednesday1000 = new(2026, 10, 7, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ADailySlotThatPassedMovesToTomorrow()
    {
        var slot = new EventSlot(EventKind.Temple, "BDW", EventSchedule.EveryDay, 9, 0, 20, 83);

        Assert.Equal(Wednesday1000.Date.AddDays(1).AddHours(9), EventSchedule.NextStartUtc(slot, Wednesday1000));
    }

    [Fact]
    public void AWeeklySlotFindsItsNextWeekday()
    {
        var friday = new EventSlot(EventKind.Temple, "UTC", (int)DayOfWeek.Friday, 21, 0, 70, 83);

        Assert.Equal(new DateTime(2026, 10, 9, 21, 0, 0, DateTimeKind.Utc), EventSchedule.NextStartUtc(friday, Wednesday1000));
    }

    [Fact]
    public void TheIconPulsesOnlyForAFittingEventWithinTwentyMinutes()
    {
        var schedule = new EventSchedule(new List<EventNow>(), new List<EventSlot>
        {
            new(EventKind.Temple, "FT", EventSchedule.EveryDay, 10, 15, 60, 83),
        }, Wednesday1000);

        Assert.True(schedule.AlertDue(65, Wednesday1000, TimeSpan.FromMinutes(20)));
        Assert.False(schedule.AlertDue(40, Wednesday1000, TimeSpan.FromMinutes(20)));
        Assert.False(schedule.AlertDue(65, Wednesday1000, TimeSpan.FromMinutes(10)));
    }

    [Fact]
    public void TheNextDayListsWhatStartsWithinTwentyFourHoursInStartOrder()
    {
        var schedule = new EventSchedule(new List<EventNow>(), new List<EventSlot>
        {
            new(EventKind.Lottery, "Late", EventSchedule.EveryDay, 22, 0, 0, 0),
            new(EventKind.Lottery, "Early", EventSchedule.EveryDay, 11, 0, 0, 0),
            new(EventKind.Lottery, "Tomorrow", EventSchedule.EveryDay, 9, 0, 0, 0),
            new(EventKind.Temple, "Friday", (int)DayOfWeek.Friday, 9, 0, 0, 0),
        }, Wednesday1000);

        var next = schedule.Upcoming(Wednesday1000, TimeSpan.FromHours(24));

        Assert.Equal(new[] { "Early", "Late", "Tomorrow" }, next.ConvertAll(u => u.Slot.Name));
    }

    [Fact]
    public void ARunningEventCountsDownFromWhenItArrived()
    {
        var entry = new EventNow(EventKind.Temple, "BDW", EventPhase.SignUp, 100, 20, 83);
        var schedule = new EventSchedule(new List<EventNow> { entry }, new List<EventSlot>(), Wednesday1000);

        Assert.Equal(70, schedule.SecondsLeft(entry, Wednesday1000.AddSeconds(30)));
    }
}
