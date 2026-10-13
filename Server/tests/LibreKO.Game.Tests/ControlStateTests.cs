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

public class ControlStateTests : GameTestBase
{
    private const int StunSkill = 110520;
    private const int SlowSkill = 109756;
    private const int SwiftSkill = 107725;
    private const byte Zone = 21;

    private static (UserSession Session, List<Packet> Sent) Join(SessionManager sessions, int id)
    {
        var client = Substitute.For<IClient>();
        client.Id.Returns(Guid.NewGuid());
        var sent = new List<Packet>();
        client.SendPacket(Arg.Do<Packet>(sent.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var session = sessions.CreateSession(client, id, accountId: id + 100);
        session.ZoneId = Zone;
        session.Hp = 100;
        sessions.Regions.AddToRegion(session);
        return (session, sent);
    }

    private static (byte Flags, byte Move, short Attack, List<(int Skill, byte Type, int Ms)> Effects) Read(Packet packet, int unitId)
    {
        packet.ResetOffset();
        packet.ReadByte().Should().Be(TargetStatePacketWriter.SubControl);
        packet.ReadInt().Should().Be(unitId);
        var flags = packet.ReadByte();
        var move = packet.ReadByte();
        var attack = packet.ReadShort();
        var effects = new List<(int, byte, int)>();
        int count = packet.ReadByte();
        for (var i = 0; i < count; i++)
            effects.Add((packet.ReadInt(), packet.ReadByte(), packet.ReadInt()));
        packet.RemainingBytes.Should().Be(0);
        return (flags, move, attack, effects);
    }

    private static List<Packet> Controls(List<Packet> sent) =>
        sent.Where(p => p.GetOpcode() == (byte)GameOpcodes.GS_TARGET_STATE).ToList();

    [Fact]
    public async Task AStunIsReportedOnceAndClearedWhenItEnds()
    {
        using var provider = CreateProvider(_ => { });
        var sessions = provider.GetRequiredService<SessionManager>();
        var service = provider.GetRequiredService<IControlStateService>();
        var (victim, _) = Join(sessions, 901);
        var (_, watcherSent) = Join(sessions, 902);
        victim.SetBuff(StunSkill, new ActiveBuff
        {
            MagicId = StunSkill, BuffType = BuffType.Stun, ExpireTicks = DateTime.UtcNow.AddSeconds(2).Ticks,
        });

        await service.SyncAsync(victim);
        await service.SyncAsync(victim);

        var reported = Controls(watcherSent);
        reported.Should().ContainSingle("an unchanged state is not sent twice");
        var state = Read(reported[0], victim.CharacterId);
        state.Flags.Should().Be(ControlFlags.Stunned);
        state.Effects.Should().ContainSingle().Which.Skill.Should().Be(StunSkill);

        victim.ClearBuffs();
        await service.SyncAsync(victim);

        var cleared = Read(Controls(watcherSent).Last(), victim.CharacterId);
        cleared.Flags.Should().Be(0);
        cleared.Effects.Should().BeEmpty();
    }

    [Fact]
    public async Task ASpeedBuffIsNotAControlEffect()
    {
        using var provider = CreateProvider(_ => { });
        var sessions = provider.GetRequiredService<SessionManager>();
        var (runner, sent) = Join(sessions, 903);
        runner.SetBuff(SwiftSkill, new ActiveBuff
        {
            MagicId = SwiftSkill, BuffType = BuffType.Speed, BonusSpeed = 150, ExpireTicks = DateTime.UtcNow.AddSeconds(30).Ticks,
        });

        await provider.GetRequiredService<IControlStateService>().SyncAsync(runner);

        Controls(sent).Should().BeEmpty();
    }

    [Fact]
    public async Task AMonsterSlowCarriesItsSpeedPercent()
    {
        using var provider = CreateProvider(_ => { });
        var sessions = provider.GetRequiredService<SessionManager>();
        var (viewer, sent) = Join(sessions, 904);
        var npc = sessions.Regions.SpawnNpc(new NpcInstance { ZoneId = Zone, X = viewer.X, Z = viewer.Z, Hp = 50 });
        npc.Debuffs.TryApply(SlowSkill, new MagicType4Data { BuffType = (byte)BuffType.Speed, Duration = 10, Speed = 40 },
            DateTime.UtcNow.Ticks);

        await provider.GetRequiredService<IControlStateService>().SyncAsync(npc);

        var state = Read(Controls(sent).Single(), npc.UniqueId);
        state.Flags.Should().Be(ControlFlags.Slowed);
        state.Move.Should().Be(40);
    }
}
