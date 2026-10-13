using FluentAssertions;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.World;
using NSubstitute;
using Xunit;

namespace LibreKO.Game.Tests;

public class PartyLeaderMarkTests
{
    private const byte Moradon = 21;

    [Fact]
    public async Task BecomingOrLeavingTheLeadIsShownToPlayersAround()
    {
        var sessions = new SessionManager();
        var (leader, _) = Player(sessions, 1);
        var (watcher, seen) = Player(sessions, 2);
        sessions.Regions.AddToRegion(leader);
        sessions.Regions.AddToRegion(watcher);

        leader.IsPartyLeader = true;
        leader.IsPartyLeader = true;
        await Task.Delay(50);
        leader.IsPartyLeader = false;
        await Task.Delay(50);

        var states = seen.Where(p => p.GetOpcode() == (byte)GameOpcodes.GS_STATE_CHANGE).Select(Read).ToList();
        states.Should().Equal((1, (byte)StateChangeType.PartyLeader, 1), (1, (byte)StateChangeType.PartyLeader, 0));
    }

    private static (int, byte, int) Read(Packet packet)
    {
        packet.ResetOffset();
        return (packet.ReadInt(), packet.ReadByte(), packet.ReadInt());
    }

    private static (UserSession, List<Packet>) Player(SessionManager sessions, int characterId)
    {
        var client = Substitute.For<IClient>();
        client.Id.Returns(Guid.NewGuid());
        var sent = new List<Packet>();
        client.SendPacket(Arg.Do<Packet>(p => sent.Add(p)), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var session = sessions.CreateSession(client, characterId, characterId);
        session.ZoneId = Moradon;
        session.X = 800;
        session.Z = 530;
        return (session, sent);
    }
}
