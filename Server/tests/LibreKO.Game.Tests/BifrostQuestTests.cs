using System.Runtime.CompilerServices;
using FluentAssertions;
using LibreKO.Quests;
using LibreKO.Quests.Binding;
using LibreKO.Quests.Runtime;
using NSubstitute;
using Xunit;

namespace LibreKO.Game.Tests;

public class BifrostQuestTests
{
    private static string BakedQuestPath(string name, [CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", "..", "LibreKO.Game", "Quests", name));

    private const int CrystalOfTrial = 389190000;

    [Fact]
    public void RothenQuest_Compiles_And_Binds_To_Npc_16095_In_Zone_31()
    {
        var text = File.ReadAllText(BakedQuestPath("16095_31.quest"));
        var compilation = QuestCompilation.Create(text, "16095_31.quest");

        compilation.Succeeded.Should().BeTrue(compilation.RenderDiagnostics());
        compilation.Program.Bindings.Should().Contain(b => b.NpcId == 16095 && b.ZoneId == 31);
        compilation.Program.TryGetEntry(QuestProgram.GreetingEvent, 0, out _).Should().BeTrue();
    }

    [Fact]
    public void RothenQuest_Greeting_Asks_Which_Mission()
    {
        var text = File.ReadAllText(BakedQuestPath("16095_31.quest"));
        var compilation = QuestCompilation.Create(text, "16095_31.quest");
        var host = Substitute.For<IQuestHost>();
        var interpreter = new QuestInterpreter(compilation.Program, host);

        compilation.Program.TryGetEntry(QuestProgram.GreetingEvent, 0, out var greetingId).Should().BeTrue();
        interpreter.Run(greetingId).Failure.Should().BeNull();

        host.Received(1).ShowDialog(
            DialogStyle.Talk,
            Arg.Any<int>(),
            Arg.Is<DialogLine>(l => l.Text!.Contains("Knight of the Sky")),
            Arg.Is<IReadOnlyList<DialogButton>>(buttons =>
                buttons.Count == 1 && buttons[0].Label.Text == "Confirm"));
    }

    [Fact]
    public void RothenQuest_Without_Crystal_Offers_Production()
    {
        var text = File.ReadAllText(BakedQuestPath("16095_31.quest"));
        var compilation = QuestCompilation.Create(text, "16095_31.quest");
        var host = Substitute.For<IQuestHost>();
        var interpreter = new QuestInterpreter(compilation.Program, host);
        compilation.Program.EventNames.TryGetValue("choose_mission", out var chooseEventId).Should().BeTrue();

        interpreter.Run(chooseEventId).Failure.Should().BeNull();

        host.Received(1).ShowDialog(
            DialogStyle.Talk,
            Arg.Any<int>(),
            Arg.Is<DialogLine>(l => l.Text!.Contains("produce the Crystal of Trial")),
            Arg.Is<IReadOnlyList<DialogButton>>(buttons =>
                buttons.Count == 2
                && buttons.Any(b => b.Label.Text == "Production")
                && buttons.Any(b => b.Label.Text == "Cancel")));
    }

    [Fact]
    public void RothenQuest_With_Crystal_Offers_Entry()
    {
        var text = File.ReadAllText(BakedQuestPath("16095_31.quest"));
        var compilation = QuestCompilation.Create(text, "16095_31.quest");
        var host = Substitute.For<IQuestHost>();
        host.ItemCount(CrystalOfTrial).Returns(1);
        var interpreter = new QuestInterpreter(compilation.Program, host);
        compilation.Program.EventNames.TryGetValue("choose_mission", out var chooseEventId).Should().BeTrue();

        interpreter.Run(chooseEventId).Failure.Should().BeNull();

        host.Received(1).ShowDialog(
            DialogStyle.Talk,
            Arg.Any<int>(),
            Arg.Is<DialogLine>(l => l.Text!.Contains("enter the Lost Chamber")),
            Arg.Is<IReadOnlyList<DialogButton>>(buttons =>
                buttons.Count == 2
                && buttons.Any(b => b.Label.Text == "Enter")
                && buttons.Any(b => b.Label.Text == "Cancel")));
    }

    private static readonly int[] CraftFragmentIds =
    [
        389160000, 389161000, 389162000, 389163000
    ];

    private static readonly int[] EnterItemIds =
    [
        CrystalOfTrial, 389164000, 389165000, 389166000
    ];

    [Fact]
    public void RothenQuest_CraftCrystal_With4Fragments_Takes4AndGivesCrystal()
    {
        var text = File.ReadAllText(BakedQuestPath("16095_31.quest"));
        var compilation = QuestCompilation.Create(text, "16095_31.quest");
        var host = Substitute.For<IQuestHost>();
        foreach (var fragId in CraftFragmentIds)
        {
            host.ItemCount(fragId).Returns(1);
        }

        var interpreter = new QuestInterpreter(compilation.Program, host);
        compilation.Program.EventNames.TryGetValue("craft_crystal", out var craftEventId).Should().BeTrue();

        interpreter.Run(craftEventId).Failure.Should().BeNull();

        foreach (var fragId in CraftFragmentIds)
        {
            host.Received(1).TakeItem(fragId, 1);
        }
        host.Received(1).GiveItem(CrystalOfTrial, 1, 0);
    }

    [Fact]
    public void RothenQuest_CraftCrystal_MissingAnyFragment_DoesNotCraft()
    {
        var text = File.ReadAllText(BakedQuestPath("16095_31.quest"));
        var compilation = QuestCompilation.Create(text, "16095_31.quest");
        var host = Substitute.For<IQuestHost>();
        host.ItemCount(389160000).Returns(1);
        host.ItemCount(389161000).Returns(1);
        host.ItemCount(389162000).Returns(1);
        host.ItemCount(389163000).Returns(0);

        var interpreter = new QuestInterpreter(compilation.Program, host);
        compilation.Program.EventNames.TryGetValue("craft_crystal", out var craftEventId).Should().BeTrue();

        interpreter.Run(craftEventId).Failure.Should().BeNull();

        host.DidNotReceive().TakeItem(Arg.Any<int>(), Arg.Any<int>());
        host.DidNotReceive().GiveItem(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>());
        host.Received(1).ShowDialog(
            DialogStyle.Talk,
            Arg.Any<int>(),
            Arg.Is<DialogLine>(l => l.Text!.Contains("Fragment of Sloth")),
            Arg.Any<IReadOnlyList<DialogButton>>());
    }

    [Fact]
    public void RothenQuest_Enter_With_Crystal_And_Remaining_Fragments_Takes_Items_And_Teleports_Party()
    {
        var text = File.ReadAllText(BakedQuestPath("16095_31.quest"));
        var compilation = QuestCompilation.Create(text, "16095_31.quest");
        var host = Substitute.For<IQuestHost>();
        foreach (var itemId in EnterItemIds)
        {
            host.ItemCount(itemId).Returns(1);
        }

        var interpreter = new QuestInterpreter(compilation.Program, host);
        compilation.Program.EventNames.TryGetValue("enter_chamber", out var enterEventId).Should().BeTrue();

        interpreter.Run(enterEventId).Failure.Should().BeNull();

        foreach (var itemId in EnterItemIds)
        {
            host.Received(1).TakeItem(itemId, 1);
        }
        host.Received(1).TeleportPartyToZone(31, 940, 186);
        host.DidNotReceive().TeleportToZone(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public void RothenQuest_Enter_Missing_Any_Item_Does_Not_Take_Items_And_Warns_Player()
    {
        var text = File.ReadAllText(BakedQuestPath("16095_31.quest"));
        var compilation = QuestCompilation.Create(text, "16095_31.quest");
        var host = Substitute.For<IQuestHost>();
        host.ItemCount(CrystalOfTrial).Returns(1);
        host.ItemCount(389164000).Returns(1);
        host.ItemCount(389165000).Returns(0);
        host.ItemCount(389166000).Returns(1);

        var interpreter = new QuestInterpreter(compilation.Program, host);
        compilation.Program.EventNames.TryGetValue("enter_chamber", out var enterEventId).Should().BeTrue();

        interpreter.Run(enterEventId).Failure.Should().BeNull();

        host.DidNotReceive().TakeItem(Arg.Any<int>(), Arg.Any<int>());
        host.DidNotReceive().TeleportToZone(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>());
        host.DidNotReceive().TeleportPartyToZone(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>());

        host.Received(1).ShowDialog(
            DialogStyle.Talk,
            Arg.Any<int>(),
            Arg.Is<DialogLine>(l => l.Text!.Contains("Fragment of Jealousy")),
            Arg.Any<IReadOnlyList<DialogButton>>());
    }

    [Fact]
    public void AsanQuest_Compiles_And_Binds_To_Npc_16094_In_Zone_31()
    {
        var text = File.ReadAllText(BakedQuestPath("16094_31.quest"));
        var compilation = QuestCompilation.Create(text, "16094_31.quest");

        compilation.Succeeded.Should().BeTrue(compilation.RenderDiagnostics());
        compilation.Program.Bindings.Should().Contain(b => b.NpcId == 16094 && b.ZoneId == 31);
        compilation.Program.TryGetEntry(QuestProgram.GreetingEvent, 0, out _).Should().BeTrue();
    }

    [Fact]
    public void AsanQuest_Leave_Teleports_Level30Plus_To_Ronark_Land()
    {
        var text = File.ReadAllText(BakedQuestPath("16094_31.quest"));
        var compilation = QuestCompilation.Create(text, "16094_31.quest");
        var host = Substitute.For<IQuestHost>();
        host.PlayerLevel.Returns(80);
        host.PlayerNation.Returns(1); // Karus

        var interpreter = new QuestInterpreter(compilation.Program, host);
        compilation.Program.EventNames.TryGetValue("leave_bifrost", out var leaveEventId).Should().BeTrue();

        interpreter.Run(leaveEventId).Failure.Should().BeNull();
        host.Received(1).TeleportToZone(71, 1014, 1020);
    }
}
