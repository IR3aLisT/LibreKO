using FluentAssertions;
using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Enums;
using LibreKO.Game.Protocol;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;
using LibreKO.Quests;
using LibreKO.Quests.Runtime;
using NSubstitute;

namespace LibreKO.Game.Tests;

public class RepurchaseTests
{
    private const int Kaishan = 18004;
    private const int Moradon = 21;
    private const int Sword = 110110001;
    private const long Now = 1_800_000_000;

    private static ItemSlot Slot(int itemId, ushort count = 1, long expiresAt = 0, int uniqueId = 0) =>
        new() { ItemId = itemId, Count = count, Durability = 900, Flag = 0, ExpiresAt = expiresAt, UniqueId = uniqueId };

    [Fact]
    public void ASoldOrDestroyedItemIsKeptForThreeDays()
    {
        var list = new List<RepurchaseEntry>();
        RepurchaseRules.Record(list, Slot(Sword), 1, Now);
        list.Should().ContainSingle().Which.DeletesAt.Should().Be(Now + 3 * ItemSlot.SecondsPerDay);
        RepurchaseRules.Prune(list, Now + 3 * ItemSlot.SecondsPerDay);
        list.Should().BeEmpty();
    }

    [Fact]
    public void TimedAndLinkedItemsAreNotKept()
    {
        var list = new List<RepurchaseEntry>();
        RepurchaseRules.Record(list, Slot(Sword, expiresAt: Now + 10), 1, Now);
        RepurchaseRules.Record(list, Slot(Sword, uniqueId: 7), 1, Now);
        list.Should().BeEmpty();
    }

    [Fact]
    public void OnlyTheLatestTwentyAreKept()
    {
        var list = new List<RepurchaseEntry>();
        for (var i = 0; i < RepurchaseRules.MaxEntries + 5; i++)
            RepurchaseRules.Record(list, Slot(Sword + i), 1, Now + i);
        list.Should().HaveCount(RepurchaseRules.MaxEntries);
        list[0].ItemId.Should().Be(Sword + 5);
    }

    [Fact]
    public void TheListSurvivesTheCharacterBlob()
    {
        var list = new List<RepurchaseEntry>();
        RepurchaseRules.Record(list, Slot(Sword, 3), 3, Now);
        RepurchaseRules.Decode(RepurchaseRules.Encode(list)).Should().Equal(list);
        RepurchaseRules.Decode([]).Should().BeEmpty();
    }

    [Fact]
    public void ThePriceIsThreeTimesTheBuyPricePerItemCappedAtTheCoinMaximum()
    {
        RepurchaseRules.Price(1_000, 2).Should().Be(6_000);
        RepurchaseRules.Price(int.MaxValue, 10).Should().Be(2_100_000_000);
    }

    [Fact]
    public void TheListUsesThe2619Layout()
    {
        var packet = ItemTradePacketWriter.RepurchaseList(ItemTradePacketWriter.RepurchaseOpen,
            [new ItemTradePacketWriter.RepurchaseRow(0, Sword, 6_000, 1_800_259_200)]);
        packet.ReadByte().Should().Be((byte)ItemTradeResult.Repurchase);
        packet.ReadByte().Should().Be(ItemTradePacketWriter.RepurchaseOpen);
        packet.ReadByte().Should().Be(1);
        packet.ReadByte().Should().Be(1);
        packet.ReadByte().Should().Be(0);
        packet.ReadInt().Should().Be(Sword);
        packet.ReadInt().Should().Be(6_000);
        packet.ReadUInt().Should().Be(1_800_259_200u);
    }

    [Fact]
    public void ABuyReplyLeavesOneByteBeforeItsPayload()
    {
        var bought = ItemTradePacketWriter.RepurchaseBought(4, Sword, 0);
        bought.ReadByte().Should().Be((byte)ItemTradeResult.Repurchase);
        bought.ReadByte().Should().Be(ItemTradePacketWriter.RepurchaseBuy);
        bought.ReadByte().Should().Be(ItemTradePacketWriter.RepurchaseSucceeded);
        bought.ReadByte().Should().Be(0);
        bought.ReadByte().Should().Be(4);
        bought.ReadInt().Should().Be(Sword);

        var refused = ItemTradePacketWriter.RepurchaseRefused(Sword, -6);
        refused.ReadByte();
        refused.ReadByte();
        refused.ReadByte().Should().Be(ItemTradePacketWriter.RepurchaseFailed);
        refused.ReadByte().Should().Be(0);
        refused.ReadInt().Should().Be(Sword);
        refused.ReadShort().Should().Be(-6);
    }

    private static string BakedQuestPath(string name, [System.Runtime.CompilerServices.CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", "..", "LibreKO.Game", "Quests", name));

    [Fact]
    public void KaishansTopicOpensTheBuyBackWindow()
    {
        var compilation = QuestCompilation.CreateFromFile(BakedQuestPath("18004_21_77.quest"));
        compilation.Succeeded.Should().BeTrue(compilation.RenderDiagnostics());
        var program = QuestProgramComposer.Compose("kaishan", Kaishan, Moradon, [compilation.Program]);
        var host = Substitute.For<IQuestHost>();
        host.PlayerZone.Returns(Moradon);
        var topic = program.Events.Values.Single(e => e.Name is null).Id;
        new QuestInterpreter(program, host).Run(topic).Failure.Should().BeNull();
        host.Received(1).OpenRepurchaseWindow();
        NpcDialogPacketWriter.RepurchaseWindowStyle.Should().Be(30);
    }
}
