using FluentAssertions;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol;
using LibreKO.Game.Protocol.Writers;
using Xunit;

namespace LibreKO.Game.Tests;

public class PartyPacketWriterTests
{
    private static PartyPacketWriter.MemberState Member => new(
        CharacterId: 70_001, Name: "Aurelia", Level: 62, Class: 105,
        MaxHp: 1800, Hp: 1750, MaxMp: 900, Mp: 880);

    [Fact]
    public void MemberInfo_MatchesTheRetailRecord()
    {
        var packet = PartyPacketWriter.MemberInfo(Member);
        packet.ResetOffset();

        packet.GetOpcode().Should().Be((byte)GameOpcodes.GS_PARTY);
        packet.ReadByte().Should().Be((byte)PartySubOpcode.MemberInfo);
        packet.ReadShort().Should().Be(PartyPacketWriter.StatusMemberRecord);
        packet.ReadInt().Should().Be(70_001);
        packet.ReadByte().Should().Be(PartyPacketWriter.MemberJoined);
        packet.ReadString().Should().Be("Aurelia");
        packet.ReadShort().Should().Be(1800);
        packet.ReadShort().Should().Be(1750);
        packet.ReadByte().Should().Be(62);
        packet.ReadShort().Should().Be(105);
        packet.ReadShort().Should().Be(900);
        packet.ReadShort().Should().Be(880);
        packet.ReadByte().Should().Be(0);
        packet.ReadByte().Should().Be(0);
        packet.ReadInt().Should().Be(PartyPacketWriter.NoTargetMark);
        packet.ReadByte().Should().Be(0);
        packet.ReadByte().Should().Be(0);
        packet.RemainingBytes.Should().Be(0);
    }

    [Fact]
    public void MemberInfoLeadsWithAStatusNotACharacterId()
    {
        var packet = PartyPacketWriter.MemberInfo(Member);
        packet.ResetOffset();
        packet.ReadByte();

        packet.ReadShort().Should().Be(1);
    }

    [Fact]
    public void RejectionSharesSubTwoAndCarriesANegativeStatus()
    {
        var packet = PartyPacketWriter.Rejected(-3);
        packet.ResetOffset();

        packet.ReadByte().Should().Be((byte)PartySubOpcode.MemberInfo);
        packet.ReadShort().Should().Be(-3);
        packet.RemainingBytes.Should().Be(0);
    }

    [Fact]
    public void PromotionReusesMemberInfoWithTheLeaderStatusCode()
    {
        var packet = PartyPacketWriter
            .MemberInfo(Member, PartyPacketWriter.MemberPromotedToLeader);
        packet.ResetOffset();
        packet.ReadByte();
        packet.ReadShort();
        packet.ReadInt();

        packet.ReadByte().Should().Be(PartyPacketWriter.MemberPromotedToLeader);
    }

    [Fact]
    public void InviteIsSubTwoWithAFullWidthCharacterId()
    {
        var packet = PartyPacketWriter.Invite(70_001, "Aurelia");
        packet.ResetOffset();

        packet.ReadByte().Should().Be((byte)PartySubOpcode.Invite);
        packet.ReadInt().Should().Be(70_001);
        packet.ReadString().Should().Be("Aurelia");
        packet.RemainingBytes.Should().Be(0);
    }

    [Fact]
    public void MemberLeftIsSubFour()
    {
        var packet = PartyPacketWriter.MemberLeft(70_001);
        packet.ResetOffset();

        packet.ReadByte().Should().Be((byte)PartySubOpcode.MemberLeft);
        packet.ReadInt().Should().Be(70_001);
        packet.RemainingBytes.Should().Be(0);
    }

    [Fact]
    public void DisbandIsSubFiveAndCarriesNoPayload()
    {
        var packet = PartyPacketWriter.Disband();
        packet.ResetOffset();

        packet.ReadByte().Should().Be((byte)PartySubOpcode.Disband);
        packet.RemainingBytes.Should().Be(0);
    }

    [Fact]
    public void VitalsIsSubSixWithFourShorts()
    {
        var packet = PartyPacketWriter.VitalsChange(70_001, 1800, 1750, 900, 880);
        packet.ResetOffset();

        packet.ReadByte().Should().Be((byte)PartySubOpcode.VitalsChange);
        packet.ReadInt().Should().Be(70_001);
        packet.ReadShort().Should().Be(1800);
        packet.ReadShort().Should().Be(1750);
        packet.ReadShort().Should().Be(900);
        packet.ReadShort().Should().Be(880);
        packet.RemainingBytes.Should().Be(0);
    }

    [Fact]
    public void LevelChangeCarriesOnlyTheLevel()
    {
        var packet = PartyPacketWriter.LevelChange(70_001, 62);
        packet.ResetOffset();

        packet.ReadByte().Should().Be((byte)PartySubOpcode.LevelChange);
        packet.ReadInt().Should().Be(70_001);
        packet.ReadByte().Should().Be(62);
        packet.RemainingBytes.Should().Be(0);
    }

    [Fact]
    public void ClassChangeIsSubEight()
    {
        var packet = PartyPacketWriter.ClassChange(70_001, 105);
        packet.ResetOffset();

        packet.ReadByte().Should().Be((byte)PartySubOpcode.ClassChange);
        packet.ReadInt().Should().Be(70_001);
        packet.ReadShort().Should().Be(105);
        packet.RemainingBytes.Should().Be(0);
    }

    [Fact]
    public void StatusEffectIsSubNine()
    {
        var packet = PartyPacketWriter.StatusEffect(70_001, 4, applied: true);
        packet.ResetOffset();

        packet.ReadByte().Should().Be((byte)PartySubOpcode.StatusEffect);
        packet.ReadInt().Should().Be(70_001);
        packet.ReadByte().Should().Be(4);
        packet.ReadByte().Should().Be(1);
        packet.RemainingBytes.Should().Be(0);
    }

    [Fact]
    public void CommanderIsSub30WithATwoByteNamePrefix()
    {
        var packet = PartyPacketWriter.Commander(70_001, "Aurelia");
        packet.ResetOffset();

        packet.ReadByte().Should().Be(30);
        packet.ReadInt().Should().Be(70_001);
        packet.ReadShort().Should().Be(7);
        packet.ReadBytes(7).Should().Equal("Aurelia"u8.ToArray());
        packet.RemainingBytes.Should().Be(0);
    }

    [Fact]
    public void TheCommanderMarkIsSub31WithTheTargetAndAClearFlag()
    {
        var packet = PartyPacketWriter.CommanderMark(70_001, clear: true);
        packet.ResetOffset();

        packet.ReadByte().Should().Be(31);
        packet.ReadInt().Should().Be(70_001);
        packet.ReadByte().Should().Be(1);
        packet.RemainingBytes.Should().Be(0);
    }

    [Fact]
    public void TheCommanderOrderIsSub32()
    {
        var packet = PartyPacketWriter.CommanderOrder(3);
        packet.ResetOffset();

        packet.ReadByte().Should().Be(32);
        packet.ReadByte().Should().Be(3);
        packet.RemainingBytes.Should().Be(0);
    }

    [Fact]
    public void MemberBuffsIsSub64WithTheMemberAndEachSkillId()
    {
        var packet = PartyPacketWriter.MemberBuffs(70_001, [500_049, 107_725]);

        packet.GetOpcode().Should().Be((byte)GameOpcodes.GS_PARTY);
        Convert.ToHexString(packet.GetData()).Should().Be("40711101000251A10700CDA40100");
    }

    [Fact]
    public void MemberBuffsWithNoBuffsCarriesAZeroCount()
    {
        var packet = PartyPacketWriter.MemberBuffs(70_001, []);

        Convert.ToHexString(packet.GetData()).Should().Be("407111010000");
    }

    [Fact]
    public void MemberBuffsStopsAtTheLargestCountAByteHolds()
    {
        var skillIds = Enumerable.Range(1, PartyPacketWriter.MaxMemberBuffs + 10).ToArray();

        var packet = PartyPacketWriter.MemberBuffs(70_001, skillIds);
        packet.ResetOffset();

        packet.ReadByte().Should().Be((byte)PartySubOpcode.MemberBuffs);
        packet.ReadInt().Should().Be(70_001);
        packet.ReadByte().Should().Be(PartyPacketWriter.MaxMemberBuffs);
        for (var index = 1; index <= PartyPacketWriter.MaxMemberBuffs; index++)
            packet.ReadInt().Should().Be(index);
        packet.RemainingBytes.Should().Be(0);
    }

    [Fact]
    public void SignalProbeIsSub65WithTheToken()
    {
        var packet = PartyPacketWriter.SignalProbe(0x0A0B0C0D);

        packet.GetOpcode().Should().Be((byte)GameOpcodes.GS_PARTY);
        Convert.ToHexString(packet.GetData()).Should().Be("410D0C0B0A");
    }

    [Fact]
    public void SignalTableIsSub66WithEachMembersRoundTrip()
    {
        var packet = PartyPacketWriter.SignalTable(
        [
            new PartyPacketWriter.SignalEntry(70_001, 80),
            new PartyPacketWriter.SignalEntry(70_002, PartyPacketWriter.NoSignal),
        ]);

        packet.GetOpcode().Should().Be((byte)GameOpcodes.GS_PARTY);
        Convert.ToHexString(packet.GetData()).Should().Be("420271110100500072110100FFFF");
    }

    [Fact]
    public void SignalTableWithNoEntriesCarriesAZeroCount()
    {
        Convert.ToHexString(PartyPacketWriter.SignalTable([]).GetData()).Should().Be("4200");
    }

    [Fact]
    public void LevelProgressIsSub67WithTheMemberAndThePermille()
    {
        var packet = PartyPacketWriter.LevelProgress(70_001, 375);

        packet.GetOpcode().Should().Be((byte)GameOpcodes.GS_PARTY);
        Convert.ToHexString(packet.GetData()).Should().Be("43711101007701");
    }
}
