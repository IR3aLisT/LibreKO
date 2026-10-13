using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class DeathPoseTests
{
    [Theory]
    [InlineData(22, DeathPose.Disjoint)]
    [InlineData(63, DeathPose.Disjoint)]
    [InlineData(21, DeathPose.KnockDown)]
    [InlineData(51, DeathPose.KnockDown)]
    [InlineData(70, DeathPose.KeepPosition)]
    [InlineData(0, DeathPose.KeepPosition)]
    public void TheKillersWeaponPicksThePose(int kind, DeathPose pose)
    {
        Assert.Equal(pose, DeathPoses.ForKillerKind(kind));
    }

    [Theory]
    [InlineData(DeathPose.Disjoint, false, 10)]
    [InlineData(DeathPose.KnockDown, false, 9)]
    [InlineData(DeathPose.KeepPosition, false, 8)]
    [InlineData(DeathPose.Disjoint, true, 11)]
    [InlineData(DeathPose.KnockDown, true, 11)]
    [InlineData(DeathPose.KeepPosition, true, 10)]
    public void PlayersAndMonstersUseThe2619DeathClips(DeathPose pose, bool monster, int clip)
    {
        Assert.Equal(clip, DeathPoses.ClipIndex(pose, monster));
    }

    [Fact]
    public void OnlyFallingPosesPushAndNeverModel30300()
    {
        Assert.True(DeathPoses.Pushes(DeathPose.Disjoint, 1001));
        Assert.True(DeathPoses.Pushes(DeathPose.KnockDown, 1001));
        Assert.False(DeathPoses.Pushes(DeathPose.KeepPosition, 1001));
        Assert.False(DeathPoses.Pushes(DeathPose.Disjoint, DeathPoses.UnpushedModel));
    }

    [Theory]
    [InlineData(0.1f, 5f)]
    [InlineData(0.2f, 5f)]
    [InlineData(0.3f, 0.5f)]
    [InlineData(0.4f, 0f)]
    [InlineData(1f, 0f)]
    public void ThePushSlowsToAStopAtFourTenths(float seconds, float speed)
    {
        Assert.Equal(speed, DeathPoses.PushSpeedAt(seconds), 3);
    }

    [Fact]
    public void SmallBodiesSlideFurther()
    {
        Assert.Equal(1f * 5f * 0.1f, DeathPoses.PushStep(0.1f, 1f, 0.1f), 4);
        Assert.True(DeathPoses.PushStep(0.1f, 0.5f, 0.1f) > DeathPoses.PushStep(0.1f, 2f, 0.1f));
    }
}
