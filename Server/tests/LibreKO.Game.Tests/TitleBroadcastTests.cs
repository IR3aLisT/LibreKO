using FluentAssertions;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol;
using LibreKO.Game.World;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace LibreKO.Game.Tests;

public class TitleBroadcastTests : GameTestBase
{
    private const short Title = 137;

    [Fact]
    public async Task OthersSeeTheNewTitleAsStateChangeFourteen()
    {
        using var provider = CreateProvider(_ => { });
        var sessions = provider.GetRequiredService<SessionManager>();
        var ownerSent = new List<Packet>();
        var watcherSent = new List<Packet>();
        var owner = Place(sessions, 500, ownerSent);
        Place(sessions, 600, watcherSent);
        owner.DisplayTitleId = Title;

        await provider.GetRequiredService<IWorldVisibilityService>().BroadcastDisplayTitleAsync(owner);

        ownerSent.Should().Contain(p => p.GetOpcode() == (byte)GameOpcodes.GS_ACHIEVEMENT);
        var state = watcherSent.Single(p => p.GetOpcode() == (byte)GameOpcodes.GS_STATE_CHANGE);
        state.ReadInt().Should().Be(500);
        state.ReadByte().Should().Be((byte)StateChangeType.Title);
        state.ReadInt().Should().Be(Title);
    }

    private static UserSession Place(SessionManager sessions, int characterId, List<Packet> sent)
    {
        var client = Substitute.For<IClient>();
        client.Id.Returns(Guid.NewGuid());
        client.SendPacket(Arg.Do<Packet>(sent.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var session = sessions.CreateSession(client, characterId: characterId, accountId: characterId + 1);
        session.Name = $"P{characterId}";
        session.ZoneId = 21;
        session.X = 800;
        session.Z = 400;
        sessions.Regions.AddToRegion(session);
        return session;
    }
}
