using FluentAssertions;
using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;
using NSubstitute;
using Xunit;

namespace LibreKO.Game.Tests;

public class FullMoonTests
{
    private const int BagSlot = 14;

    private static UserSession Member(int id, byte zone = (byte)ZoneId.Moradon, bool voucher = true, short hp = 100)
    {
        var session = new UserSession(Substitute.For<IClient>(), id, id + 1000) { ZoneId = zone, Hp = hp };
        for (int i = 0; i < session.Inventory.Length; i++) session.Inventory[i] = new ItemSlot();
        if (voucher)
        {
            session.Inventory[BagSlot].ItemId = FullMoonRules.VoucherItem;
            session.Inventory[BagSlot].Count = 1;
        }
        return session;
    }

    [Fact]
    public void EntryIsCheckedInThe2619Order()
    {
        var leader = Member(1);
        FullMoonRules.EntryRefusal(null, [], FullMoonRules.FullMoonBoard).Should().Be(FullMoonRules.NoParty);
        FullMoonRules.EntryRefusal(leader, [leader, Member(2)], FullMoonRules.NormalBoard).Should().Be(FullMoonRules.NotFullMoonParty);
        FullMoonRules.EntryRefusal(leader, [leader, Member(2, (byte)ZoneId.RonarkLand)], FullMoonRules.FullMoonBoard)
            .Should().Be(FullMoonRules.NotAllInMoradon);
        FullMoonRules.EntryRefusal(leader, [leader, Member(2, hp: 0)], FullMoonRules.FullMoonBoard).Should().Be(FullMoonRules.MemberDead);
        FullMoonRules.EntryRefusal(leader, [leader, Member(2, voucher: false)], FullMoonRules.FullMoonBoard)
            .Should().Be(FullMoonRules.MemberWithoutVoucher);
    }

    [Fact]
    public void AReadyPartyIsToldTheInstanceCouldNotBeMadeUntilTheDungeonExists()
    {
        var leader = Member(1);
        FullMoonRules.EntryRefusal(leader, [leader, Member(2)], FullMoonRules.FullMoonBoard).Should().Be(FullMoonRules.InstanceFailed);
    }

    [Theory]
    [InlineData(0, 0, 0, true, 21, 55, true)]
    [InlineData(2, 0, 0, true, 21, 55, false)]
    [InlineData(3, 0, 0, false, 21, 55, false)]
    [InlineData(0, 21, 0, false, 71, 55, false)]
    [InlineData(0, 0, 6, false, 21, 55, true)]
    [InlineData(0, 0, 6, false, 21, 61, false)]
    [InlineData(0, 0, 9, false, 21, 83, true)]
    public void TheNormalBoardFiltersByKindZoneAndLevelBand(byte state, short zone, byte level, bool party, short candidateZone,
        byte candidateLevel, bool passes) =>
        FullMoonRules.PassesFilter(state, zone, level, party, candidateZone, candidateLevel).Should().Be(passes);

    [Fact]
    public void ThePartyRecordCarriesTheMarkedTargetAndThePartyType()
    {
        var packet = PartyPacketWriter.MemberInfo(new PartyPacketWriter.MemberState(7, "Velkan", 60, 101, 900, 800, 500, 400),
            markedTarget: 42, partyType: FullMoonRules.FullMoonBoard);
        packet.ResetOffset();
        var bytes = packet.ReadBytes(packet.RemainingBytes);

        BitConverter.ToInt32(bytes, bytes.Length - 6).Should().Be(42);
        bytes[^2].Should().Be(FullMoonRules.FullMoonBoard);
    }
}
