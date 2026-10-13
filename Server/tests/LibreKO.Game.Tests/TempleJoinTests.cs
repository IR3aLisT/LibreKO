using FluentAssertions;
using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Domain.Services;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Configuration;
using LibreKO.Game.Protocol;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace LibreKO.Game.Tests;

public class TempleJoinTests
{
    private const int JoinWindow = 300;
    private readonly SessionManager _sessions = new();
    private readonly GameServerSettings _settings = new();
    private readonly EventSchedulerService _scheduler;

    public TempleJoinTests()
    {
        _scheduler = new EventSchedulerService(
            _sessions,
            Options.Create(_settings),
            Substitute.For<IZoneTransitionService>(),
            Substitute.For<ICollectionRaceService>(),
            Substitute.For<ILotteryService>(),
            Substitute.For<IJuraidMountainService>(),
            Substitute.For<IBorderDefenseWarService>(),
            Substitute.For<IChaosDungeonService>(),
            Substitute.For<IUnderTheCastleService>(),
            Substitute.For<IForgottenTempleService>(),
            Substitute.For<IKrowazService>(),
            Substitute.For<INereidMonumentService>(),
            Substitute.For<IGameDataService>(),
            new ScheduleClock(),
            Substitute.For<ILogger<EventSchedulerService>>());
    }

    private UserSession Player(int characterId, byte level, AccountNation nation = AccountNation.ElMorad)
    {
        var client = Substitute.For<IClient>();
        client.Id.Returns(Guid.NewGuid());
        client.SendPacket(Arg.Any<Packet>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var session = _sessions.CreateSession(client, characterId: characterId, accountId: characterId * 10);
        session.Level = level;
        session.Nation = nation;
        for (int i = 0; i < session.Inventory.Length; i++)
            session.Inventory[i] = new ItemSlot();
        return session;
    }

    [Fact]
    public void NothingOpenIsTheNotTimeCode()
    {
        _scheduler.TryJoinTempleEvent(Player(1, 70)).Should().Be(TempleJoinResult.NotOpen);
    }

    [Fact]
    public async Task LevelsOutsideTheBandAreRefusedEachWithItsOwnCode()
    {
        await _scheduler.CallTempleEventAsync(TempleEvent.ForgottenTemple, JoinWindow, minLevel: 60, maxLevel: 70);

        _scheduler.TryJoinTempleEvent(Player(1, 59)).Should().Be(TempleJoinResult.LevelTooLow);
        _scheduler.TryJoinTempleEvent(Player(2, 71)).Should().Be(TempleJoinResult.LevelTooHigh);
        var player = Player(3, 65);
        _scheduler.TryJoinTempleEvent(player).Should().Be(TempleJoinResult.Joined);
        _scheduler.TryJoinTempleEvent(player).Should().Be(TempleJoinResult.AlreadyJoined);
    }

    [Fact]
    public async Task ChaosAsksForTheMapOnlyWhenTheServerRequiresIt()
    {
        await _scheduler.CallTempleEventAsync(TempleEvent.Chaos, JoinWindow, minLevel: 1, maxLevel: 83);
        _scheduler.TryJoinTempleEvent(Player(1, 60)).Should().Be(TempleJoinResult.Joined);

        _settings.Events.ChaosNeedsMap = true;
        _scheduler.TryJoinTempleEvent(Player(2, 60)).Should().Be(TempleJoinResult.NeedsMapOfChaos);

        var mapHolder = Player(3, 60);
        mapHolder.Inventory[InventoryConstants.SlotMax] = new ItemSlot { ItemId = TempleEventRules.MapOfChaos, Count = 1 };
        _scheduler.TryJoinTempleEvent(mapHolder).Should().Be(TempleJoinResult.Joined);
    }

    [Fact]
    public async Task SignUpsAreCountedPerNation()
    {
        await _scheduler.CallTempleEventAsync(TempleEvent.BorderDefenseWar, JoinWindow, minLevel: 1, maxLevel: 83);
        _scheduler.TryJoinTempleEvent(Player(1, 60, AccountNation.Karus));
        _scheduler.TryJoinTempleEvent(Player(2, 60, AccountNation.Karus));
        _scheduler.TryJoinTempleEvent(Player(3, 60));

        _scheduler.TempleSignUps(TempleEvent.BorderDefenseWar).Should().Be(((ushort)2, (ushort)1));
    }

    [Fact]
    public void TheCountPacketCarriesTheEventAndBothNations()
    {
        var packet = EventPacketWriter.TempleSignUps(TempleEvent.BorderDefenseWar, 2, 1);

        packet.GetOpcode().Should().Be((byte)GameOpcodes.GS_EVENT);
        packet.ResetOffset();
        packet.ReadByte().Should().Be((byte)TempleSubOpcode.TempleEventCounter);
        packet.ReadUShort().Should().Be((ushort)TempleEvent.BorderDefenseWar);
        packet.ReadUShort().Should().Be(2);
        packet.ReadUShort().Should().Be(1);
    }
}
