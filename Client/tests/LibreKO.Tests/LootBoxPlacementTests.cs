using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class LootBoxPlacementTests
{
    [Theory]
    [InlineData(0.9f, 0.9f, 1f)]
    [InlineData(4f, 4f, 1f)]
    [InlineData(8f, 6f, 1f)]
    [InlineData(10f, 6f, 2f)]
    [InlineData(25f, 6f, 2f)]
    public void TheBoxSitsBesideTheCorpseAndDoublesForHugeMonsters(float radius, float offset, float scale)
    {
        Assert.Equal(offset, LootBoxPlacement.Offset(radius));
        Assert.Equal(scale, LootBoxPlacement.Scale(radius));
    }

    [Fact]
    public void TheBoxWaitsForTheDeathClip()
    {
        Assert.False(LootBoxPlacement.Ready(ownerKnown: true, ownerDead: false, now: 10, deathClipEnd: 0));
        Assert.False(LootBoxPlacement.Ready(ownerKnown: true, ownerDead: true, now: 10, deathClipEnd: 11.5));
        Assert.True(LootBoxPlacement.Ready(ownerKnown: true, ownerDead: true, now: 12, deathClipEnd: 11.5));
        Assert.True(LootBoxPlacement.Ready(ownerKnown: false, ownerDead: false, now: 0, deathClipEnd: 0));
    }
}
