using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class NpcChoiceTests
{
    [Theory]
    [InlineData("Buy / Sell", NpcChoiceKind.Shop)]
    [InlineData("Repair", NpcChoiceKind.Repair)]
    [InlineData("Open the warehouse", NpcChoiceKind.Storage)]
    [InlineData("Teleport to Luferson Castle", NpcChoiceKind.Travel)]
    [InlineData("Show me the quests", NpcChoiceKind.Quest)]
    [InlineData("Goodbye", NpcChoiceKind.Close)]
    [InlineData("Where is the [Manager]?", NpcChoiceKind.Talk)]
    [InlineData("I'm a little tied up right now...", NpcChoiceKind.Talk)]
    [InlineData("Beginning of the story", NpcChoiceKind.Talk)]
    public void CaptionsAreSortedIntoKindsByWholeWords(string caption, NpcChoiceKind kind) =>
        Assert.Equal(kind, NpcChoice.Classify(caption));

    [Theory]
    [InlineData("1.   Upgrade Item", "Upgrade Item")]
    [InlineData("12. Close", "Close")]
    [InlineData("3D view", "3D view")]
    [InlineData("Close", "Close")]
    public void TheMenuNumberIsDropped(string label, string caption) =>
        Assert.Equal(caption, NpcChoice.Caption(label));

    [Theory]
    [InlineData("[Ready] Kecoon Captain hunt", "Kecoon Captain hunt", true)]
    [InlineData("[In progress] Bandicoot hunt", "[In progress] Bandicoot hunt", false)]
    [InlineData("Ready for war", "Ready for war", false)]
    public void TheReadyMarkBecomesAFlag(string label, string caption, bool ready) =>
        Assert.Equal((caption, ready), NpcChoice.SplitReady(label));
}
