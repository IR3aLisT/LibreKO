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

public class RespawnBlinkTests : GameTestBase
{
    private const byte Moradon = 21;
    private const byte RonarkLand = 71;
    private const byte RegeneTown = 1;
    private const short MaxHp = 900;
    private const int Healing = 111545;
    private const int Strike = 111505;

    private static ServiceProvider Provider() => CreateProvider(_ => { }, gameData =>
    {
        gameData.GetStartPosition(Moradon).Returns(new StartPositionData
        {
            ZoneId = Moradon, KarusX = 500, KarusZ = 500, ElmoradX = 500, ElmoradZ = 500,
        });
        gameData.GetMagic(Healing).Returns(new MagicData { Id = Healing, Type1 = (byte)MagicSkillType.Buff, Moral = 1 });
        gameData.GetMagic(Strike).Returns(new MagicData { Id = Strike, Type1 = (byte)MagicSkillType.Melee, Moral = 7 });
    });

    private sealed record Player(UserSession Session, IClient Client, List<Packet> Sent);

    private static Player Join(ServiceProvider provider, int characterId, byte zone = Moradon, short hp = MaxHp)
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
        session.ZoneId = zone;
        session.X = 100;
        session.Z = 100;
        session.MaxHp = MaxHp;
        session.Hp = hp;
        sessionManager.Regions.AddToRegion(session);
        return new Player(session, client, sent);
    }

    private static int? LastAbnormal(Player player)
    {
        int? last = null;
        foreach (var packet in player.Sent)
        {
            if (packet.GetOpcode() != (byte)GameOpcodes.GS_STATE_CHANGE) continue;
            packet.ResetOffset();
            if (packet.ReadInt() != player.Session.CharacterId) continue;
            if (packet.ReadByte() != (byte)StateChangeType.Abnormal) continue;
            last = packet.ReadInt();
        }
        return last;
    }

    private static async Task RespawnInTown(ServiceProvider provider, Player player)
    {
        var packet = new Packet(GameOpcodes.GS_REGENE);
        packet.WriteByte(RegeneTown);
        await provider.GetRequiredService<ICombatPacketCoordinator>().HandleRegeneAsync(player.Client, packet);
    }

    [Fact]
    public async Task ARespawnInTownBlinks()
    {
        using var provider = Provider();
        var player = Join(provider, 901, hp: 0);

        await RespawnInTown(provider, player);

        player.Session.IsBlinking.Should().BeTrue();
        LastAbnormal(player).Should().Be((int)PlayerAppearanceState.Blinking);
    }

    [Fact]
    public async Task TheBlinkEndsAfterItsTime()
    {
        using var provider = Provider();
        var player = Join(provider, 902);
        var blink = provider.GetRequiredService<IRespawnBlinkService>();
        await blink.StartAsync(player.Session);

        await blink.ExpireAsync(player.Session, DateTime.UtcNow.AddSeconds(RespawnBlinkService.BlinkSeconds - 1).Ticks);
        player.Session.IsBlinking.Should().BeTrue();

        await blink.ExpireAsync(player.Session, DateTime.UtcNow.AddSeconds(RespawnBlinkService.BlinkSeconds + 1).Ticks);
        player.Session.IsBlinking.Should().BeFalse();
        LastAbnormal(player).Should().Be((int)PlayerAppearanceState.Normal);
    }

    [Fact]
    public async Task NoBlinkWhereNationsFight()
    {
        using var provider = Provider();
        var player = Join(provider, 903, zone: RonarkLand);

        await provider.GetRequiredService<IRespawnBlinkService>().StartAsync(player.Session);

        player.Session.IsBlinking.Should().BeFalse();
        LastAbnormal(player).Should().BeNull();
    }

    [Fact]
    public void ABlinkingPlayerCannotBeAttacked()
    {
        using var provider = Provider();
        var attacker = Join(provider, 904, zone: RonarkLand);
        var target = Join(provider, 905, zone: RonarkLand);
        target.Session.Nation = AccountNation.ElMorad;

        PvpRules.CanAttackPlayer(attacker.Session, target.Session).Should().BeTrue();
        target.Session.BlinkUntilTicks = DateTime.UtcNow.AddSeconds(RespawnBlinkService.BlinkSeconds).Ticks;
        PvpRules.CanAttackPlayer(attacker.Session, target.Session).Should().BeFalse();
    }

    [Fact]
    public void ABlinkingPlayerKeepsBuffsAndItemsOnly()
    {
        RespawnBlinkService.AllowsSkill(new MagicData { Id = Healing, Type1 = (byte)MagicSkillType.Buff }).Should().BeTrue();
        RespawnBlinkService.AllowsSkill(new MagicData { Id = 490010, Type1 = (byte)MagicSkillType.OverTime }).Should().BeTrue();
        RespawnBlinkService.AllowsSkill(new MagicData { Id = Strike, Type1 = (byte)MagicSkillType.Melee }).Should().BeFalse();
    }

    [Fact]
    public void NoChaosSkillWhileBlinking()
    {
        RespawnBlinkService.AllowsSkill(new MagicData
        {
            Id = 490222, Type1 = (byte)MagicSkillType.OverTime, Skill = ChaosRules.ChaosSkillTree,
        }).Should().BeFalse();
        RespawnBlinkService.AllowsSkill(new MagicData
        {
            Id = 490223, Type1 = (byte)MagicSkillType.Buff, Skill = ChaosRules.ChaosSkillTree,
        }).Should().BeFalse();
    }
}
