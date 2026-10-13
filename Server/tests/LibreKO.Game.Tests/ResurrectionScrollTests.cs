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

public class ResurrectionScrollTests : GameTestBase
{
    private const int ScrollSkillId = 480002;
    private const int ScrollItemId = 910022000;
    private const short MaxHp = 800;

    private static ServiceProvider Provider() => CreateProvider(_ => { }, gameData =>
    {
        gameData.GetMagic(ScrollSkillId).Returns(new MagicData
        {
            Id = ScrollSkillId,
            Type1 = (byte)MagicSkillType.Special,
            Moral = (byte)SkillMoral.Self,
            UseItem = ScrollItemId,
        });
        gameData.MagicType5Table.Returns(new Dictionary<int, MagicType5Data>
        {
            [ScrollSkillId] = new() { Id = ScrollSkillId, Type = (byte)SpecialMagicType.ResurrectionSelf, ExpRecover = 50 },
        });
        gameData.GetItem(ScrollItemId).Returns(new ItemData
        {
            Num = ScrollItemId,
            Countable = 1,
            Weight = 1,
            Duration = 1,
        });
    });

    private static (UserSession Session, IClient Client) DeadPlayer(ServiceProvider provider, ushort scrolls)
    {
        var client = Substitute.For<IClient>();
        client.Id.Returns(Guid.NewGuid());
        client.SendPacket(Arg.Any<Packet>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var sessionManager = provider.GetRequiredService<SessionManager>();
        var session = sessionManager.CreateSession(client, characterId: 561, accountId: 661);
        session.Class = 101;
        session.Level = 40;
        session.ZoneId = 1;
        session.X = 20;
        session.Z = 20;
        session.MaxHp = MaxHp;
        session.Hp = 0;
        if (scrolls > 0)
        {
            session.Inventory[InventoryConstants.InventoryStart].ItemId = ScrollItemId;
            session.Inventory[InventoryConstants.InventoryStart].Count = scrolls;
            session.Inventory[InventoryConstants.InventoryStart].Durability = 1;
        }
        sessionManager.Regions.AddToRegion(session);
        return (session, client);
    }

    private static async Task UseScroll(ServiceProvider provider, UserSession session, IClient client)
    {
        var packet = new Packet(GameOpcodes.GS_MAGIC_PROCESS);
        packet.WriteByte((byte)MagicProcessOpcode.Effecting);
        packet.WriteInt(ScrollSkillId);
        packet.WriteInt(session.CharacterId);
        packet.WriteInt(session.CharacterId);
        for (var i = 0; i < 7; i++)
            packet.WriteInt(0);

        await provider.GetRequiredService<IMagicPacketCoordinator>().HandleAsync(client, packet);
    }

    [Fact]
    public async Task AScrollRaisesTheDeadPlayerAndIsUsedUp()
    {
        using var provider = Provider();
        var (session, client) = DeadPlayer(provider, scrolls: 2);

        await UseScroll(provider, session, client);

        session.Hp.Should().Be(MaxHp);
        session.Inventory[InventoryConstants.InventoryStart].Count.Should().Be(1);
    }

    [Fact]
    public async Task WithoutTheScrollThePlayerStaysDead()
    {
        using var provider = Provider();
        var (session, client) = DeadPlayer(provider, scrolls: 0);

        await UseScroll(provider, session, client);

        session.Hp.Should().Be(0);
    }
}
