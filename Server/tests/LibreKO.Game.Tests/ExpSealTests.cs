using FluentAssertions;
using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Domain.Services;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;
using NSubstitute;
using Xunit;

namespace LibreKO.Game.Tests;

public class ExpSealTests
{
    private readonly SessionManager _sessions = new();
    private readonly IMagicItemUsageService _items = Substitute.For<IMagicItemUsageService>();
    private readonly IItemGrantService _grants = Substitute.For<IItemGrantService>();
    private readonly IPlayerProgressionService _progression = Substitute.For<IPlayerProgressionService>();
    private readonly ExpSealService _seal;
    private readonly UserSession _player;

    public ExpSealTests()
    {
        var gameData = Substitute.For<IGameDataService>();
        gameData.GetItem(ExpSealRules.LargeJar).Returns(new ItemData { Num = ExpSealRules.LargeJar });
        _items.TryConsumeItemAsync(Arg.Any<UserSession>(), Arg.Any<int>(), Arg.Any<int>()).Returns(true);
        _grants.GrantAsync(Arg.Any<UserSession>(), Arg.Any<ItemData>(), Arg.Any<int>()).Returns(call => call.Arg<int>());
        _seal = new ExpSealService(_sessions, gameData, _items, _grants, _progression);
        var client = Substitute.For<IClient>();
        client.Id.Returns(Guid.NewGuid());
        client.SendPacket(Arg.Any<Packet>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _player = _sessions.CreateSession(client, 1, 1001);
        _player.Hp = 100;
    }

    private static Packet Request(byte sub, params object[] parts)
    {
        var packet = new Packet(GameOpcodes.GS_EXP_SEAL);
        packet.WriteByte(sub);
        foreach (var part in parts)
        {
            switch (part)
            {
                case int i: packet.WriteInt(i); break;
                case byte b: packet.WriteByte(b); break;
                case long l: packet.WriteLong(l); break;
            }
        }
        packet.ResetOffset();
        return packet;
    }

    private Packet LastSent()
    {
        var packet = _player.Client.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == nameof(IClient.SendPacket))
            .Select(call => (Packet)call.GetArguments()[0]!)
            .Last();
        packet.ResetOffset();
        return packet;
    }

    [Fact]
    public async Task WhileOnMonsterExpGoesIntoTheSealAndTheTotalIsSent()
    {
        await _seal.HandleAsync(_player.Client, Request(ExpSealRules.SubOn));
        _player.ExpSealOn.Should().BeTrue();

        (await _seal.TrySealAsync(_player, 1_500)).Should().BeTrue();

        _player.SealedExperience.Should().Be(1_500);
        var total = LastSent();
        total.ReadByte().Should().Be(ExpSealRules.SubTotal);
        total.ReadLong().Should().Be(1_500);
    }

    [Fact]
    public async Task WhileOffNothingIsSealed()
    {
        await _seal.HandleAsync(_player.Client, Request(ExpSealRules.SubOff));

        (await _seal.TrySealAsync(_player, 1_500)).Should().BeFalse();
        _player.SealedExperience.Should().Be(0);
    }

    [Fact]
    public async Task StoringNeedsTheSealOffAndEnoughSealedExp()
    {
        _player.ExpSealOn = true;
        _player.SealedExperience = 600_000_000;
        await _seal.HandleAsync(_player.Client, Request(ExpSealRules.SubStore, ExpSealRules.EmptyJar, (byte)14, 500_000_000L));
        var whileOn = LastSent();
        whileOn.ReadByte();
        whileOn.ReadShort().Should().Be(ExpSealRules.StoreWhileOn);

        _player.ExpSealOn = false;
        await _seal.HandleAsync(_player.Client, Request(ExpSealRules.SubStore, ExpSealRules.EmptyJar, (byte)14, 1_000_000_000L));
        var tooLittle = LastSent();
        tooLittle.ReadByte();
        tooLittle.ReadShort().Should().Be(ExpSealRules.StoreTooLittle);
    }

    [Fact]
    public async Task StoringTurnsTheEmptyJarIntoFilledJarsAndLowersTheTotal()
    {
        _player.SealedExperience = 600_000_000;

        await _seal.HandleAsync(_player.Client, Request(ExpSealRules.SubStore, ExpSealRules.EmptyJar, (byte)14, 500_000_000L));

        await _items.Received(1).TryConsumeItemAsync(_player, ExpSealRules.EmptyJar, 1);
        await _grants.Received(1).GrantAsync(_player, Arg.Is<ItemData>(i => i.Num == ExpSealRules.LargeJar), 5);
        _player.SealedExperience.Should().Be(100_000_000);
        var reply = LastSent();
        reply.ReadByte().Should().Be(ExpSealRules.SubStore);
        reply.ReadShort().Should().Be(ExpSealRules.Succeeded);
        reply.ReadLong().Should().Be(100_000_000);
    }

    [Fact]
    public async Task UsingAFilledJarGivesItsExp()
    {
        await _seal.HandleAsync(_player.Client, Request(ExpSealRules.SubUseJar, ExpSealRules.SmallJar, (byte)14));

        await _progression.Received(1).ChangeExperienceAsync(_player, ExpSealRules.SmallJarExp);
        var reply = LastSent();
        reply.ReadByte().Should().Be(ExpSealRules.SubUseJar);
        reply.ReadShort().Should().Be(ExpSealRules.Succeeded);
    }
}
