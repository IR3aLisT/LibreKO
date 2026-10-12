using FluentAssertions;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol;
using LibreKO.Game.World;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace LibreKO.Game.Tests;

public class PartyRejoinForgetTests : GameTestBase
{
    private const byte Moradon = 21;

    [Fact]
    public async Task AKickedMemberIsForgottenByTheLeader()
    {
        using var provider = CreateProvider(_ => { });
        var (leader, sent, member) = ThreeMemberParty(provider);

        await provider.GetRequiredService<IPartyPacketCoordinator>().RemoveMemberAsync(leader, (short)member.CharacterId);

        ForgetIds(sent).Should().ContainSingle().Which.Should().Be(member.CharacterId);
    }

    [Fact]
    public async Task AMemberWhoDisconnectsIsNotForgotten()
    {
        using var provider = CreateProvider(_ => { });
        var (_, sent, member) = ThreeMemberParty(provider);

        await provider.GetRequiredService<IPartyPacketCoordinator>().LeaveGameAsync(member);

        ForgetIds(sent).Should().BeEmpty();
    }

    private static (UserSession Leader, List<Packet> Sent, UserSession Member) ThreeMemberParty(ServiceProvider provider)
    {
        var sessions = provider.GetRequiredService<SessionManager>();
        var leaderPackets = new List<Packet>();
        var leader = Player(sessions, 910, leaderPackets);
        var member = Player(sessions, 911, new List<Packet>());
        var third = Player(sessions, 912, new List<Packet>());
        var party = sessions.Parties.CreateParty((short)leader.CharacterId);
        party.MemberIds[1] = (short)member.CharacterId;
        party.MemberIds[2] = (short)third.CharacterId;
        foreach (var session in new[] { leader, member, third }) session.PartyIndex = party.Index;
        leader.IsPartyLeader = true;
        return (leader, leaderPackets, member);
    }

    private static UserSession Player(SessionManager sessions, int characterId, List<Packet> sent)
    {
        var client = Substitute.For<IClient>();
        client.Id.Returns(Guid.NewGuid());
        client.SendPacket(Arg.Do<Packet>(sent.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var player = sessions.CreateSession(client, characterId, characterId + 100);
        player.Name = $"Player{characterId}";
        player.Nation = AccountNation.Karus;
        player.ZoneId = Moradon;
        return player;
    }

    private static List<int> ForgetIds(List<Packet> sent)
    {
        var ids = new List<int>();
        foreach (var packet in sent)
        {
            packet.ResetOffset();
            if (packet.RemainingBytes < 5 || packet.ReadByte() != (byte)PartySubOpcode.RejoinForget) continue;
            ids.Add(packet.ReadInt());
        }
        return ids;
    }
}
