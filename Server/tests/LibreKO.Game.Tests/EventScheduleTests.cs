using FluentAssertions;
using LibreKO.Game.Protocol;
using LibreKO.Game.Protocol.Writers;

namespace LibreKO.Game.Tests;

public class EventScheduleTests
{
    private const int ThreeHoursAhead = 180;

    [Fact]
    public void ADailyLocalHourIsSentInUtc()
    {
        var slot = EventSchedulePacketCoordinator.Slot(EventScheduleKind.Temple, "Juraid Mountain", null, 1, 30,
            ThreeHoursAhead, 40, 83);

        slot.Day.Should().Be(EventSchedulePacketWriter.EveryDay);
        slot.HourUtc.Should().Be(22);
        slot.MinuteUtc.Should().Be(30);
    }

    [Fact]
    public void AWeeklyLocalTimeCrossesBackIntoTheDayBefore()
    {
        var slot = EventSchedulePacketCoordinator.Slot(EventScheduleKind.Temple, "Under The Castle", DayOfWeek.Sunday, 1, 0,
            ThreeHoursAhead, 70, 83);

        slot.Day.Should().Be((byte)DayOfWeek.Saturday);
        slot.HourUtc.Should().Be(22);
    }

    [Fact]
    public void UtcSchedulesPassThrough()
    {
        var slot = EventSchedulePacketCoordinator.Slot(EventScheduleKind.CollectionRace, "Race", DayOfWeek.Wednesday, 11, 5,
            0, 1, 83);

        slot.Should().Be(new EventSlot(EventScheduleKind.CollectionRace, "Race", (byte)DayOfWeek.Wednesday, 11, 5, 1, 83));
    }
}
