using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class CharacterLevelTextTests
{
    [Fact]
    public void TheRebirthLevelFollowsTheLevelOnlyOnceReborn()
    {
        Assert.Equal("Level 83", CharacterLevelText.Of(83, 0));
        Assert.Equal("Level 83 / 5", CharacterLevelText.Of(83, 5));
    }
}
