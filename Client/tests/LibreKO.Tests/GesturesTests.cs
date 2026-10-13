using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class GesturesTests
{
    private static string Names(int id, string fallback) => fallback;

    [Theory]
    [InlineData(Gestures.Greeting1, 104)]
    [InlineData(Gestures.Greeting2, 105)]
    [InlineData(Gestures.Greeting3, 106)]
    [InlineData(Gestures.Provoke1, 108)]
    [InlineData(Gestures.Provoke2, 107)]
    [InlineData(Gestures.Provoke3, 109)]
    [InlineData(Gestures.Stomp, 140)]
    [InlineData(Gestures.Kick, 143)]
    public void EachGesturePlaysItsClip(int action, int clip) => Assert.Equal(clip, Gestures.ClipFor(action));

    [Fact]
    public void AnUnknownGesturePlaysNothing() => Assert.Equal(-1, Gestures.ClipFor(5));

    [Theory]
    [InlineData(0, Gestures.Stomp)]
    [InlineData(1, Gestures.Kick)]
    [InlineData(7, Gestures.Kick)]
    public void TheSpaceTauntIsAStompOrAKick(int roll, int action) => Assert.Equal(action, Gestures.TauntFor(roll));

    [Theory]
    [InlineData(0, Nations.Karus, Gestures.StompSkill)]
    [InlineData(1, Nations.ElMorad, Gestures.KickSkill)]
    [InlineData(2, Nations.Karus, Gestures.KarusFlagSkill)]
    [InlineData(5, Nations.ElMorad, Gestures.ElMoradFlagSkill)]
    public void ACorpseGetsAStompAKickOrTheNationsFlag(int roll, int nation, int skill) =>
        Assert.Equal(skill, Gestures.CorpseSkillFor(roll, nation));

    [Fact]
    public void OnlyTheFourCorpseSkillsCount()
    {
        Assert.True(Gestures.IsCorpseSkill(Gestures.StompSkill));
        Assert.True(Gestures.IsCorpseSkill(Gestures.ElMoradFlagSkill));
        Assert.False(Gestures.IsCorpseSkill(490050));
    }

    [Theory]
    [InlineData("Greeting", Gestures.Greeting1)]
    [InlineData("greeting3", Gestures.Greeting3)]
    [InlineData("PROVOKE2", Gestures.Provoke2)]
    public void TheChatCommandsNameTheGestures(string word, int action) =>
        Assert.Equal(action, Gestures.CommandAction(word, Names));

    [Fact]
    public void OtherWordsAreNotGestures() => Assert.Null(Gestures.CommandAction("Greeting4", Names));
}
