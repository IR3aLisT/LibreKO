using FluentAssertions;
using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Domain.Services;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.Scripting;
using LibreKO.Game.World;
using NSubstitute;

namespace LibreKO.Game.Tests;

public class TargetQuestDropsTests
{
    private const int AnimalBlood = 379273000;
    private const int RottenEye = 379274000;
    private const int Apple = 379091000;
    private const short DropGroup = 77;
    private const int BloodQuest = 127;
    private const int EyeQuest = 130;
    private const int AppleQuest = 900;

    private static (IGameDataService Data, IQuestDefinitionSource Quests) Sources()
    {
        var data = Substitute.For<IGameDataService>();
        data.GetNpcItem(DropGroup, true).Returns(new NpcItemData
        {
            Index = DropGroup, IsMonster = true,
            Item1 = AnimalBlood, Percent1 = 3000,
            Item2 = RottenEye, Percent2 = 3000,
        });
        data.MakeItemGroupTable.Returns(new Dictionary<int, int[]>());
        var quests = Substitute.For<IQuestDefinitionSource>();
        quests.CollectsFor(BloodQuest, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>()).Returns(new List<(int, int)> { (AnimalBlood, 3) });
        quests.CollectsFor(EyeQuest, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>()).Returns(new List<(int, int)> { (RottenEye, 3) });
        quests.CollectsFor(AppleQuest, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>()).Returns(new List<(int, int)> { (Apple, 5) });
        return (data, quests);
    }

    private static UserSession Player()
    {
        var sessions = new SessionManager();
        var client = Substitute.For<IClient>();
        client.Id.Returns(Guid.NewGuid());
        var session = sessions.CreateSession(client, 1, 1);
        session.Class = 101;
        session.Nation = AccountNation.Karus;
        return session;
    }

    [Fact]
    public void OnlyOpenQuestItemsThisMonsterDropsAreListed()
    {
        var (data, quests) = Sources();
        var player = Player();
        player.Quest.QuestMap[BloodQuest] = (byte)QuestStatus.Active;
        player.Quest.QuestMap[EyeQuest] = (byte)QuestStatus.Completed;
        player.Quest.QuestMap[AppleQuest] = (byte)QuestStatus.Active;
        player.Inventory[InventoryConstants.InventoryStart].ItemId = AnimalBlood;
        player.Inventory[InventoryConstants.InventoryStart].Count = 2;

        var rows = TargetQuestDrops.For(player, new NpcInstance { IsMonster = true, DropItemGroup = DropGroup }, data, quests);

        rows.Should().Equal(new TargetQuestDrops.Row(AnimalBlood, 2, 3));
    }

    [Fact]
    public void ANonMonsterListsNothing()
    {
        var (data, quests) = Sources();
        var player = Player();
        player.Quest.QuestMap[BloodQuest] = (byte)QuestStatus.Active;

        TargetQuestDrops.For(player, new NpcInstance { IsMonster = false, DropItemGroup = DropGroup }, data, quests).Should().BeEmpty();
    }

    [Fact]
    public void TheRowsFollowTheHpFields()
    {
        var packet = TargetHpPacketWriter.Polled(12345, TargetHpPacketWriter.EchoPoll, 900, 450, 0,
            [new TargetQuestDrops.Row(AnimalBlood, 2, 3)]);
        packet.ResetOffset();
        packet.ReadInt().Should().Be(12345);
        packet.ReadByte();
        packet.ReadInt();
        packet.ReadInt();
        packet.ReadInt();
        packet.ReadInt();
        packet.ReadByte().Should().Be(1);
        packet.ReadInt().Should().Be(AnimalBlood);
        packet.ReadUShort().Should().Be(2);
        packet.ReadUShort().Should().Be(3);
        packet.RemainingBytes.Should().Be(0);
    }

    [Fact]
    public void WithoutRowsThePacketIsUnchanged() =>
        TargetHpPacketWriter.Polled(1, 0, 10, 10, 0).GetLength().Should().Be(TargetHpPacketWriter.Polled(1, 0, 10, 10, 0, []).GetLength());
}
