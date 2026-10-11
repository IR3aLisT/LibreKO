using FluentAssertions;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol.Writers;
using Xunit;

namespace LibreKO.Game.Tests;

public class ItemAnnouncementTests
{
    [Fact]
    public void TheAnnouncementNamesTheItemAndTheWinner()
    {
        var packet = EffectPacketWriter.ItemAnnouncement(389010000, "Veteran");

        packet.GetOpcode().Should().Be((byte)GameOpcodes.GS_EFFECT);
        packet.ResetOffset();
        packet.ReadInt().Should().Be(EffectPacketWriter.AnnouncementEntity);
        packet.ReadInt().Should().Be(389010000);
        packet.ReadSByteString().Should().Be("Veteran");
    }
}
