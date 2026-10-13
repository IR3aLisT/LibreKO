using FluentAssertions;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace LibreKO.Game.Tests;

public class OwnStatusTests
{
    [Fact]
    public async Task TheVictimHearsItsOwnStatusEvenOutsideAParty()
    {
        var sessions = new SessionManager();
        var client = Substitute.For<IClient>();
        client.Id.Returns(Guid.NewGuid());
        var sent = new List<Packet>();
        client.SendPacket(Arg.Do<Packet>(p => sent.Add(p)), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var victim = sessions.CreateSession(client, 1, 1);
        var notifications = new CombatNotificationService(sessions, Substitute.For<ILogger<CombatNotificationService>>());

        await notifications.SendPartyStatusUpdateAsync(victim, (byte)PartyStatusIcon.Poison, applied: true);

        var packet = sent.Single();
        packet.GetOpcode().Should().Be((byte)GameOpcodes.GS_ZONEABILITY);
        packet.ResetOffset();
        packet.ReadByte().Should().Be(ZoneChangePacketWriter.UserStatusSub);
        packet.ReadByte().Should().Be((byte)PartyStatusIcon.Poison);
        packet.ReadByte().Should().Be(1);
    }
}
