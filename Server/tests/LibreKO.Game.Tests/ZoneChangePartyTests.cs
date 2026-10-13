using FluentAssertions;
using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.World;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace LibreKO.Game.Tests;

public class ZoneChangePartyTests : GameTestBase
{
    private const byte Moradon = 21;

    private static (ServiceProvider Provider, SessionManager Sessions, UserSession[] Members) Arrange(int memberCount)
    {
        var provider = CreateProvider(
            _ => { },
            gameData => gameData.KingSystemTable.Returns(new Dictionary<byte, KingSystemData>()));
        var sessions = provider.GetRequiredService<SessionManager>();
        var members = new UserSession[memberCount];
        for (int i = 0; i < memberCount; i++)
        {
            var client = Substitute.For<IClient>();
            client.Id.Returns(Guid.NewGuid());
            client.SendPacket(Arg.Any<Packet>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            members[i] = sessions.CreateSession(client, characterId: 200 + i, accountId: 300 + i);
            members[i].ZoneId = Moradon;
            members[i].Nation = AccountNation.Karus;
        }

        var party = sessions.Parties.CreateParty((short)members[0].CharacterId);
        members[0].IsPartyLeader = true;
        for (int i = 0; i < memberCount; i++)
        {
            party.MemberIds[i] = (short)members[i].CharacterId;
            members[i].PartyIndex = party.Index;
        }
        return (provider, sessions, members);
    }

    [Fact]
    public async Task LeavingTheZoneLeavesTheParty()
    {
        var (provider, sessions, members) = Arrange(3);
        using (provider)
        {
            await provider.GetRequiredService<IZoneTransitionService>()
                .ChangeZoneAsync(members[2], (byte)ZoneId.Ardream, 100f, 100f);

            members[2].IsInParty.Should().BeFalse();
            members[0].IsInParty.Should().BeTrue();
            sessions.Parties.GetParty(members[0].PartyIndex)!.FindMember((short)members[2].CharacterId).Should().Be(-1);
        }
    }

    [Fact]
    public async Task ALeaderWhoLeavesTheZoneHandsThePartyOn()
    {
        var (provider, sessions, members) = Arrange(3);
        using (provider)
        {
            await provider.GetRequiredService<IZoneTransitionService>()
                .ChangeZoneAsync(members[0], (byte)ZoneId.Ardream, 100f, 100f);

            members[0].IsInParty.Should().BeFalse();
            members[1].IsPartyLeader.Should().BeTrue();
            sessions.Parties.GetParty(members[1].PartyIndex)!.MemberCount.Should().Be(2);
        }
    }

    [Fact]
    public async Task AWarpInsideTheZoneKeepsTheParty()
    {
        var (provider, _, members) = Arrange(2);
        using (provider)
        {
            await provider.GetRequiredService<IZoneTransitionService>()
                .ChangeZoneAsync(members[1], Moradon, 300f, 300f);

            members[1].IsInParty.Should().BeTrue();
        }
    }
}
