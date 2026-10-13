using FluentAssertions;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol;
using LibreKO.Game.Protocol.Writers;
using Xunit;

namespace LibreKO.Game.Tests;

public class AchievementTitleAnswerTests
{
    [Fact]
    public void ASelectAnswerCarriesTheTitleThenTheResult()
    {
        var packet = AchievementPacketWriter.TitleSelected(AchievementSubOpcode.SelectDisplayTitle, 42, AchievementPacketWriter.TitleDone);
        packet.ResetOffset();

        packet.GetOpcode().Should().Be((byte)GameOpcodes.GS_ACHIEVEMENT);
        packet.ReadByte().Should().Be((byte)AchievementSubOpcode.SelectDisplayTitle);
        packet.ReadUShort().Should().Be(0);
        packet.ReadUShort().Should().Be(42);
        packet.ReadShort().Should().Be(AchievementPacketWriter.TitleDone);
        packet.RemainingBytes.Should().Be(0);
    }

    [Fact]
    public void AResetAnswerIsOnlyTheResult()
    {
        var packet = AchievementPacketWriter.TitleReset(AchievementSubOpcode.ResetDisplayTitle, AchievementPacketWriter.TitleDone);
        packet.ResetOffset();

        packet.ReadByte().Should().Be((byte)AchievementSubOpcode.ResetDisplayTitle);
        packet.ReadShort().Should().Be(AchievementPacketWriter.TitleDone);
        packet.RemainingBytes.Should().Be(0);
    }
}
