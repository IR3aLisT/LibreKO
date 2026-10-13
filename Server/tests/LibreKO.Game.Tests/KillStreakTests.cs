using FluentAssertions;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace LibreKO.Game.Tests;

public class KillStreakTests
{
    private readonly SessionManager _sessions = new();
    private readonly Dictionary<int, List<Packet>> _sent = [];
    private readonly KillStreakService _service;

    public KillStreakTests()
    {
        _service = new KillStreakService(_sessions, Substitute.For<ILogger<KillStreakService>>());
    }

    private UserSession Player(int id, AccountNation nation, byte zone = 71)
    {
        var client = Substitute.For<IClient>();
        client.Id.Returns(Guid.NewGuid());
        var log = _sent[id] = [];
        client.SendPacket(Arg.Do<Packet>(p => log.Add(p)), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var session = _sessions.CreateSession(client, characterId: id, accountId: id * 10);
        session.Name = $"Player{id}";
        session.Nation = nation;
        session.ZoneId = zone;
        return session;
    }

    private static (byte Kind, int Id, string Name, byte Nation, ushort Count) Read(Packet p)
    {
        p.ResetOffset();
        p.ReadByte().Should().Be(KillAssistPacketWriter.KillSub);
        return (p.ReadByte(), p.ReadInt(), p.ReadString(), p.ReadByte(), p.ReadUShort());
    }

    [Fact]
    public async Task EachKillRaisesTheKillersOwnSplash()
    {
        var killer = Player(1, AccountNation.Karus);
        var victim = Player(2, AccountNation.ElMorad);

        await _service.OnPlayerKilledAsync(victim, killer);
        await _service.OnPlayerKilledAsync(victim, killer);

        var last = Read(_sent[1].Last());
        last.Kind.Should().Be(KillAssistPacketWriter.OwnStreak);
        last.Name.Should().Be(killer.Name);
        last.Nation.Should().Be((byte)AccountNation.Karus);
        last.Count.Should().Be(2);
    }

    [Fact]
    public async Task DyingEndsTheStreakAndSaysSo()
    {
        var killer = Player(1, AccountNation.Karus);
        var victim = Player(2, AccountNation.ElMorad);
        await _service.OnPlayerKilledAsync(victim, killer);

        await _service.OnPlayerDiedAsync(killer);

        _service.StreakOf(killer.CharacterId).Should().Be(0);
        Read(_sent[1].Last()).Count.Should().Be(0);
        await _service.OnPlayerDiedAsync(victim);
        _sent[2].Should().BeEmpty();
    }

    [Fact]
    public async Task EveryTenthKillIsAnnouncedInTheZone()
    {
        var killer = Player(1, AccountNation.Karus);
        var victim = Player(2, AccountNation.ElMorad);
        var bystander = Player(3, AccountNation.ElMorad);
        var elsewhere = Player(4, AccountNation.ElMorad, zone: 21);

        for (int i = 0; i < KillStreakService.AnnouncementStep; i++)
            await _service.OnPlayerKilledAsync(victim, killer);

        var heard = Read(_sent[3].Single());
        heard.Kind.Should().Be(KillAssistPacketWriter.StreakAnnouncement);
        heard.Count.Should().Be((ushort)KillStreakService.AnnouncementStep);
        _sent[4].Should().BeEmpty();
    }
}
