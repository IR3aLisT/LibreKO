using FluentAssertions;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol;
using LibreKO.Game.World;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace LibreKO.Game.Tests;

public class LookToggleTests : GameTestBase
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task TheHelmetAndCostumeFlagsAreStoredAndEchoedSeparately(bool hideHelmet, bool hideCospre)
    {
        using var provider = CreateProvider(_ => { });
        var sessions = provider.GetRequiredService<SessionManager>();
        var client = Substitute.For<IClient>();
        client.Id.Returns(Guid.NewGuid());
        var sent = new List<Packet>();
        client.SendPacket(Arg.Do<Packet>(sent.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var session = sessions.CreateSession(client, 600, 700);
        session.Name = "Zeus";
        session.Hp = 100;
        sessions.Regions.AddToRegion(session);

        var request = new Packet(GameOpcodes.GS_HELMET);
        request.WriteByte(hideHelmet ? (byte)1 : (byte)0);
        request.WriteByte(hideCospre ? (byte)1 : (byte)0);
        request.ResetOffset();
        await provider.GetRequiredService<ICharacterDevelopmentPacketCoordinator>().HandleHelmetAsync(client, request);

        session.IsHidingHelmet.Should().Be(hideHelmet);
        session.IsHidingCospre.Should().Be(hideCospre);
        var echo = sent.Should().ContainSingle(p => p.GetOpcode() == (byte)GameOpcodes.GS_HELMET).Subject;
        echo.ResetOffset();
        echo.ReadByte().Should().Be(hideHelmet ? (byte)1 : (byte)0);
        echo.ReadByte().Should().Be(hideCospre ? (byte)1 : (byte)0);
        echo.ReadInt().Should().Be(session.CharacterId);
    }
}
