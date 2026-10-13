using LibreKO.Domain;
using LibreKO.Network;
using Xunit;

namespace LibreKO.Tests;

public class ChaosBattleTests
{
    [Fact]
    public void TheLadderSortsByKillsThenDeathsAndSharesTiedRanks()
    {
        var lines = ChaosBattle.Ladder(new[]
        {
            new ChaosRankRow("C", 1, 0),
            new ChaosRankRow("A", 5, 2),
            new ChaosRankRow("B", 5, 2),
            new ChaosRankRow("D", 0, 3),
        }, "B");

        Assert.Equal(new[] { "A", "B", "C", "D" }, lines.ConvertAll(l => l.Name));
        Assert.Equal(new[] { 1, 1, 3, 4 }, lines.ConvertAll(l => l.Rank));
        Assert.True(lines[1].Own);
    }

    [Theory]
    [InlineData(18, 4)]
    [InlineData(10, 2)]
    [InlineData(2, 1)]
    public void TheWinnerCutFollowsTheTwentyPercentRule(int count, int cut) =>
        Assert.Equal(cut, ChaosBattle.WinnerCut(count));

    [Fact]
    public void OneRowShowsNoBadgeAndAllTiedShowsNoWinner()
    {
        var single = ChaosBattle.Ladder(new[] { new ChaosRankRow("A", 3, 0) }, "A");
        Assert.False(single[0].Winner);
        Assert.False(single[0].Loser);

        var tied = ChaosBattle.Ladder(new[] { new ChaosRankRow("A", 0, 0), new ChaosRankRow("B", 0, 0), new ChaosRankRow("C", 0, 0) }, "A");
        Assert.All(tied, l => Assert.False(l.Winner));
        Assert.All(tied, l => Assert.False(l.Loser));
    }

    [Fact]
    public void BeforeTheFinishYouReadAsYourOwnDeityAndOthersAsTheOther()
    {
        Assert.Equal(ChatSender.ElMoradDeityText, ChaosBattle.MaskedNameText(own: true, Nations.ElMorad));
        Assert.Equal(ChatSender.KarusDeityText, ChaosBattle.MaskedNameText(own: false, Nations.ElMorad));
        Assert.Equal(ChatSender.KarusDeityText, ChaosBattle.MaskedNameText(own: true, Nations.Karus));
    }

    [Fact]
    public void ZoneEightyFiveAllowsOnlyChaosSkillsAndCubeItems()
    {
        Assert.True(ZoneSkillTrees.Allowed(ChaosBattle.Zone, ChaosBattle.SkillTree, 0));
        Assert.False(ZoneSkillTrees.Allowed(ChaosBattle.Zone, 105, 0));
        Assert.True(ZoneSkillTrees.Allowed(21, 105, 0));
        Assert.True(ChaosBattle.ItemUsable(ChaosBattle.Zone, ChaosBattle.ChaosItemKind));
        Assert.False(ChaosBattle.ItemUsable(ChaosBattle.Zone, 0));
        Assert.True(ChaosBattle.KeptOnEntry(490230));
        Assert.False(ChaosBattle.KeptOnEntry(490231));
    }

    [Fact]
    public void TheRankingReaderTakesTheServersBytes()
    {
        var packet = new Packet(GameOpcodes.GS_RANK);
        packet.WriteByte(2);
        packet.WriteString("Zeus");
        packet.WriteShort(7);
        packet.WriteShort(2);
        packet.WriteString("Rikka");
        packet.WriteShort(1);
        packet.WriteShort(4);
        packet.WriteUInt(1500);
        packet.WriteUInt(3000);

        var ranking = ChaosRanking.Read(packet);

        Assert.Equal(2, ranking.Rows.Count);
        Assert.Equal(new ChaosRankRow("Zeus", 7, 2), ranking.Rows[0]);
        Assert.Equal(1500u, ranking.Exp);
        Assert.Equal(3000u, ranking.PremiumExp);
    }
}
