using FluentAssertions;
using LibreKO.Common.Domain.Entities;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace LibreKO.Game.Tests;

public class ClanMatchTests
{
    private const short Wolves = 40;
    private const short Lions = 50;

    private readonly SessionManager _sessions = new();
    private readonly IZoneTransitionService _zones = Substitute.For<IZoneTransitionService>();
    private readonly ClanMatchService _match;

    public ClanMatchTests()
    {
        _sessions.Knights.AddClan(Wolves, new KnightsEntity { Id = Wolves, Name = "Wolves" });
        _sessions.Knights.AddClan(Lions, new KnightsEntity { Id = Lions, Name = "Lions" });
        _zones.ChangeZoneAsync(Arg.Any<UserSession>(), Arg.Any<byte>(), Arg.Any<float>(), Arg.Any<float>())
            .Returns(call =>
            {
                call.Arg<UserSession>().ZoneId = call.ArgAt<byte>(1);
                return Task.CompletedTask;
            });
        _match = new ClanMatchService(_sessions, _zones, NullLogger<ClanMatchService>.Instance);
    }

    private UserSession Member(int id, short clan)
    {
        var client = Substitute.For<IClient>();
        client.Id.Returns(Guid.NewGuid());
        client.SendPacket(Arg.Any<Packet>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var session = _sessions.CreateSession(client, id, id + 1000);
        session.Name = $"Fighter{id}";
        session.KnightsId = clan;
        session.ZoneId = (byte)ZoneId.Moradon;
        session.Hp = 100;
        session.MaxHp = 100;
        return session;
    }

    private static List<Packet> Sent(UserSession session) =>
        session.Client.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == nameof(IClient.SendPacket))
            .Select(call =>
            {
                var packet = (Packet)call.GetArguments()[0]!;
                packet.ResetOffset();
                return packet;
            })
            .ToList();

    [Fact]
    public async Task StartingSendsUpToEightOfEachClanToTheMatchZone()
    {
        var wolves = Enumerable.Range(1, 9).Select(i => Member(i, Wolves)).ToList();
        var lion = Member(20, Lions);

        await _match.StartAsync("wolves", "LIONS");

        wolves.Count(w => w.MatchTeam == ClanMatchService.TeamA).Should().Be(ClanMatchService.MaxFighters);
        lion.MatchTeam.Should().Be(ClanMatchService.TeamB);
        lion.ZoneId.Should().Be(ClanMatchService.Zone);
        _match.Running.Should().BeTrue();
    }

    [Fact]
    public async Task OnlyTheTwoTeamsFightEachOther()
    {
        var wolf = Member(1, Wolves);
        var mate = Member(2, Wolves);
        var lion = Member(3, Lions);
        await _match.StartAsync("Wolves", "Lions");

        PvpRules.IsEnemy(wolf, lion).Should().BeTrue();
        PvpRules.IsEnemy(wolf, mate).Should().BeFalse();
    }

    [Fact]
    public async Task TheTeamWithMoreKillsWinsWhenTimeRunsOut()
    {
        var wolf = Member(1, Wolves);
        var lion = Member(2, Lions);
        await _match.StartAsync("Wolves", "Lions");

        await _match.OnPlayerDiedAsync(lion, wolf);
        for (int i = 0; i < ClanMatchService.MatchSeconds; i++)
            await _match.TickAsync();

        var result = Sent(wolf).Last(p => p.ReadByte() == ClanMatchPacketWriter.BattleSub && p.ReadByte() == ClanMatchPacketWriter.ResultSub);
        result.ResetOffset();
        result.ReadByte();
        result.ReadByte();
        result.ReadShort().Should().Be(Wolves);
        _match.Running.Should().BeFalse();
    }

    [Fact]
    public async Task EveryoneGoesHomeAfterTheBanner()
    {
        var wolf = Member(1, Wolves);
        Member(2, Lions);
        await _match.StartAsync("Wolves", "Lions");
        await _match.StopAsync();

        for (int i = 0; i < ClanMatchService.SendHomeSeconds; i++)
            await _match.TickAsync();

        wolf.ZoneId.Should().Be((byte)ZoneId.Moradon);
        wolf.MatchTeam.Should().Be(ClanMatchService.NoTeam);
    }

    [Fact]
    public void TheRosterRecordFollowsThe2619Layout()
    {
        var packet = ClanMatchPacketWriter.Roster([new ClanMatchPacketWriter.Fighter(Wolves, 7, "Velkan", 900, 800, 105, 400, 300, 1)]);
        packet.ResetOffset();

        packet.ReadByte().Should().Be(ClanMatchPacketWriter.BattleSub);
        packet.ReadByte().Should().Be(ClanMatchPacketWriter.RosterSub);
        packet.ReadUShort().Should().Be(1);
        packet.ReadShort().Should().Be(Wolves);
        packet.ReadInt().Should().Be(7);
        packet.ReadString().Should().Be("Velkan");
        packet.ReadShort().Should().Be(900);
        packet.ReadShort().Should().Be(800);
        packet.ReadByte();
        packet.ReadShort().Should().Be(105);
        packet.ReadShort().Should().Be(400);
        packet.ReadShort().Should().Be(300);
        packet.ReadByte().Should().Be(1);
    }
}
