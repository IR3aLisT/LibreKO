using LibreKO.Common.Domain.Services;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Configuration;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;
using Microsoft.Extensions.Options;

namespace LibreKO.Game.Protocol;

public interface IEventSchedulePacketCoordinator
{
    Task HandleAsync(IClient client, Packet packet);
}

public sealed class EventSchedulePacketCoordinator(
    SessionManager sessionManager,
    IGameDataService gameDataService,
    EventSchedulerService scheduler,
    ICollectionRaceService collectionRaces,
    ILotteryService lottery,
    IOptions<GameServerSettings> settings) : IEventSchedulePacketCoordinator
{
    private const int MinutesPerHour = 60;
    private const int MinutesPerDay = 24 * MinutesPerHour;
    private const int DaysPerWeek = 7;
    private const int MinutesPerWeek = DaysPerWeek * MinutesPerDay;
    private const byte AnyLevel = 0;

    public async Task HandleAsync(IClient client, Packet packet)
    {
        var session = sessionManager.GetByClientId(client.Id);
        if (session == null || packet.RemainingBytes < 1 || packet.ReadByte() != EventSchedulePacketWriter.SubSchedule)
            return;

        await session.Client.SendPacket(EventSchedulePacketWriter.Schedule(Now(), Slots()));
    }

    internal List<EventNow> Now()
    {
        var now = new List<EventNow>();
        if (scheduler.CurrentTempleEvent != TempleEvent.None)
        {
            bool signUp = scheduler.IsTempleEventJoinOpen;
            now.Add(new EventNow(EventScheduleKind.Temple, TempleEventRules.NameFor(scheduler.CurrentTempleEvent),
                signUp ? EventSchedulePhase.SignUp : EventSchedulePhase.Running,
                signUp ? scheduler.TempleRemainingJoinSeconds : scheduler.TempleRemainingSeconds,
                scheduler.TempleMinLevel, scheduler.TempleMaxLevel));
        }

        foreach (var race in collectionRaces.ActiveRaces)
            now.Add(new EventNow(EventScheduleKind.CollectionRace, race.Race.Name, EventSchedulePhase.Running,
                race.RemainingSeconds, race.Race.MinLevel, race.Race.MaxLevel));

        if (lottery.ActiveEvent is { Closed: false } draw)
            now.Add(new EventNow(EventScheduleKind.Lottery, draw.EventData.Name, EventSchedulePhase.Running,
                draw.RemainingSeconds, AnyLevel, AnyLevel));

        return now;
    }

    internal List<EventSlot> Slots()
    {
        var events = settings.Value.Events;
        int offset = events.UseLocalTimeForSchedules
            ? (int)TimeZoneInfo.Local.GetUtcOffset(DateTime.Now).TotalMinutes
            : 0;
        var slots = new List<EventSlot>();

        foreach (var s in gameDataService.TempleEventSchedules ?? [])
            slots.Add(Slot(EventScheduleKind.Temple, TempleEventRules.NameFor(s.Event), s.Day, s.Hour, s.Minute,
                offset, s.MinLevel, s.MaxLevel));

        foreach (var hour in events.ChaosStartHours)
            slots.Add(Slot(EventScheduleKind.Temple, TempleEventRules.NameFor(TempleEvent.Chaos), null, (byte)hour,
                TempleEventRules.StartMinuteOfHour, offset, AnyLevel, AnyLevel));

        foreach (var group in gameDataService.CollectionRaceSchedulesByRace)
        {
            if (!gameDataService.CollectionRaceTable.TryGetValue(group.Key, out var race))
                continue;
            foreach (var s in group)
                slots.Add(Slot(EventScheduleKind.CollectionRace, race.Name, s.Day, s.Hour, s.Minute, 0,
                    race.MinLevel, race.MaxLevel));
        }

        foreach (var group in gameDataService.LotterySchedulesByEvent)
        {
            if (!gameDataService.LotteryEventTable.TryGetValue(group.Key, out var draw))
                continue;
            foreach (var s in group)
                slots.Add(Slot(EventScheduleKind.Lottery, draw.Name, s.Day, s.Hour, s.Minute, 0, AnyLevel, AnyLevel));
        }

        return slots;
    }

    internal static EventSlot Slot(EventScheduleKind kind, string name, DayOfWeek? day, byte hour, byte minute,
        int offsetMinutes, byte minLevel, byte maxLevel)
    {
        if (day == null)
        {
            int minuteOfDay = Wrap(hour * MinutesPerHour + minute - offsetMinutes, MinutesPerDay);
            return new EventSlot(kind, name, EventSchedulePacketWriter.EveryDay,
                (byte)(minuteOfDay / MinutesPerHour), (byte)(minuteOfDay % MinutesPerHour), minLevel, maxLevel);
        }

        int minuteOfWeek = Wrap((int)day.Value * MinutesPerDay + hour * MinutesPerHour + minute - offsetMinutes,
            MinutesPerWeek);
        int dayMinutes = minuteOfWeek % MinutesPerDay;
        return new EventSlot(kind, name, (byte)(minuteOfWeek / MinutesPerDay),
            (byte)(dayMinutes / MinutesPerHour), (byte)(dayMinutes % MinutesPerHour), minLevel, maxLevel);
    }

    private static int Wrap(int value, int span) => ((value % span) + span) % span;
}
