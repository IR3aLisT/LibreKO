using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class BattleMusicTests
{
    [Theory]
    [InlineData(SkillTarget.EnemyOnly, true)]
    [InlineData(SkillTarget.AreaEnemy, false)]
    [InlineData(SkillTarget.FriendWithMe, false)]
    [InlineData(SkillTarget.Self, false)]
    public void OnlyASingleEnemySkillStartsTheBattleTrack(int moral, bool starts) =>
        Assert.Equal(starts, BattleMusic.Starts(moral));

    [Fact]
    public void EachNationHasItsOwnBattleTrack()
    {
        Assert.Equal(1001, BattleMusic.Track(1001, 2002, karus: true));
        Assert.Equal(2002, BattleMusic.Track(1001, 2002, karus: false));
    }

    [Theory]
    [InlineData(true, 11.9f, false)]
    [InlineData(true, 12f, true)]
    [InlineData(true, float.MaxValue, true)]
    [InlineData(false, 50f, false)]
    public void TheTrackEndsOnceNoHostileIsWithinTwelveMetres(bool playing, float nearest, bool ends) =>
        Assert.Equal(ends, BattleMusic.Ends(playing, nearest));
}
