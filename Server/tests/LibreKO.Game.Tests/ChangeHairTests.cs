using FluentAssertions;
using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;
using NSubstitute;
using Xunit;

namespace LibreKO.Game.Tests;

public class ChangeHairTests
{
    private const byte CouponBagPosition = 3;
    private const byte NewFace = 4;
    private const int NewHair = 0x05_AA_BB_CC;

    private static (ChangeHairPacketCoordinator Coordinator, UserSession Player, List<Packet> Sent, IMagicItemUsageService Items) Arrange(int couponId)
    {
        var sessions = new SessionManager();
        var client = Substitute.For<IClient>();
        client.Id.Returns(Guid.NewGuid());
        var sent = new List<Packet>();
        client.SendPacket(Arg.Do<Packet>(p => sent.Add(p)), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var player = sessions.CreateSession(client, 1, 1);
        player.Hp = 100;
        for (var i = 0; i < player.Inventory.Length; i++)
            player.Inventory[i] = new ItemSlot();
        player.Inventory[InventoryConstants.SlotMax + CouponBagPosition] = new ItemSlot { ItemId = couponId, Count = 1 };
        var items = Substitute.For<IMagicItemUsageService>();
        items.TryConsumeItemAsync(player, Arg.Any<int>(), Arg.Any<int>()).Returns(true);
        var coordinator = new ChangeHairPacketCoordinator(
            sessions, items, Substitute.For<ICharacterStatePersister>(), Substitute.For<IWorldVisibilityService>());
        return (coordinator, player, sent, items);
    }

    private static Packet Request(byte bagPosition)
    {
        var packet = new Packet(GameOpcodes.GS_CHANGE_HAIR);
        packet.WriteByte(ChangeHairPacketCoordinator.InGameSub);
        packet.WriteByte(bagPosition);
        packet.WriteByte(NewFace);
        packet.WriteInt(NewHair);
        packet.ResetOffset();
        return packet;
    }

    [Fact]
    public async Task ACouponPaysForTheNewLook()
    {
        var (coordinator, player, sent, items) = Arrange(ChangeHairPacketCoordinator.MakeOverCoupon);

        await coordinator.HandleAsync(player.Client, Request(CouponBagPosition));

        player.Face.Should().Be(NewFace);
        player.Hair.Should().Be(NewHair);
        await items.Received(1).TryConsumeItemAsync(player, ChangeHairPacketCoordinator.MakeOverCoupon, Arg.Any<int>());
        var reply = sent.Single();
        reply.ResetOffset();
        reply.ReadByte().Should().Be(PreGamePacketWriter.ChangeHairSucceeded);
        reply.ReadUInt().Should().Be(0);
    }

    [Fact]
    public async Task WithoutACouponAtThatPositionNothingChanges()
    {
        var (coordinator, player, sent, _) = Arrange(ChangeHairPacketCoordinator.FaceChangeCoupon);
        var face = player.Face;

        await coordinator.HandleAsync(player.Client, Request(CouponBagPosition + 1));

        player.Face.Should().Be(face);
        var reply = sent.Single();
        reply.ResetOffset();
        reply.ReadByte().Should().Be(PreGamePacketWriter.ChangeHairFailed);
    }
}
