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
    public void RothenQuest_Greeting_Presents_Options_To_Enter_Lost_Chamber()
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
            Arg.Is<DialogLine>(l => l.Text!.Contains("Lost Chamber") || l.Text.Contains("Ultima")),
            Arg.Is<IReadOnlyList<DialogButton>>(buttons =>
                buttons.Count == 2
                && buttons.Any(b => b.Label.Text!.Contains("Enter the Lost Chamber"))
                && buttons.Any(b => b.Label.Text!.Contains("Cancel"))));
    }

    [Fact]
    public void RothenQuest_Enter_With_Crystal_As_Party_Leader_Takes_Item_And_Teleports_Party()
    {
        var text = File.ReadAllText(BakedQuestPath("16095_31.quest"));
        var compilation = QuestCompilation.Create(text, "16095_31.quest");
        var host = Substitute.For<IQuestHost>();
        host.ItemCount(CrystalOfTrial).Returns(1);
        host.IsPartyLeader.Returns(true);

        var interpreter = new QuestInterpreter(compilation.Program, host);
        compilation.Program.EventNames.TryGetValue("enter_chamber", out var enterEventId).Should().BeTrue();

        interpreter.Run(enterEventId).Failure.Should().BeNull();

        host.Received(1).TakeItem(CrystalOfTrial, 1);
        host.Received(1).TeleportPartyToZone(31, 940, 186);
        host.DidNotReceive().TeleportToZone(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public void RothenQuest_Enter_With_Crystal_As_Solo_Takes_Item_And_Teleports_Individual()
    {
        var text = File.ReadAllText(BakedQuestPath("16095_31.quest"));
        var compilation = QuestCompilation.Create(text, "16095_31.quest");
        var host = Substitute.For<IQuestHost>();
        host.ItemCount(CrystalOfTrial).Returns(1);
        host.IsPartyLeader.Returns(false);

        var interpreter = new QuestInterpreter(compilation.Program, host);
        compilation.Program.EventNames.TryGetValue("enter_chamber", out var enterEventId).Should().BeTrue();

        interpreter.Run(enterEventId).Failure.Should().BeNull();

        host.Received(1).TakeItem(CrystalOfTrial, 1);
        host.Received(1).TeleportToZone(31, 940, 186);
        host.DidNotReceive().TeleportPartyToZone(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public void RothenQuest_Enter_Without_Crystal_Does_Not_Take_Item_And_Warns_Player()
    {
        var text = File.ReadAllText(BakedQuestPath("16095_31.quest"));
        var compilation = QuestCompilation.Create(text, "16095_31.quest");
        var host = Substitute.For<IQuestHost>();
        host.ItemCount(CrystalOfTrial).Returns(0);

        var interpreter = new QuestInterpreter(compilation.Program, host);
        compilation.Program.EventNames.TryGetValue("enter_chamber", out var enterEventId).Should().BeTrue();

        interpreter.Run(enterEventId).Failure.Should().BeNull();

        host.DidNotReceive().TakeItem(Arg.Any<int>(), Arg.Any<int>());
        host.DidNotReceive().TeleportToZone(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>());
        host.DidNotReceive().TeleportPartyToZone(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>());

        host.Received(1).ShowDialog(
            DialogStyle.Talk,
            Arg.Any<int>(),
            Arg.Is<DialogLine>(l => l.Text!.Contains("do not have a Crystal of Trial")),
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
