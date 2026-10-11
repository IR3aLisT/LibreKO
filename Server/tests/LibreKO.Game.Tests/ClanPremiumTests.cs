using FluentAssertions;
using LibreKO.Common.Domain.Entities;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol;
using LibreKO.Game.World;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace LibreKO.Game.Tests;

public class ClanPremiumTests : GameTestBase
{
    private const short Wolves = 71;
    private const int ThreeDays = 3 * 24 * 3600;

    [Fact]
    public async Task TheQueryAnswersWithTheTimeLeft()
    {
        var (session, sent, provider) = Member(DateTime.UtcNow.AddDays(3));

        await Query(provider, session);

        var reply = sent.Last();
        reply.ResetOffset();
        reply.ReadByte().Should().Be(KnightsBroadcastBuilders.ClanPremiumQuery);
        reply.ReadByte().Should().Be(KnightsBroadcastBuilders.ClanPremiumActive);
        ((int)reply.ReadUInt()).Should().BeCloseTo(ThreeDays, 60);
    }

    [Fact]
    public async Task AnExpiredPremiumReadsInactiveWithNoTime()
    {
        var (session, sent, provider) = Member(DateTime.UtcNow.AddDays(-1));

        await Query(provider, session);

        var reply = sent.Last();
        reply.ResetOffset();
        reply.ReadByte();
        reply.ReadByte().Should().Be(KnightsBroadcastBuilders.ClanPremiumInactive);
        reply.ReadUInt().Should().Be(0u);
    }

    private static Task Query(ServiceProvider provider, UserSession session)
    {
        var packet = new Packet(GameOpcodes.GS_CLAN_PREMIUM);
        packet.WriteByte(KnightsBroadcastBuilders.ClanPremiumQuery);
        packet.ResetOffset();
        return provider.GetRequiredService<IKnightsPacketCoordinator>().HandlePremiumAsync(session.Client, packet);
    }

    private static (UserSession Session, List<Packet> Sent, ServiceProvider Provider) Member(DateTime premiumEnd)
    {
        var provider = CreateProvider(_ => { });
        var sessions = provider.GetRequiredService<SessionManager>();
        sessions.Knights.AddClan(Wolves, new KnightsEntity { Id = Wolves, Name = "Wolves", Chief = "Alpha", Nation = 1, PremiumExpiry = premiumEnd });
        var client = Substitute.For<IClient>();
        client.Id.Returns(Guid.NewGuid());
        var sent = new List<Packet>();
        client.SendPacket(Arg.Do<Packet>(sent.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var session = sessions.CreateSession(client, characterId: 72, accountId: 82);
        session.KnightsId = Wolves;
        return (session, sent, provider);
    }
}
