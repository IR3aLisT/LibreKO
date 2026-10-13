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

public class MagicFailCodeTests : GameTestBase
{
    private const int UnknownSkill = 999001;
    private const int ManaBuff = 111654;
    private const short ManaCost = 400;
    private const byte Moradon = 21;

    private static ServiceProvider Provider() => CreateProvider(_ => { }, gameData =>
    {
        gameData.GetMagic(ManaBuff).Returns(new MagicData
        {
            Id = ManaBuff,
            Type1 = (byte)MagicSkillType.Buff,
            Moral = (byte)SkillMoral.Self,
            Msp = ManaCost,
        });
        gameData.MagicType4Table.Returns(new Dictionary<int, MagicType4Data>
        {
            [ManaBuff] = new() { Id = ManaBuff, BuffType = (byte)BuffType.Ac, Duration = 60, Ac = 10 },
        });
    });

    private sealed record Player(UserSession Session, IClient Client, List<Packet> Sent);

    private static Player Join(ServiceProvider provider, int characterId)
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
        session.MaxHp = 900;
        session.Hp = 900;
        session.MaxMp = 500;
        session.Mp = 100;
        sessionManager.Regions.AddToRegion(session);
        return new Player(session, client, sent);
    }

    private static async Task Send(ServiceProvider provider, Player player, MagicProcessOpcode stage, int skillId)
    {
        var packet = new Packet(GameOpcodes.GS_MAGIC_PROCESS);
        packet.WriteByte((byte)stage);
        packet.WriteInt(skillId);
        packet.WriteInt(player.Session.CharacterId);
        packet.WriteInt(player.Session.CharacterId);
        for (var i = 0; i < 7; i++)
            packet.WriteInt(0);
        await provider.GetRequiredService<IMagicPacketCoordinator>().HandleAsync(player.Client, packet);
    }

    private static int? FailCode(Player player)
    {
        foreach (var packet in player.Sent)
        {
            if (packet.GetOpcode() != (byte)GameOpcodes.GS_MAGIC_PROCESS) continue;
            packet.ResetOffset();
            if (packet.ReadByte() != (byte)MagicProcessOpcode.Fail) continue;
            packet.ReadInt();
            packet.ReadInt();
            packet.ReadInt();
            for (var i = 0; i < MagicFailCode.Slot; i++)
                packet.ReadInt();
            return packet.ReadInt();
        }
        return null;
    }

    [Theory]
    [InlineData(MagicProcessOpcode.Casting, MagicFailCode.Casting)]
    [InlineData(MagicProcessOpcode.Flying, MagicFailCode.KillFlying)]
    [InlineData(MagicProcessOpcode.Effecting, MagicFailCode.NoEffect)]
    public async Task ARefusalCarriesTheCodeOfItsStage(MagicProcessOpcode stage, int expected)
    {
        using var provider = Provider();
        var player = Join(provider, 801);

        await Send(provider, player, stage, UnknownSkill);

        FailCode(player).Should().Be(expected);
    }

    [Fact]
    public async Task ShortManaIsReportedAsSuch()
    {
        using var provider = Provider();
        var player = Join(provider, 802);

        await Send(provider, player, MagicProcessOpcode.Effecting, ManaBuff);

        FailCode(player).Should().Be(MagicFailCode.NotEnoughMp);
        player.Session.Mp.Should().Be(100);
    }
}
