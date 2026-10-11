using FluentAssertions;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace LibreKO.Game.Tests;

public class UserOptionTests : GameTestBase
{
    private const byte Moradon = 21;
    private const byte Hide = 0;
    private const byte Show = 1;

    private static (UserSession Session, List<Packet> Sent, IClient Client) Player(ServiceProvider provider, int id, string name)
    {
        var client = Substitute.For<IClient>();
        client.Id.Returns(Guid.NewGuid());
        var sent = new List<Packet>();
        client.SendPacket(Arg.Do<Packet>(sent.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var sessions = provider.GetRequiredService<SessionManager>();
        var session = sessions.CreateSession(client, characterId: id, accountId: id + 1000);
        session.Name = name;
        session.Level = 60;
        session.Nation = AccountNation.ElMorad;
        session.ZoneId = Moradon;
        session.X = 800;
        session.Z = 400;
        session.Hp = 100;
        session.MaxHp = 100;
        sessions.Regions.AddToRegion(session);
        return (session, sent, client);
    }

    private static Packet RankAuraRequest(byte show)
    {
        var request = new Packet(GameOpcodes.GS_USER_OPTION);
        request.WriteByte(UserOptionPacketWriter.SubRankAura);
        request.WriteByte(show);
        request.ResetOffset();
        return request;
    }

    private static List<(int Id, bool Shown)> RankAuraPackets(List<Packet> sent) => sent
        .Where(p => p.GetOpcode() == (byte)GameOpcodes.GS_USER_OPTION)
        .Select(p =>
        {
            p.ResetOffset();
            p.ReadByte().Should().Be(UserOptionPacketWriter.SubRankAura);
            return (p.ReadInt(), p.ReadByte() != 0);
        })
        .ToList();

    [Fact]
    public async Task HidingTheRankAuraTellsTheOthersAroundButNotThePlayer()
    {
        using var provider = CreateProvider(_ => { });
        var (ranker, rankerSent, rankerClient) = Player(provider, 1, "Ranker");
        var (_, watcherSent, _) = Player(provider, 2, "Watcher");
        var coordinator = new UserOptionPacketCoordinator(provider.GetRequiredService<SessionManager>());

        await coordinator.HandleAsync(rankerClient, RankAuraRequest(Hide));
        await coordinator.HandleAsync(rankerClient, RankAuraRequest(Hide));

        ranker.RankAuraHidden.Should().BeTrue();
        RankAuraPackets(watcherSent).Should().Equal((1, false));
        RankAuraPackets(rankerSent).Should().BeEmpty();

        await coordinator.HandleAsync(rankerClient, RankAuraRequest(Show));
        ranker.RankAuraHidden.Should().BeFalse();
        RankAuraPackets(watcherSent).Should().Equal((1, false), (1, true));
    }

    [Fact]
    public async Task AHiddenAuraFollowsTheUserRecordWhenSomeoneAsksForIt()
    {
        using var provider = CreateProvider(_ => { });
        var (ranker, _, _) = Player(provider, 1, "Ranker");
        var (_, watcherSent, watcherClient) = Player(provider, 2, "Watcher");
        ranker.RankAuraHidden = true;

        var request = new Packet(GameOpcodes.GS_REQ_USERIN);
        request.ResetOffset();
        await provider.GetRequiredService<IWorldVisibilityService>().HandleReqUserInAsync(watcherClient, request);

        RankAuraPackets(watcherSent).Should().Equal((1, false));
    }
}
