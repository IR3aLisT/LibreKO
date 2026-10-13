using FluentAssertions;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol;
using LibreKO.Game.World;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace LibreKO.Game.Tests;

public class TransformThroughGateTests : GameTestBase
{
    private const byte Moradon = 21;
    private const byte ArrivedSub = 2;
    private const int TransformSkill = 450001;

    [Fact]
    public async Task ATransformationIsShownAgainOnArrival()
    {
        using var provider = CreateProvider(_ => { });
        var sessions = provider.GetRequiredService<SessionManager>();
        var client = Substitute.For<IClient>();
        client.Id.Returns(Guid.NewGuid());
        var sent = new List<Packet>();
        client.SendPacket(Arg.Do<Packet>(p => sent.Add(p)), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var player = sessions.CreateSession(client, 1, 1);
        player.ZoneId = Moradon;
        player.Hp = 100;
        player.TransformSkillId = TransformSkill;
        sessions.Regions.AddToRegion(player);

        var arrived = new Packet(GameOpcodes.GS_ZONE_CHANGE);
        arrived.WriteByte(ArrivedSub);
        arrived.ResetOffset();
        await provider.GetRequiredService<IWorldMovementService>().HandleZoneChangeAsync(client, arrived);

        var state = sent.Single(p => p.GetOpcode() == (byte)GameOpcodes.GS_STATE_CHANGE);
        state.ResetOffset();
        state.ReadInt().Should().Be(1);
        state.ReadByte().Should().Be((byte)StateChangeType.Transformation);
        state.ReadInt().Should().Be(TransformSkill);
    }
}
