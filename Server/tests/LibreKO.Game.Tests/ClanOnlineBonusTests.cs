using FluentAssertions;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;
using NSubstitute;
using Xunit;

namespace LibreKO.Game.Tests;

public class ClanOnlineBonusTests
{
    private const short ClanId = 7;

    [Theory]
    [InlineData(4, 0, 0)]
    [InlineData(5, 20, 1)]
    [InlineData(9, 24, 1)]
    [InlineData(10, 25, 2)]
    [InlineData(35, 50, 4)]
    [InlineData(50, 65, 6)]
    [InlineData(80, 65, 6)]
    public void TheBonusStartsAtFiveOnlineAndCapsAtTheRetailLimits(int online, int expPercent, int np)
    {
        ClanOnlineBonus.ExpPercent(online).Should().Be(expPercent);
        ClanOnlineBonus.Np(online).Should().Be(np);
    }

    [Fact]
    public void TheBonusPacketCarriesTheOnlineCount()
    {
        var packet = KnightsPacketWriter.ClanBonus(12);
        packet.ResetOffset();
        packet.GetOpcode().Should().Be((byte)GameOpcodes.GS_KNIGHTS_PROCESS);
        packet.ReadByte().Should().Be((byte)KnightsSubOpcode.ClanBonus);
        packet.ReadByte().Should().Be(KnightsPacketWriter.Succeeded);
        packet.ReadByte().Should().Be(12);
        packet.RemainingBytes.Should().Be(0);
    }

    [Fact]
    public async Task ARefreshCountsTheOnlineMembersAndLeavesTheLeaverOut()
    {
        var sessions = new SessionManager();
        var members = Enumerable.Range(1, 6).Select(id => Member(sessions, id)).ToList();
        var outsider = Member(sessions, 99, clan: 0);
        var runtime = new KnightsRuntimeService(sessions);

        await runtime.RefreshClanBonusAsync(ClanId, leaving: members[0]);

        members.Skip(1).Should().OnlyContain(m => m.ClanOnlineCount == 5);
        outsider.ClanOnlineCount.Should().Be(0);
    }

    private static UserSession Member(SessionManager sessions, int id, short clan = ClanId)
    {
        var client = Substitute.For<IClient>();
        client.Id.Returns(Guid.NewGuid());
        client.SendPacket(Arg.Any<Packet>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var session = sessions.CreateSession(client, id, id);
        session.Name = $"m{id}";
        session.KnightsId = clan;
        return session;
    }
}
