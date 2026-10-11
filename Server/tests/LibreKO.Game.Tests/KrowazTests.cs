using System.Runtime.CompilerServices;
using FluentAssertions;
using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Configuration;
using LibreKO.Game.Protocol;
using LibreKO.Game.World;
using LibreKO.Quests;
using LibreKO.Quests.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace LibreKO.Game.Tests;

public class KrowazTests : GameTestBase
{
    private const int Krowaz = (int)ZoneId.KrowazDominion;
    private const string Enter = "I'll just go.";
    private const string ClosedText = "Currently you may not enter the Krowaz. Krowaz entry times are from 14:00 to 15:00.";

    private static string BakedQuestPath(string name, [CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", "..", "LibreKO.Game", "Quests", name));

    private static IReadOnlyList<DialogButton> Shown(IQuestHost host) =>
        (IReadOnlyList<DialogButton>)host.ReceivedCalls().Last(c => c.GetMethodInfo().Name == "ShowDialog").GetArguments()[3]!;

    private static DialogLine ShownText(IQuestHost host) =>
        (DialogLine)host.ReceivedCalls().Last(c => c.GetMethodInfo().Name == "ShowDialog").GetArguments()[2]!;

    private static void Run(QuestProgram program, IQuestHost host, int eventId) =>
        new QuestInterpreter(program, host).Run(eventId).Failure.Should().BeNull();

    private static IQuestHost Rider(bool open, bool leader)
    {
        var host = Substitute.For<IQuestHost>();
        host.PlayerLevel.Returns(70);
        host.KrowazOpen.Returns(open);
        host.IsInParty.Returns(leader);
        host.IsPartyLeader.Returns(leader);
        return host;
    }

    private static void TalkAndEnter(IQuestHost host, string file = "32285.quest")
    {
        var compilation = QuestCompilation.CreateFromFile(BakedQuestPath(file));
        compilation.Succeeded.Should().BeTrue(compilation.RenderDiagnostics());
        compilation.Program.TryGetEntry(QuestProgram.GreetingEvent, 0, out var greeting).Should().BeTrue();
        Run(compilation.Program, host, greeting);
        Run(compilation.Program, host, Shown(host).Single(t => t.Label.Text == Enter).TargetEvent);
    }

    [Fact]
    public void TheRiderTakesAPlayerInWhileKrowazIsOpen()
    {
        var host = Rider(open: true, leader: false);
        TalkAndEnter(host);
        host.Received(1).TeleportToZone(Krowaz, 55, 1974);
        host.DidNotReceive().TeleportPartyToZone(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public void ThePartyLeaderTakesTheWholePartyIn()
    {
        var host = Rider(open: true, leader: true);
        TalkAndEnter(host, "32289.quest");
        host.Received(1).TeleportPartyToZone(Krowaz, 62, 1859);
        host.DidNotReceive().TeleportToZone(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public void AClosedKrowazTurnsThePlayerAway()
    {
        var host = Rider(open: false, leader: false);
        TalkAndEnter(host);
        ShownText(host).Text.Should().Be(ClosedText);
        host.DidNotReceive().TeleportToZone(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public async Task TheWindowOpensOnItsHourCountsDownAndSendsEveryoneHome()
    {
        using var provider = CreateProvider(_ => { });
        var sessions = provider.GetRequiredService<SessionManager>();
        var krowaz = (KrowazService)provider.GetRequiredService<IKrowazService>();
        var inside = Join(sessions, 900, Krowaz);
        var start = new DateTime(2026, 10, 9, 14, 0, 0);

        await krowaz.TickAtAsync(start);
        krowaz.IsOpen.Should().BeTrue();
        inside.ZoneId.Should().Be((byte)Krowaz, "nobody is sent home while it is open");

        await krowaz.TickAtAsync(start.AddMinutes(KrowazService.OpenMinutes));
        krowaz.IsOpen.Should().BeFalse();
        inside.ZoneId.Should().Be((byte)ZoneId.Moradon);
    }

    [Fact]
    public async Task OutsideTheWindowPlayersInKrowazAreSentToMoradon()
    {
        using var provider = CreateProvider(_ => { });
        var sessions = provider.GetRequiredService<SessionManager>();
        var krowaz = (KrowazService)provider.GetRequiredService<IKrowazService>();
        var stray = Join(sessions, 901, Krowaz);

        await krowaz.TickAtAsync(new DateTime(2026, 10, 9, 9, 30, 0));

        krowaz.IsOpen.Should().BeFalse();
        stray.ZoneId.Should().Be((byte)ZoneId.Moradon);
    }

    [Theory]
    [InlineData(100f, 200f, true)]
    [InlineData(106.9f, 202.9f, true)]
    [InlineData(107.5f, 200f, false)]
    [InlineData(100f, 203.5f, false)]
    public void TheGasCloudIsSevenWideAndThreeDeep(float x, float z, bool inside) =>
        KrowazTrapService.InGasCloud(x, z, 100f, 200f).Should().Be(inside);

    [Fact]
    public void TrapsHurtByTheirKind()
    {
        var saw = new NpcInstance { NpcId = TouchTrapService.SawBladeNpcId };
        var stone = new NpcInstance { NpcId = 32152, NpcType = NpcData.TypeRollingStone };
        var rockslide = new NpcInstance { NpcId = 30200, NpcType = NpcData.TypeObjectWood };
        var guard = new NpcInstance { NpcId = 1, NpcType = 11 };

        TouchTrapService.TouchDamage(saw, Krowaz).Should().Be(TouchTrapService.SawBladeDamageInKrowaz);
        TouchTrapService.TouchDamage(saw, (short)ZoneId.ChaosDungeon).Should().Be(TouchTrapService.SawBladeDamageElsewhere);
        TouchTrapService.TouchDamage(stone, Krowaz).Should().Be(TouchTrapService.RollingStoneDamage);
        TouchTrapService.TouchDamage(rockslide, (short)ZoneId.Moradon).Should().Be(TouchTrapService.WoodObjectDamage);
        TouchTrapService.TouchDamage(guard, Krowaz).Should().Be(0);
    }

    [Fact]
    public async Task TouchingASawBladeHurtsAndTalkingToItDoesNot()
    {
        using var provider = CreateProvider(_ => { });
        var sessions = provider.GetRequiredService<SessionManager>();
        var player = Join(sessions, 902, Krowaz);
        player.MaxHp = 5000;
        player.Hp = 5000;
        var saw = new NpcInstance
        {
            NpcId = TouchTrapService.SawBladeNpcId, ZoneId = (byte)Krowaz, X = player.X + 1, Z = player.Z, Hp = 100, MaxHp = 100,
        };
        sessions.Regions.SpawnNpc(saw);

        await NpcEvent(provider, player, Talk, saw.UniqueId);
        player.Hp.Should().Be(5000);

        await NpcEvent(provider, player, Touch, saw.UniqueId);
        player.Hp.Should().Be(5000 - TouchTrapService.SawBladeDamageInKrowaz);
    }

    [Fact]
    public async Task DyingToARollingStoneInKrowazCostsNationalPoints()
    {
        using var provider = CreateProvider(_ => { });
        var sessions = provider.GetRequiredService<SessionManager>();
        var player = Join(sessions, 903, Krowaz);
        player.Hp = 1000;
        player.Loyalty = 500;
        var stone = new NpcInstance
        {
            NpcId = 32152, NpcType = NpcData.TypeRollingStone, ZoneId = (byte)Krowaz, X = player.X, Z = player.Z + 1, Hp = 100, MaxHp = 100,
        };
        sessions.Regions.SpawnNpc(stone);

        await NpcEvent(provider, player, Touch, stone.UniqueId);

        player.Hp.Should().Be(0);
        player.Loyalty.Should().Be(500 + TouchTrapService.TrapDeathLoyalty);
    }

    private const byte Talk = 1;
    private const byte Touch = 2;

    private static Task NpcEvent(ServiceProvider provider, UserSession player, byte kind, int npcId)
    {
        var packet = new Packet(GameOpcodes.GS_NPC_EVENT);
        packet.WriteByte(kind);
        packet.WriteInt(npcId);
        packet.WriteInt(-1);
        return provider.GetRequiredService<IQuestNpcInteractionService>().HandleNpcEventAsync(player.Client, packet);
    }

    [Fact]
    public async Task AKeyGateOpensOnceAndClosesAfterItsTime()
    {
        using var provider = CreateProvider(_ => { });
        var sessions = provider.GetRequiredService<SessionManager>();
        var traps = (KrowazTrapService)provider.GetRequiredService<IKrowazTrapService>();
        var player = Join(sessions, 904, Krowaz);
        const short gateNpc = 32110;
        var gate = new NpcInstance { NpcId = gateNpc, ZoneId = (byte)Krowaz, X = player.X, Z = player.Z, Hp = 100, MaxHp = 100,
            ObjectEventType = KrowazTrapService.KeyGateObject };
        sessions.Regions.SpawnNpc(gate);
        var objectEvent = new ObjectEvent { Index = gateNpc, Type = KrowazTrapService.KeyGateObject, ControlNpcId = gateNpc };

        (await traps.TryOpenKeyGateAsync(player, objectEvent)).Should().BeTrue();
        (await traps.TryOpenKeyGateAsync(player, objectEvent)).Should().BeFalse("an open gate cannot be opened again");
        gate.GateOpen.Should().Be(1);

        await traps.TickAtAsync(DateTime.UtcNow.AddSeconds(KrowazTrapService.KeyGateOpenSeconds + 1));
        gate.GateOpen.Should().Be(0);
    }

    private static UserSession Join(SessionManager sessions, int id, int zone)
    {
        var client = Substitute.For<IClient>();
        client.Id.Returns(Guid.NewGuid());
        client.SendPacket(Arg.Any<Packet>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var session = sessions.CreateSession(client, id, id + 100);
        session.Name = $"P{id}";
        session.ZoneId = (byte)zone;
        session.Level = 70;
        session.Hp = 500;
        session.MaxHp = 500;
        session.X = 60;
        session.Z = 1970;
        sessions.Regions.AddToRegion(session);
        return session;
    }
}
