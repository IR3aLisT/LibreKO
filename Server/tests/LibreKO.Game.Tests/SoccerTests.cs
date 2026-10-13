using FluentAssertions;
using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace LibreKO.Game.Tests;

public class SoccerTests
{
    private const byte Moradon = SoccerService.PitchZone;
    private const float NearBallZ = SoccerPitch.CentreZ - 2f;

    private sealed record Player(UserSession Session, IClient Client, List<Packet> Sent);

    private readonly SessionManager _sessions = new();
    private readonly IWorldMovementService _movement = Substitute.For<IWorldMovementService>();
    private readonly SoccerService _soccer;
    private readonly NpcInstance _ball;

    public SoccerTests()
    {
        var provider = new ServiceCollection().AddSingleton(_movement).BuildServiceProvider();
        _soccer = new SoccerService(_sessions, provider, NullLogger<SoccerService>.Instance);
        _ball = new NpcInstance
        {
            NpcId = SoccerService.BallNpcId, NpcType = NpcData.TypeSoccerBall, Name = "Football", IsMonster = true,
            ZoneId = Moradon, X = SoccerPitch.CentreX, Z = SoccerPitch.CentreZ,
        };
        _sessions.Regions.SpawnNpc(_ball);
    }

    private Player Join(int id, string name, byte zone = Moradon)
    {
        var client = Substitute.For<IClient>();
        client.Id.Returns(Guid.NewGuid());
        var sent = new List<Packet>();
        client.SendPacket(Arg.Do<Packet>(p => sent.Add(p)), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var session = _sessions.CreateSession(client, id, id);
        session.Name = name;
        session.ZoneId = zone;
        session.Nation = AccountNation.Karus;
        session.X = SoccerPitch.CentreX;
        session.Z = NearBallZ;
        _sessions.Regions.UpdateRegion(session);
        return new Player(session, client, sent);
    }

    private static IEnumerable<byte[]> SoccerPackets(Player player) => player.Sent
        .Where(p => p.GetOpcode() == (byte)GameOpcodes.GS_MINING)
        .Select(p => p.GetData())
        .Where(d => d.Length > 0 && d[0] == SoccerPacketWriter.MiningSub);

    private static int[] KickTo(float x, float z)
    {
        var data = new int[7];
        data[SoccerService.KickXSlot] = (int)x;
        data[SoccerService.KickZSlot] = (int)z;
        return data;
    }

    [Theory]
    [InlineData(672f, 112f, SoccerBallSpot.BlueScores)]
    [InlineData(672f, 203f, SoccerBallSpot.RedScores)]
    [InlineData(672f, 199.5f, SoccerBallSpot.RedScores)]
    [InlineData(690f, 205f, SoccerBallSpot.Out)]
    [InlineData(640f, 160f, SoccerBallSpot.Out)]
    [InlineData(672f, 160f, SoccerBallSpot.InPlay)]
    public void TheBallIsClassifiedByThePitchAndGoalMouths(float x, float z, SoccerBallSpot spot)
    {
        SoccerPitch.Classify(x, z).Should().Be(spot);
    }

    [Fact]
    public void AKickLandsNoFurtherThanTheLongestShot()
    {
        var (x, z) = SoccerPitch.KickLanding(672f, 160f, 672f, 200f);

        x.Should().Be(672f);
        z.Should().BeApproximately(160f + SoccerPitch.MaxKickReach, 0.001f);
        SoccerPitch.KickLanding(672f, 160f, 672f, 166f).Should().Be((672f, 166f));
    }

    [Fact]
    public void ThePacketsFollowThe2619Layouts()
    {
        SoccerPacketWriter.Goal(77, SoccerService.TeamBlue, SoccerService.TeamRed, 2, 1).GetData()
            .Should().Equal(16, 1, 77, 0, 0, 0, 1, 2, 2, 1);
        SoccerPacketWriter.Clock(600).GetData().Should().Equal(16, 2, 0x58, 0x02);
        SoccerPacketWriter.Sync(30, 3, 4).GetData().Should().Equal(16, 3, 0, 30, 0, 3, 4);
        SoccerPacketWriter.End(SoccerService.TeamRed, 1, 2).GetData().Should().Equal(16, 4, 2, 1, 2);
    }

    [Fact]
    public async Task JoiningSetsTheTeamWarpsToKickOffAndResetsTheBoard()
    {
        var rin = Join(1, "Rin");

        (await _soccer.JoinAsync(rin.Session, SoccerService.TeamBlue)).Should().Be(SoccerJoinResult.Joined);

        rin.Session.SoccerTeam.Should().Be(SoccerService.TeamBlue);
        SoccerPackets(rin).Should().ContainSingle().Which.Should().Equal(SoccerPacketWriter.Clock(0).GetData());
        rin.Sent.Should().Contain(p => p.GetOpcode() == (byte)GameOpcodes.GS_STATE_CHANGE);
        await _movement.Received(1).WarpAsync(rin.Session, (ushort)(SoccerPitch.CentreX * 10), (ushort)(SoccerPitch.BlueKickOffZ * 10));
    }

    [Fact]
    public async Task JoiningIsRefusedOutsideMoradonTwiceOrIntoAFullTeam()
    {
        var away = Join(1, "Away", zone: (byte)ZoneId.RonarkLand);
        (await _soccer.JoinAsync(away.Session, SoccerService.TeamRed)).Should().Be(SoccerJoinResult.WrongZone);

        for (int i = 0; i < SoccerService.MaxTeamPlayers; i++)
            await _soccer.JoinAsync(Join(10 + i, $"Red{i}").Session, SoccerService.TeamRed);
        var late = Join(50, "Late");
        (await _soccer.JoinAsync(late.Session, SoccerService.TeamRed)).Should().Be(SoccerJoinResult.TeamFull);
        (await _soccer.JoinAsync(late.Session, SoccerService.TeamBlue)).Should().Be(SoccerJoinResult.Joined);
        (await _soccer.JoinAsync(late.Session, SoccerService.TeamRed)).Should().Be(SoccerJoinResult.AlreadyIn);
    }

    [Fact]
    public async Task AMatchNeedsBothTeamsUnlessItIsPractice()
    {
        var rin = Join(1, "Rin");
        await _soccer.JoinAsync(rin.Session, SoccerService.TeamBlue);

        (await _soccer.StartAsync(rin.Session)).Should().BeFalse();
        var kai = Join(2, "Kai");
        await _soccer.JoinAsync(kai.Session, SoccerService.TeamRed);
        (await _soccer.StartAsync(rin.Session)).Should().BeTrue();
        _soccer.Running.Should().BeTrue();
        SoccerPackets(kai).Last().Should().Equal(SoccerPacketWriter.Clock(SoccerService.MatchSeconds).GetData());
    }

    [Fact]
    public async Task ABallKickedIntoTheRedGoalScoresForBlueAndNamesTheKicker()
    {
        var rin = Join(1, "Rin");
        await _soccer.JoinAsync(rin.Session, SoccerService.TeamBlue);
        await _soccer.StartAsync(rin.Session, practice: true);
        _ball.X = 672f;
        _ball.Z = 125f;
        rin.Session.Z = 128f;

        (await _soccer.KickAsync(rin.Session, _ball.UniqueId, KickTo(672f, 112f))).Should().BeTrue();
        rin.Sent.Clear();
        await _soccer.TickAsync();

        SoccerPackets(rin).Should().ContainSingle().Which.Should()
            .Equal(SoccerPacketWriter.Goal(rin.Session.CharacterId, SoccerService.TeamBlue, SoccerService.TeamRed, 1, 0).GetData());
        (_ball.X, _ball.Z).Should().Be((SoccerPitch.CentreX, SoccerPitch.CentreZ));
    }

    [Fact]
    public async Task OnlyAPlayerOnATeamNextToTheBallCanKickIt()
    {
        var rin = Join(1, "Rin");
        (await _soccer.KickAsync(rin.Session, _ball.UniqueId, KickTo(672f, 170f))).Should().BeFalse();

        await _soccer.JoinAsync(rin.Session, SoccerService.TeamBlue);
        rin.Session.Z = SoccerPitch.CentreZ + SoccerPitch.MaxKickerDistance * 2;
        (await _soccer.KickAsync(rin.Session, _ball.UniqueId, KickTo(672f, 170f))).Should().BeFalse();
    }

    [Fact]
    public async Task TheFinalWhistleSendsTheResultAndClearsTheTeams()
    {
        var rin = Join(1, "Rin");
        var kai = Join(2, "Kai");
        await _soccer.JoinAsync(rin.Session, SoccerService.TeamBlue);
        await _soccer.JoinAsync(kai.Session, SoccerService.TeamRed);
        await _soccer.StartAsync(rin.Session);
        rin.Sent.Clear();

        await _soccer.FinishAsync();

        SoccerPackets(rin).Should().ContainSingle().Which.Should().Equal(SoccerPacketWriter.End(SoccerService.Draw, 0, 0).GetData());
        rin.Session.SoccerTeam.Should().Be(SoccerService.TeamNone);
        _soccer.TeamOf(kai.Session.CharacterId).Should().Be(SoccerService.TeamNone);
        _soccer.Running.Should().BeFalse();
        await _movement.Received(1).WarpAsync(kai.Session, (ushort)(SoccerPitch.RedExitX * 10), (ushort)(SoccerPitch.RedExitZ * 10));
    }

    [Fact]
    public async Task AMatchWithOnePlayerLeftEndsOnTheNextTicks()
    {
        var rin = Join(1, "Rin");
        var kai = Join(2, "Kai");
        await _soccer.JoinAsync(rin.Session, SoccerService.TeamBlue);
        await _soccer.JoinAsync(kai.Session, SoccerService.TeamRed);
        await _soccer.StartAsync(rin.Session);

        await _soccer.LeaveAsync(kai.Session);
        await _soccer.TickAsync();

        _soccer.Running.Should().BeFalse();
    }

    [Fact]
    public void TheBallCannotBeHitAndHasNoAi()
    {
        _ball.IsAttackable.Should().BeFalse();
        _ball.HasAi.Should().BeFalse();
    }
}
