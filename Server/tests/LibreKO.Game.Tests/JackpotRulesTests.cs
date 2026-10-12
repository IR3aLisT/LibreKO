using FluentAssertions;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;

namespace LibreKO.Game.Tests;

public class JackpotRulesTests
{
    private static readonly int[] Rates = [1100, 200, 50, 25, 10, 5];

    [Theory]
    [InlineData(1, 2)]
    [InlineData(1100, 2)]
    [InlineData(1101, 10)]
    [InlineData(1350, 50)]
    [InlineData(1390, 1000)]
    [InlineData(1391, JackpotRules.None)]
    [InlineData(10000, JackpotRules.None)]
    public void TheRollWalksTheCumulativeRates(int roll, int times) => JackpotRules.Times(Rates, roll).Should().Be(times);

    [Fact]
    public void TheExpJackpotIsUnitThenMultiplier()
    {
        var packet = ExperiencePacketWriter.Jackpot(1234, 50);
        packet.ResetOffset();
        packet.ReadByte().Should().Be(ExperiencePacketWriter.JackpotSub);
        packet.ReadInt().Should().Be(1234);
        packet.ReadUShort().Should().Be(50);
        packet.RemainingBytes.Should().Be(0);
    }

    [Fact]
    public void TheCoinJackpotCarriesTheMultiplierAndUnitAfterTheTotals()
    {
        var packet = GoldChangePacketWriter.Multiplied(5000, 90000, 10, 1234);
        packet.ResetOffset();
        packet.ReadByte().Should().Be(GoldChangePacketWriter.Jackpot);
        packet.ReadInt().Should().Be(5000);
        packet.ReadInt().Should().Be(90000);
        packet.ReadUShort().Should().Be(10);
        packet.ReadInt().Should().Be(1234);
    }
}
