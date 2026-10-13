using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class NpcTitleTests
{
    [Theory]
    [InlineData("Patrick", "Guard", 13001, "Patrick")]
    [InlineData(null, "Guard", 13001, "Guard")]
    [InlineData("", "", 13001, "NPC #13001")]
    public void TheTableNameComesFirstThenTheSpawnedName(string? table, string? spawned, int npcId, string title) =>
        Assert.Equal(title, NpcTitle.Of(table, spawned, npcId));
}
