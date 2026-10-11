using FluentAssertions;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol;
using LibreKO.Game.Protocol.Writers;
using Xunit;

namespace LibreKO.Game.Tests;

public class NationPacketWriterTests
{
    private const byte SiegeSub = 3;
    private const byte WarStatusSub = 2;
    private const byte RankTypePkZone = 1;

    [Fact]
    public void TheWarStatusIsThe2619Shape()
    {
        var packet = SiegePacketWriter.WarStatus(2, 42, 7, 40, 840, 3600, "Wolves");
        packet.ResetOffset();

        packet.GetOpcode().Should().Be((byte)GameOpcodes.GS_SIEGE);
        packet.ReadByte().Should().Be(WarStatusSub);
        packet.ReadByte().Should().Be(2);
        packet.ReadShort().Should().Be(42);
        packet.ReadShort().Should().Be(7);
        packet.ReadShort().Should().Be(40);
        packet.ReadInt().Should().Be(840);
        packet.ReadShort().Should().Be(3600);
        packet.ReadSByteString().Should().Be("Wolves");
        packet.RemainingBytes.Should().Be(0);
    }

    [Fact]
    public void TheSiegeScheduleListsEachWarAsTypeWeekdayHourMinute()
    {
        var packet = SiegePacketWriter.Schedule([new SiegePacketWriter.WarSchedule(2, 6, 20, 30)]);
        packet.ResetOffset();

        packet.ReadByte().Should().Be(SiegeSub);
        packet.ReadByte().Should().Be(2);
        packet.ReadShort().Should().Be(1);
        packet.ReadByte().Should().Be(1);
        packet.ReadByte().Should().Be(2);
        packet.ReadByte().Should().Be(6);
        packet.ReadByte().Should().Be(20);
        packet.ReadByte().Should().Be(30);
        packet.RemainingBytes.Should().Be(0);
    }

    [Fact]
    public void ATariffChangeCarriesTheRateAndTheZoneAsShorts()
    {
        var packet = SiegePacketWriter.TariffChanged(4, 15, 21);
        packet.ResetOffset();

        packet.ReadByte().Should().Be(4);
        packet.ReadByte().Should().Be(4);
        packet.ReadShort().Should().Be(1);
        packet.ReadShort().Should().Be(15);
        packet.ReadShort().Should().Be(21);
        packet.RemainingBytes.Should().Be(0);
    }

    [Fact]
    public void PkZoneRankWritesBothNationsThenTheCallersOwnRank()
    {
        var karus = new[] { new RankPacketWriter.RankEntry("Aurelia", 1, 42, 7, "Wolves", 500, 12, 4) };
        var packet = RankPacketWriter.PkZone(RankTypePkZone, karus, [], 3, 500, 9);
        packet.ResetOffset();

        packet.GetOpcode().Should().Be((byte)GameOpcodes.GS_RANK);
        packet.ReadByte().Should().Be(RankTypePkZone);
        packet.ReadUShort().Should().Be(1);
        packet.ReadString().Should().Be("Aurelia");
        packet.ReadByte().Should().Be(1);
        packet.ReadUShort().Should().Be(42);
        packet.ReadUShort().Should().Be(7);
        packet.ReadString().Should().Be("Wolves");
        packet.ReadInt().Should().Be(500);
        packet.ReadUShort().Should().Be(12);
        packet.ReadByte().Should().Be(4);
        packet.ReadUShort().Should().Be(0);
        packet.ReadUShort().Should().Be(3);
        packet.ReadInt().Should().Be(500);
        packet.ReadUShort().Should().Be(9);
        packet.RemainingBytes.Should().Be(0);
    }

    [Fact]
    public void TheEmptyBorderDefenseWarBoardEndsWithTwoFourByteTrailers()
    {
        var packet = RankPacketWriter.BorderDefenseWar(2, [], [], 0, 0);
        packet.ResetOffset();
        packet.ReadByte();
        packet.ReadUShort().Should().Be(0);
        packet.ReadUShort().Should().Be(0);
        packet.ReadInt().Should().Be(0);
        packet.ReadInt().Should().Be(0);
        packet.RemainingBytes.Should().Be(0);
    }

    [Fact]
    public void BorderDefenseWarRowsCarryNameNationClanAndPointsThenTheExp()
    {
        var karus = new[] { new RankPacketWriter.RankEntry("Rikka", 1, 12, 3, "Olympus", 21) };
        var elmorad = new[] { new RankPacketWriter.RankEntry("Zeus", 2, 0, 0, "", 7) };

        var packet = RankPacketWriter.BorderDefenseWar(2, karus, elmorad, 150000, 0);
        packet.ResetOffset();

        packet.ReadByte().Should().Be(2);
        packet.ReadUShort().Should().Be(1);
        packet.ReadString().Should().Be("Rikka");
        packet.ReadByte().Should().Be(1);
        packet.ReadUShort().Should().Be(12);
        packet.ReadUShort().Should().Be(3);
        packet.ReadString().Should().Be("Olympus");
        packet.ReadInt().Should().Be(21);
        packet.ReadUShort().Should().Be(1);
        packet.ReadString().Should().Be("Zeus");
        packet.ReadByte().Should().Be(2);
        packet.ReadUShort().Should().Be(0);
        packet.ReadUShort().Should().Be(0);
        packet.ReadString().Should().Be("");
        packet.ReadInt().Should().Be(7);
        packet.ReadUInt().Should().Be(150000u);
        packet.ReadUInt().Should().Be(0u);
        packet.RemainingBytes.Should().Be(0);
    }

    [Fact]
    public void ChaosDungeonRankCarriesRowsThenTwoExpFields()
    {
        var packet = RankPacketWriter.ChaosDungeon(3, [new RankPacketWriter.ChaosRow("Zeus", 7, 2)], 1500, 3000);
        packet.ResetOffset();
        packet.ReadByte().Should().Be(3);
        packet.ReadByte().Should().Be(1);
        packet.ReadString().Should().Be("Zeus");
        packet.ReadShort().Should().Be(7);
        packet.ReadShort().Should().Be(2);
        packet.ReadUInt().Should().Be(1500u);
        packet.ReadUInt().Should().Be(3000u);
        packet.RemainingBytes.Should().Be(0);
    }

    [Fact]
    public void BifrostCarriesSecondsRemaining()
    {
        var packet = BifrostPacketWriter.Remaining(TempleSubOpcode.BifrostRemaining, 600);
        packet.ResetOffset();

        packet.GetOpcode().Should().Be((byte)GameOpcodes.GS_BIFROST);
        packet.ReadByte().Should().Be((byte)TempleSubOpcode.BifrostRemaining);
        packet.ReadInt().Should().Be(600);
        packet.ReadByte().Should().Be(0);
        packet.RemainingBytes.Should().Be(0);
    }
}
