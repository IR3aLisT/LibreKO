using FluentAssertions;
using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Domain.Services;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace LibreKO.Game.Tests;

public class BuffExtensionTests
{
    private const int BuffSkill = 111654;
    private const int ContinuanceSkill = 500039;
    private const int ContinuanceItem = 800022000;
    private const short BuffSeconds = 600;

    private readonly IGameDataService _data = Substitute.For<IGameDataService>();
    private readonly IMagicItemUsageService _items = Substitute.For<IMagicItemUsageService>();
    private readonly BuffExtensionService _service;
    private readonly SessionManager _sessions = new();
    private readonly List<Packet> _sent = new();
    private readonly UserSession _session;

    public BuffExtensionTests()
    {
        _data.GetMagic(BuffSkill).Returns(new MagicData { Id = BuffSkill, Moral = 1 });
        _data.GetMagic(ContinuanceSkill).Returns(new MagicData { Id = ContinuanceSkill, Moral = BuffExtensionService.ExtendDurationMoral });
        _data.MagicType4Table.Returns(new Dictionary<int, MagicType4Data> { [BuffSkill] = new() { BuffType = (byte)BuffType.None, Duration = BuffSeconds } });
        _data.GetItem(ContinuanceItem).Returns(new ItemData { Num = ContinuanceItem, Kind = BuffExtensionService.DurationItemKind, Effect1 = ContinuanceSkill });
        _items.TryConsumeItemAsync(Arg.Any<UserSession>(), ContinuanceItem, Arg.Any<int>()).Returns(true);
        _service = new BuffExtensionService(_data, _items, NullLogger<BuffExtensionService>.Instance);

        var client = Substitute.For<IClient>();
        client.Id.Returns(Guid.NewGuid());
        client.SendPacket(Arg.Do<Packet>(p => _sent.Add(p)), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _session = _sessions.CreateSession(client, 1, 1);
        _session.Name = "Rin";
    }

    private ActiveBuff Buff()
    {
        var buff = new ActiveBuff { MagicId = BuffSkill, Duration = BuffSeconds, ExpireTicks = DateTime.UtcNow.AddSeconds(BuffSeconds).Ticks };
        _session.ActiveBuffs[BuffSkill] = buff;
        return buff;
    }

    private void CarryContinuanceItem() =>
        _session.Inventory[InventoryConstants.InventoryStart] = new ItemSlot { ItemId = ContinuanceItem, Count = 1 };

    [Fact]
    public async Task ARunningBuffIsDoubledOnceWithAContinuanceItem()
    {
        var buff = Buff();
        long before = buff.ExpireTicks;
        CarryContinuanceItem();

        (await _service.ExtendAsync(_session, BuffSkill)).Should().BeTrue();
        (await _service.ExtendAsync(_session, BuffSkill)).Should().BeFalse();

        buff.ExpireTicks.Should().Be(before + TimeSpan.FromSeconds(BuffSeconds).Ticks);
        _sent.Should().ContainSingle().Which.GetData().Should().Equal(MagicProcessPacketWriter.CreateExtendDuration(BuffSkill).GetData());
    }

    [Fact]
    public async Task NothingHappensWithoutTheItemOrTheBuff()
    {
        Buff();
        (await _service.ExtendAsync(_session, BuffSkill)).Should().BeFalse();

        _session.ActiveBuffs.Clear();
        CarryContinuanceItem();
        (await _service.ExtendAsync(_session, BuffSkill)).Should().BeFalse();
        _sent.Should().BeEmpty();
    }
}
