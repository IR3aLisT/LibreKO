using FluentAssertions;
using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace LibreKO.Game.Tests;

public class ResurrectionOfferTests : GameTestBase
{
    private const int ResurrectId = 111545;
    private const int StoneItem = ResurrectionOfferService.ResurrectionStone;
    private const short MaxHp = 900;
    private const byte RegeneOfferType = 4;
    private const byte RegeneRefusedType = 10;
    private const byte Moradon = 21;

    private static ServiceProvider Provider() => CreateProvider(_ => { }, gameData =>
    {
        gameData.GetMagic(ResurrectId).Returns(new MagicData
        {
            Id = ResurrectId,
            Type1 = (byte)MagicSkillType.Special,
            Moral = (byte)SkillMoral.CorpseFriend,
            UseItem = StoneItem,
        });
        gameData.MagicType5Table.Returns(new Dictionary<int, MagicType5Data>
        {
            [ResurrectId] = new() { Id = ResurrectId, Type = (byte)SpecialMagicType.Resurrection, ExpRecover = 60, NeedStone = 1 },
        });
        gameData.GetItem(StoneItem).Returns(new ItemData { Num = StoneItem, Countable = 1, Weight = 1, Duration = 1 });
        gameData.GetStartPosition(Moradon).Returns(new StartPositionData
        {
            ZoneId = Moradon, KarusX = 500, KarusZ = 500, ElmoradX = 500, ElmoradZ = 500,
        });
    });

    private sealed record Player(UserSession Session, IClient Client, List<Packet> Sent);

    private static Player Join(ServiceProvider provider, int characterId, short hp, ushort stones = 0)
    {
        var client = Substitute.For<IClient>();
        client.Id.Returns(Guid.NewGuid());
        var sent = new List<Packet>();
        client.SendPacket(Arg.Do<Packet>(sent.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var sessionManager = provider.GetRequiredService<SessionManager>();
        var session = sessionManager.CreateSession(client, characterId, accountId: characterId + 100);
        session.Name = $"P{characterId}";
        session.Class = 111;
        session.Level = 60;
        session.Nation = AccountNation.Karus;
        session.ZoneId = Moradon;
        session.X = 100;
        session.Z = 100;
        session.MaxHp = MaxHp;
        session.Hp = hp;
        session.MaxMp = 500;
        session.Mp = 500;
        if (stones > 0)
        {
            session.Inventory[InventoryConstants.InventoryStart].ItemId = StoneItem;
            session.Inventory[InventoryConstants.InventoryStart].Count = stones;
            session.Inventory[InventoryConstants.InventoryStart].Durability = 1;
        }
        sessionManager.Regions.AddToRegion(session);
        return new Player(session, client, sent);
    }

    private static async Task CastResurrection(ServiceProvider provider, Player priest, Player dead)
    {
        var packet = new Packet(GameOpcodes.GS_MAGIC_PROCESS);
        packet.WriteByte((byte)MagicProcessOpcode.Effecting);
        packet.WriteInt(ResurrectId);
        packet.WriteInt(priest.Session.CharacterId);
        packet.WriteInt(dead.Session.CharacterId);
        for (var i = 0; i < 7; i++)
            packet.WriteInt(0);
        await provider.GetRequiredService<IMagicPacketCoordinator>().HandleAsync(priest.Client, packet);
    }

    private static async Task Regene(ServiceProvider provider, Player player, params object[] fields)
    {
        var packet = new Packet(GameOpcodes.GS_REGENE);
        foreach (var field in fields)
        {
            if (field is byte b) packet.WriteByte(b);
            else packet.WriteInt((int)field);
        }
        await provider.GetRequiredService<ICombatPacketCoordinator>().HandleRegeneAsync(player.Client, packet);
    }

    private static Packet? LastRegene(Player player) =>
        player.Sent.LastOrDefault(p => p.GetOpcode() == (byte)GameOpcodes.GS_REGENE);

    private static byte RegeneType(Packet packet)
    {
        packet.ResetOffset();
        packet.ReadShort();
        packet.ReadShort();
        packet.ReadShort();
        return packet.ReadByte();
    }

    [Fact]
    public async Task AResurrectionCastOffersInsteadOfRaising()
    {
        using var provider = Provider();
        var priest = Join(provider, 701, hp: 500);
        var dead = Join(provider, 702, hp: 0, stones: 1);

        await CastResurrection(provider, priest, dead);

        dead.Session.Hp.Should().Be(0);
        var offer = LastRegene(dead);
        offer.Should().NotBeNull();
        RegeneType(offer!).Should().Be(RegeneOfferType);
        offer!.ReadInt().Should().Be(priest.Session.CharacterId);
        offer.ReadInt().Should().Be(ResurrectId);
    }

    [Fact]
    public async Task AcceptingRaisesTheDeadAndTakesTheirStone()
    {
        using var provider = Provider();
        var priest = Join(provider, 703, hp: 500);
        var dead = Join(provider, 704, hp: 0, stones: 2);

        await CastResurrection(provider, priest, dead);
        await Regene(provider, dead, (byte)5, priest.Session.CharacterId);

        dead.Session.Hp.Should().Be(MaxHp);
        dead.Session.Inventory[InventoryConstants.InventoryStart].Count.Should().Be(1);
    }

    [Fact]
    public async Task AcceptingAnotherCastersOfferDoesNothing()
    {
        using var provider = Provider();
        var priest = Join(provider, 705, hp: 500);
        var dead = Join(provider, 706, hp: 0, stones: 1);

        await CastResurrection(provider, priest, dead);
        await Regene(provider, dead, (byte)5, 9999);

        dead.Session.Hp.Should().Be(0);
    }

    [Fact]
    public async Task DecliningToTownRespawnsThePlayer()
    {
        using var provider = Provider();
        var priest = Join(provider, 707, hp: 500);
        var dead = Join(provider, 708, hp: 0);

        await CastResurrection(provider, priest, dead);
        await Regene(provider, dead, (byte)4, ResurrectionOfferService.ChoiceTown, priest.Session.CharacterId);

        dead.Session.Hp.Should().Be(MaxHp);
        dead.Session.X.Should().BeApproximately(500, 10);
        dead.Session.PendingResurrection.Should().BeNull();
    }

    [Fact]
    public async Task DecliningInPlaceWithoutStonesIsRefused()
    {
        using var provider = Provider();
        var priest = Join(provider, 709, hp: 500);
        var dead = Join(provider, 710, hp: 0);

        await CastResurrection(provider, priest, dead);
        await Regene(provider, dead, (byte)4, ResurrectionOfferService.ChoiceInPlace, priest.Session.CharacterId);

        dead.Session.Hp.Should().Be(0);
        var refusal = LastRegene(dead)!;
        RegeneType(refusal).Should().Be(RegeneRefusedType);
        refusal.ReadUShort().Should().Be(RegenePacketWriter.RefusedMissingItem);
        refusal.ReadInt().Should().Be(StoneItem);
        refusal.ReadUShort().Should().Be((ushort)(3 * dead.Session.Level));
    }

    [Fact]
    public async Task DecliningInPlaceWithStonesRaisesWhereThePlayerFell()
    {
        using var provider = Provider();
        var priest = Join(provider, 711, hp: 500);
        var dead = Join(provider, 712, hp: 0, stones: 200);

        await CastResurrection(provider, priest, dead);
        await Regene(provider, dead, (byte)4, ResurrectionOfferService.ChoiceInPlace, priest.Session.CharacterId);

        dead.Session.Hp.Should().Be(MaxHp);
        dead.Session.X.Should().BeApproximately(100, 0.5f);
        dead.Session.Inventory[InventoryConstants.InventoryStart].Count.Should().Be((ushort)(200 - 3 * dead.Session.Level));
    }
}
