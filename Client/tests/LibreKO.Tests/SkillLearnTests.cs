using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class SkillLearnTests
{
    private const int NoTree = 0;
    private const int AttackTree = 5;
    private const int NoviceWarrior = 105;
    private const int MasterWarrior = 106;

    [Fact]
    public void ABasicSkillWaitsForTheLevel()
    {
        Assert.Equal(new SkillLearnInfo(SkillLearnState.Locked, 0, 10, false), SkillLearn.For(NoTree, 10, 0, NoviceWarrior, 5));
        Assert.Equal(SkillLearnState.Learned, SkillLearn.For(NoTree, 10, 0, NoviceWarrior, 10).State);
    }

    [Fact]
    public void AMasterySkillWithinTheCapIsLearnable()
    {
        Assert.Equal(new SkillLearnInfo(SkillLearnState.Learnable, 6, 0, false), SkillLearn.For(AttackTree, 10, 4, NoviceWarrior, 60));
        Assert.Equal(SkillLearnState.Learned, SkillLearn.For(AttackTree, 10, 10, NoviceWarrior, 60).State);
    }

    [Fact]
    public void AMasterySkillAboveTheCapWaitsForTheLevel()
    {
        Assert.Equal(new SkillLearnInfo(SkillLearnState.Locked, 26, 30, false), SkillLearn.For(AttackTree, 30, 4, NoviceWarrior, 20));
    }

    [Fact]
    public void TheMasterTreeNeedsTheClassChangeThenLevelsAboveSixty()
    {
        Assert.True(SkillLearn.For(MasteryPoints.MasterTree, 5, 0, NoviceWarrior, 70).NeedsClassChange);
        Assert.Equal(65, SkillLearn.For(MasteryPoints.MasterTree, 5, 0, MasterWarrior, 62).LevelNeeded);
        Assert.Equal(SkillLearnState.Learnable, SkillLearn.For(MasteryPoints.MasterTree, 5, 0, MasterWarrior, 66).State);
    }
}
