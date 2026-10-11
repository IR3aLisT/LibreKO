using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class NestDungeonTests
{
    private const int Moradon = 21;
    private const int FirstNest = 81;
    private const int JuraidMountain = 87;

    [Theory]
    [InlineData(MonsterStoneResult.PartyLeaderOnly, 9912)]
    [InlineData(MonsterStoneResult.Failed, 9913)]
    [InlineData(MonsterStoneResult.Refused, 9913)]
    [InlineData(MonsterStoneResult.PartyLevelTooHigh, 9915)]
    [InlineData(MonsterStoneResult.CannotEnterHere, 9911)]
    public void RefusalsOpenAMessageBoxWithTheirText(MonsterStoneResult result, int textId)
    {
        var reply = NestDungeon.Reply(result, Moradon);
        Assert.Equal(textId, reply?.Id);
        Assert.True(reply?.Boxed);
    }

    [Fact]
    public void LowHealthGoesToTheLog()
    {
        Assert.False(NestDungeon.Reply(MonsterStoneResult.NotEnoughHealth, Moradon)?.Boxed);
        Assert.Equal(9919, NestDungeon.Reply(MonsterStoneResult.NotEnoughHealth, Moradon)?.Id);
    }

    [Fact]
    public void EnteringNeedsNoMessage()
    {
        Assert.Null(NestDungeon.Reply(MonsterStoneResult.Entered, Moradon));
    }

    [Theory]
    [InlineData(FirstNest)]
    [InlineData(JuraidMountain)]
    public void AZoneRefusalInsideAnInstanceSaysSo(int zone)
    {
        Assert.Equal(9914, NestDungeon.Reply(MonsterStoneResult.CannotEnterHere, zone)?.Id);
        Assert.True(NestDungeon.IsInstancedZone(zone));
    }

    [Theory]
    [InlineData(1, 55)]
    [InlineData(60, 1_080_050)]
    [InlineData(83, 2_858_985)]
    public void TheStoneExpIsFiveTimesTheLevelCubedPlusTen(int level, long exp)
    {
        Assert.Equal(exp, NestDungeon.ExpFor(level));
    }
}
