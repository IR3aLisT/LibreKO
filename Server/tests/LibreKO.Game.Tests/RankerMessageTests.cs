using FluentAssertions;
using LibreKO.Common.Domain.Entities;
using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace LibreKO.Game.Tests;

public class RankerMessageTests : GameTestBase
{
    private const byte Moradon = 21;
    private const int RankerId = 7;
    private const int VisitorId = 8;
    private const byte KarusFirst = NpcData.TypeRankerKarusFirst;
    private const byte KarusThird = NpcData.TypeRankerKarusFirst + 2;
    private const byte NotAStatue = NpcData.TypeTalk;

    private sealed record Setup(ServiceProvider Provider, IRankerMessagePacketCoordinator Coordinator,
        UserSession Ranker, List<Packet> RankerSent, UserSession Visitor, List<Packet> VisitorSent);

    private static async Task<Setup> ArrangeAsync()
    {
        var provider = CreateProvider(db =>
        {
            db.Accounts.AddRange(
                new Account { Id = 1, Login = "a", Password = "x", Nation = AccountNation.Karus },
                new Account { Id = 2, Login = "b", Password = "x", Nation = AccountNation.Karus });
            db.Characters.AddRange(
                new Character { Id = RankerId, AccountId = 1, Name = "Veteran", Race = 2, Class = 106, LoyaltyMonthly = 900 },
                new Character { Id = VisitorId, AccountId = 2, Name = "Visitor", Race = 1, Class = 101, LoyaltyMonthly = 0 });
        });
        var sessions = provider.GetRequiredService<SessionManager>();
        sessions.Regions.SpawnNpc(new NpcInstance
        {
            NpcId = 32082, NpcType = KarusFirst, ZoneId = Moradon, X = 790, Z = 561,
        });
        var (ranker, rankerSent) = Player(sessions, RankerId, "Veteran");
        var (visitor, visitorSent) = Player(sessions, VisitorId, "Visitor");
        await provider.GetRequiredService<INationRankService>().RefreshAsync();
        return new Setup(provider, provider.GetRequiredService<IRankerMessagePacketCoordinator>(),
            ranker, rankerSent, visitor, visitorSent);
    }

    private static (UserSession, List<Packet>) Player(SessionManager sessions, int characterId, string name)
    {
        var client = Substitute.For<IClient>();
        client.Id.Returns(Guid.NewGuid());
        var sent = new List<Packet>();
        client.SendPacket(Arg.Do<Packet>(packet => sent.Add(packet)), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var session = sessions.CreateSession(client, characterId, characterId);
        session.Name = name;
        session.Nation = AccountNation.Karus;
        session.ZoneId = Moradon;
        session.Race = 2;
        session.Class = 106;
        for (var i = 0; i < session.Inventory.Length; i++)
            session.Inventory[i] = new ItemSlot();
        return (session, sent);
    }

    private static Packet Request(RankerMessageSub sub, byte npcType, string? message = null)
    {
        var packet = new Packet(GameOpcodes.GS_RANKER_MESSAGE);
        packet.WriteByte((byte)sub);
        packet.WriteByte(npcType);
        if (message != null)
            packet.WriteString(message);
        packet.ResetOffset();
        return packet;
    }

    private static Packet Last(List<Packet> sent, RankerMessageSub sub, out short result)
    {
        var reply = sent.Last(p => p.GetOpcode() == (byte)GameOpcodes.GS_RANKER_MESSAGE);
        reply.ResetOffset();
        reply.ReadByte().Should().Be((byte)sub);
        result = reply.ReadShort();
        return reply;
    }

    [Fact]
    public async Task TheRankersMessageIsShownToVisitorsAndOnlyTheRankerCanWriteIt()
    {
        var s = await ArrangeAsync();

        await s.Coordinator.HandleAsync(s.Visitor.Client, Request(RankerMessageSub.Write, KarusFirst, "mine now"));
        Last(s.VisitorSent, RankerMessageSub.Write, out var refused);
        refused.Should().Be(RankerMessageResult.WriteNotRanker);

        await s.Coordinator.HandleAsync(s.Ranker.Client, Request(RankerMessageSub.Write, KarusFirst, "Glory to Karus"));
        Last(s.RankerSent, RankerMessageSub.Write, out var saved);
        saved.Should().Be(RankerMessageResult.Done);

        await s.Coordinator.HandleAsync(s.Visitor.Client, Request(RankerMessageSub.View, KarusFirst));
        var paper = Last(s.VisitorSent, RankerMessageSub.View, out var shown);
        shown.Should().Be(RankerMessageResult.Done);
        paper.ReadString().Should().Be("Veteran");
        paper.ReadString().Should().Be("Glory to Karus");
    }

    [Fact]
    public async Task MessagesMustFitAndVacantOrWrongStatuesAnswerWithTheirCodes()
    {
        var s = await ArrangeAsync();

        await s.Coordinator.HandleAsync(s.Ranker.Client,
            Request(RankerMessageSub.Write, KarusFirst, new string('a', RankerStatueNote.MessageMaxLength + 1)));
        Last(s.RankerSent, RankerMessageSub.Write, out var tooLong);
        tooLong.Should().Be(RankerMessageResult.WriteLengthError);

        await s.Coordinator.HandleAsync(s.Ranker.Client, Request(RankerMessageSub.Write, KarusThird, "hello"));
        Last(s.RankerSent, RankerMessageSub.Write, out var vacant);
        vacant.Should().Be(RankerMessageResult.WriteNoData);

        await s.Coordinator.HandleAsync(s.Visitor.Client, Request(RankerMessageSub.View, KarusThird));
        Last(s.VisitorSent, RankerMessageSub.View, out var revision);
        revision.Should().Be(RankerMessageResult.ViewUnderRevision);

        await s.Coordinator.HandleAsync(s.Visitor.Client, Request(RankerMessageSub.View, NotAStatue));
        Last(s.VisitorSent, RankerMessageSub.View, out var invalid);
        invalid.Should().Be(RankerMessageResult.ViewInvalidType);
    }

    [Fact]
    public async Task TheRankerCanPutTheirCurrentGearOnTheStatueOnceADay()
    {
        var s = await ArrangeAsync();
        const int Sword = 110010000;
        s.Ranker.Inventory[InventoryConstants.RightHand] = new ItemSlot { ItemId = Sword, Count = 1 };

        await s.Coordinator.HandleAsync(s.Visitor.Client, Request(RankerMessageSub.Redress, KarusFirst));
        Last(s.VisitorSent, RankerMessageSub.Redress, out var notRanker);
        notRanker.Should().Be(RankerMessageResult.RedressNotRanker);

        await s.Coordinator.HandleAsync(s.Ranker.Client, Request(RankerMessageSub.Redress, KarusFirst));
        Last(s.RankerSent, RankerMessageSub.Redress, out var applied);
        applied.Should().Be(RankerMessageResult.Done);
        s.Provider.GetRequiredService<SessionManager>().Regions.GetAllNpcs()
            .Single(npc => npc.NpcType == KarusFirst).StatueLook?.RightHand.Should().Be(Sword);

        await s.Coordinator.HandleAsync(s.Ranker.Client, Request(RankerMessageSub.Redress, KarusFirst));
        Last(s.RankerSent, RankerMessageSub.Redress, out var again);
        again.Should().Be(RankerMessageResult.RedressOncePerDay);
    }

    [Fact]
    public async Task ARankingRefreshKeepsTheRankersChosenGear()
    {
        var s = await ArrangeAsync();
        const int Sword = 110010000;
        s.Ranker.Inventory[InventoryConstants.RightHand] = new ItemSlot { ItemId = Sword, Count = 1 };
        await s.Coordinator.HandleAsync(s.Ranker.Client, Request(RankerMessageSub.Redress, KarusFirst));
        s.Ranker.Inventory[InventoryConstants.RightHand] = new ItemSlot();

        await s.Provider.GetRequiredService<INationRankService>().RefreshAsync();

        s.Provider.GetRequiredService<SessionManager>().Regions.GetAllNpcs()
            .Single(npc => npc.NpcType == KarusFirst).StatueLook?.RightHand.Should().Be(Sword);
    }
}
