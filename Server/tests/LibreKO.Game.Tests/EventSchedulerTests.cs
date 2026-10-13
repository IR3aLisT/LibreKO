using FluentAssertions;
using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Domain.Services;
using LibreKO.Common.Infrastructure.Persistence.Seed.Entities;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Configuration;
using LibreKO.Game.World;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace LibreKO.Game.Tests;

public class EventSchedulerTests
{
    private readonly SessionManager _sessionManager;
    private readonly IOptions<GameServerSettings> _settings;
    private readonly IZoneTransitionService _zoneTransition;
    private readonly ICollectionRaceService _collectionRace;
    private readonly ILotteryService _lottery;
    private readonly IJuraidMountainService _juraidMountain;
    private readonly IBorderDefenseWarService _bdw;
    private readonly IUnderTheCastleService _utc;
    private readonly IForgottenTempleService _ft;
    private readonly IGameDataService _gameData;
    private readonly ILogger<EventSchedulerService> _logger;
    private readonly EventSchedulerService _scheduler;

    public EventSchedulerTests()
    {
        _sessionManager = new SessionManager();
        _settings = Options.Create(new GameServerSettings());
        _zoneTransition = Substitute.For<IZoneTransitionService>();
        _collectionRace = Substitute.For<ICollectionRaceService>();
        _lottery = Substitute.For<ILotteryService>();
        _juraidMountain = Substitute.For<IJuraidMountainService>();
        _bdw = Substitute.For<IBorderDefenseWarService>();
        _utc = Substitute.For<IUnderTheCastleService>();
        _ft = Substitute.For<IForgottenTempleService>();
        _gameData = Substitute.For<IGameDataService>();
        _logger = Substitute.For<ILogger<EventSchedulerService>>();

        _scheduler = new EventSchedulerService(
            _sessionManager,
            _settings,
            _zoneTransition,
            _collectionRace,
            _lottery,
            _juraidMountain,
            _bdw,
            Substitute.For<IChaosDungeonService>(),
            _utc,
            _ft,
            Substitute.For<IKrowazService>(),
            Substitute.For<INereidMonumentService>(),
            _gameData,
            new ScheduleClock(),
            _logger);
    }

    private (UserSession session, List<Packet> sentPackets) CreateTestSession(int charId, int level)
    {
        var client = Substitute.For<IClient>();
        client.Id.Returns(Guid.NewGuid());
        var packets = new List<Packet>();
        client.SendPacket(Arg.Do<Packet>(p => packets.Add(p)), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var session = _sessionManager.CreateSession(client, characterId: charId, accountId: charId * 10);
        session.Name = $"Player_{charId}";
        session.Nation = AccountNation.ElMorad;
        session.Level = (byte)level;
        session.ZoneId = 21;
        return (session, packets);
    }

    [Fact]
    public async Task CallTempleEventAsync_OnlySendsPopupToEligibleLevelPlayers()
    {
        var (lowPlayer, lowPackets) = CreateTestSession(101, 50);
        var (highPlayer, highPackets) = CreateTestSession(102, 70);

        await _scheduler.CallTempleEventAsync(
            TempleEvent.ForgottenTemple,
            joinWindowSeconds: 300,
            autoJoinSession: null,
            minLevel: 60,
            maxLevel: 83);

        lowPackets.Should().NotContain(p => p.GetOpcode() == (byte)GameOpcodes.GS_BIFROST);

        highPackets.Should().Contain(p => p.GetOpcode() == (byte)GameOpcodes.GS_BIFROST);
    }

    [Fact]
    public async Task CallTempleEventAsync_OutOfRangeCaller_IsNotAutoJoined()
    {
        var (lowPlayer, _) = CreateTestSession(103, 50);

        await _scheduler.CallTempleEventAsync(
            TempleEvent.ForgottenTemple,
            joinWindowSeconds: 300,
            autoJoinSession: lowPlayer,
            minLevel: 60,
            maxLevel: 83);

        _scheduler.TryJoinTempleEvent(lowPlayer).Should().Be(TempleJoinResult.LevelTooLow);
    }

    [Fact]
    public async Task CallTempleEventAsync_InRangeCaller_IsAutoJoined()
    {
        var (highPlayer, _) = CreateTestSession(104, 75);

        await _scheduler.CallTempleEventAsync(
            TempleEvent.ForgottenTemple,
            joinWindowSeconds: 300,
            autoJoinSession: highPlayer,
            minLevel: 60,
            maxLevel: 83);

        _scheduler.TryJoinTempleEvent(highPlayer).Should().Be(TempleJoinResult.AlreadyJoined);
    }

    private static readonly DateTime FridayEightPmBerlin = new(2026, 10, 16, 18, 0, 0, DateTimeKind.Utc);

    private static TempleEventScheduleData Row(int id, TempleEvent contest, byte hour, byte minLevel, byte maxLevel,
        DayOfWeek? day = null, byte minute = 0) =>
        new() { Id = id, Event = contest, Day = day, Hour = hour, Minute = minute, MinLevel = minLevel, MaxLevel = maxLevel, CountdownMinutes = 10 };

    [Fact]
    public async Task TwoEventsDueInTheSameMinuteBothStart()
    {
        _gameData.TempleEventSchedules.Returns([
            Row(1, TempleEvent.BorderDefenseWar, 20, 20, 83),
            Row(2, TempleEvent.UnderTheCastle, 20, 70, 83, DayOfWeek.Friday),
        ]);

        await _scheduler.TickTempleEventsAsync(FridayEightPmBerlin);

        _scheduler.TempleEvents.Select(e => e.Contest).Should().BeEquivalentTo(
            [TempleEvent.BorderDefenseWar, TempleEvent.UnderTheCastle]);
        _scheduler.TempleEvents.Should().OnlyContain(e => e.JoinOpen);
    }

    [Fact]
    public async Task EveryShippedScheduleRowStartsItsEventWithinAWeek()
    {
        var rows = new TempleEventScheduleSeed().GetSeedData().ToList();
        _gameData.TempleEventSchedules.Returns(rows);
        var start = new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc);
        var missed = new List<string>();

        for (var utc = start; utc < start.AddDays(7); utc = utc.AddMinutes(1))
        {
            await _scheduler.TickTempleEventsAsync(utc);
            var local = ScheduleTime.Now(_settings.Value.Events, utc);
            var running = _scheduler.TempleEventsAt(utc);
            foreach (var row in rows.Where(r => r.Matches(local)))
            {
                int window = row.CountdownMinutes * TempleEventRules.SecondsPerMinute;
                if (!running.Any(e => e.Contest == row.Event && e.JoinOpen && e.SecondsLeft == window))
                    missed.Add($"row {row.Id} {row.Event} at {local:ddd HH:mm}");
            }
        }

        rows.Should().NotBeEmpty();
        missed.Should().BeEmpty();
    }

    [Fact]
    public async Task ScheduleRowsAreReadOnTheScheduleClockNotUtc()
    {
        _gameData.TempleEventSchedules.Returns([Row(1, TempleEvent.BorderDefenseWar, 18, 20, 83)]);

        await _scheduler.TickTempleEventsAsync(FridayEightPmBerlin);

        _scheduler.TempleEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task ARunningEventDoesNotHoldBackTheNextOne()
    {
        _gameData.TempleEventSchedules.Returns([Row(1, TempleEvent.ForgottenTemple, 20, 60, 83)]);
        await _scheduler.TickTempleEventsAsync(FridayEightPmBerlin);
        _ft.IsActive.Returns(true);
        await _scheduler.TickTempleEventsAsync(FridayEightPmBerlin.AddMinutes(TempleEventRules.DefaultCountdownMinutes));

        _gameData.TempleEventSchedules.Returns([Row(2, TempleEvent.JuraidMountain, 20, 70, 83, minute: 40)]);
        var juraidMinute = FridayEightPmBerlin.AddMinutes(40);
        await _scheduler.TickTempleEventsAsync(juraidMinute);

        _scheduler.TempleEvents.Should().HaveCount(2);
        _scheduler.TempleEvents.Should().Contain(e => e.Contest == TempleEvent.ForgottenTemple && !e.JoinOpen);
        _scheduler.TempleEvents.Should().Contain(e => e.Contest == TempleEvent.JuraidMountain && e.JoinOpen);
    }

    [Fact]
    public async Task AJoinGoesToTheNamedSignUpOrTheNewestOneThePlayerFits()
    {
        var (player, _) = CreateTestSession(105, 75);
        await _scheduler.CallTempleEventAsync(TempleEvent.BorderDefenseWar, 300, minLevel: 20, maxLevel: 83);
        await _scheduler.CallTempleEventAsync(TempleEvent.ForgottenTemple, 300, minLevel: 46, maxLevel: 59);

        _scheduler.SignUpFor(player).Should().Be(TempleEvent.BorderDefenseWar);
        _scheduler.TryJoinTempleEvent(player, TempleEvent.ForgottenTemple).Should().Be(TempleJoinResult.LevelTooHigh);
        _scheduler.TryJoinTempleEvent(player).Should().Be(TempleJoinResult.Joined);
        _scheduler.TempleSignUps(TempleEvent.BorderDefenseWar).Should().Be(((ushort)0, (ushort)1));
    }

    [Fact]
    public async Task ASecondSignUpIsRefusedAsAnotherInstantDungeon()
    {
        var (player, _) = CreateTestSession(106, 75);
        await _scheduler.CallTempleEventAsync(TempleEvent.BorderDefenseWar, 300, minLevel: 20, maxLevel: 83);
        await _scheduler.CallTempleEventAsync(TempleEvent.UnderTheCastle, 300, minLevel: 70, maxLevel: 83);

        _scheduler.TryJoinTempleEvent(player, TempleEvent.UnderTheCastle).Should().Be(TempleJoinResult.Joined);
        _scheduler.TryJoinTempleEvent(player, TempleEvent.BorderDefenseWar).Should().Be(TempleJoinResult.OtherDungeon);

        _scheduler.LeaveTempleEvent(player.CharacterId).Should().Be(TempleEvent.UnderTheCastle);
        _scheduler.TryJoinTempleEvent(player, TempleEvent.BorderDefenseWar).Should().Be(TempleJoinResult.Joined);
    }

    [Fact]
    public async Task CancellingOneEventLeavesTheOthersRunning()
    {
        await _scheduler.CallTempleEventAsync(TempleEvent.BorderDefenseWar, 300, minLevel: 20, maxLevel: 83);
        await _scheduler.CallTempleEventAsync(TempleEvent.Chaos, 300, minLevel: 1, maxLevel: 83);

        (await _scheduler.CancelTempleEventAsync(TempleEvent.Chaos)).Should().BeTrue();

        _scheduler.IsTempleEventRunning(TempleEvent.Chaos).Should().BeFalse();
        _scheduler.IsTempleEventRunning(TempleEvent.BorderDefenseWar).Should().BeTrue();
        (await _scheduler.CancelTempleEventAsync(TempleEvent.Chaos)).Should().BeFalse();
    }
}
