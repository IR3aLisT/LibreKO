using FluentAssertions;
using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Domain.Services;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace LibreKO.Game.Tests;

public class DeathTauntTests : GameTestBase
{
    private const int Stomp = 490049;
    private const byte Moradon = 21;
    private const byte NoWeaponGroup = 9;

    private static ServiceProvider Provider() => CreateProvider(_ => { }, gameData =>
    {
        gameData.GetMagic(Stomp).Returns(new MagicData
        {
            Id = Stomp, Type1 = (byte)MagicSkillType.OverTime, Moral = (byte)SkillMoral.Enemy, Range = 1, ItemGroup = NoWeaponGroup,
        });
        gameData.MagicType3Table.Returns(new Dictionary<int, MagicType3Data> { [Stomp] = new() { Id = Stomp } });
    });

    private sealed record Player(UserSession Session, IClient Client, List<Packet> Sent);

    [Fact]
    public async Task AStompOnACorpseShowsTheTauntToEveryoneAndLeavesItDead()
    {
        using var provider = Provider();
        var stomper = Join(provider, 900, AccountNation.Karus);
        var corpse = Join(provider, 901, AccountNation.ElMorad);
        corpse.Session.Hp = 0;

        await Send(provider, stomper, corpse.Session.CharacterId);

        TauntMarker(stomper).Should().Be(DeathTaunt.Marker);
        TauntMarker(corpse).Should().Be(DeathTaunt.Marker);
        corpse.Session.Hp.Should().Be(0);
    }

    [Fact]
    public async Task AStompOnALivingPlayerIsNoTaunt()
    {
        using var provider = Provider();
        var stomper = Join(provider, 900, AccountNation.Karus);
        var target = Join(provider, 901, AccountNation.ElMorad);

        await Send(provider, stomper, target.Session.CharacterId);

        TauntMarker(target).Should().NotBe(DeathTaunt.Marker);
    }

    [Fact]
    public void ASkillThatHurtsIsNoTaunt()
    {
        var magic = new MagicData { Id = Stomp, Type1 = (byte)MagicSkillType.OverTime, Moral = (byte)SkillMoral.Enemy };
        DeathTaunt.IsTaunt(magic, new MagicType3Data { Id = Stomp }).Should().BeTrue();
        DeathTaunt.IsTaunt(magic, new MagicType3Data { Id = Stomp, FirstDamage = -100 }).Should().BeFalse();
        DeathTaunt.IsTaunt(magic, null).Should().BeFalse();
    }

    private static Player Join(ServiceProvider provider, int characterId, AccountNation nation)
    {
        var client = Substitute.For<IClient>();
        client.Id.Returns(Guid.NewGuid());
        var sent = new List<Packet>();
        client.SendPacket(Arg.Do<Packet>(sent.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var sessionManager = provider.GetRequiredService<SessionManager>();
        var session = sessionManager.CreateSession(client, characterId, accountId: characterId + 100);
        session.Name = $"P{characterId}";
        session.Class = nation == AccountNation.Karus ? (short)101 : (short)201;
        session.Level = 60;
        session.Nation = nation;
        session.ZoneId = Moradon;
        session.X = 100;
        session.Z = 100;
        session.MaxHp = 900;
        session.Hp = 900;
        session.MaxMp = 500;
        session.Mp = 100;
        sessionManager.Regions.AddToRegion(session);
        return new Player(session, client, sent);
    }

    private static async Task Send(ServiceProvider provider, Player player, int targetId)
    {
        var packet = new Packet(GameOpcodes.GS_MAGIC_PROCESS);
        packet.WriteByte((byte)MagicProcessOpcode.Effecting);
        packet.WriteInt(Stomp);
        packet.WriteInt(player.Session.CharacterId);
        packet.WriteInt(targetId);
        for (var i = 0; i < 7; i++)
            packet.WriteInt(0);
        await provider.GetRequiredService<IMagicPacketCoordinator>().HandleAsync(player.Client, packet);
    }

    private static int? TauntMarker(Player player)
    {
        foreach (var packet in player.Sent)
        {
            if (packet.GetOpcode() != (byte)GameOpcodes.GS_MAGIC_PROCESS) continue;
            packet.ResetOffset();
            if (packet.ReadByte() != (byte)MagicProcessOpcode.Effecting) continue;
            packet.ReadInt();
            packet.ReadInt();
            packet.ReadInt();
            for (var i = 0; i < DeathTaunt.MarkerSlot; i++)
                packet.ReadInt();
            return packet.ReadInt();
        }
        return null;
    }
}
