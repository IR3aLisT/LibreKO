using LibreKO.Network;
using Xunit;

namespace LibreKO.Tests;

public class AchievementTitleAnswerTests
{
    [Fact]
    public void ASelectAnswerSkipsTheMainIdAndReadsTheTitleAndResult()
    {
        var p = new Packet(GameOpcodes.GS_ACHIEVEMENT);
        p.WriteUShort(0);
        p.WriteUShort(42);
        p.WriteShort(AchievementTitleAnswer.Done);
        p.ResetOffset();

        var answer = AchievementTitleAnswer.Read(AchievementTitleAnswer.SelectCover, p);

        Assert.Equal(new AchievementTitleAnswer(AchievementTitleAnswer.SelectCover, false, 42, AchievementTitleAnswer.Done, 0), answer);
    }

    [Fact]
    public void ACooldownCarriesItsSeconds()
    {
        var select = new Packet(GameOpcodes.GS_ACHIEVEMENT);
        select.WriteUShort(0);
        select.WriteUShort(42);
        select.WriteShort(AchievementTitleAnswer.SelectCooldown);
        select.WriteUShort(30);
        select.ResetOffset();
        var reset = new Packet(GameOpcodes.GS_ACHIEVEMENT);
        reset.WriteShort(AchievementTitleAnswer.ResetCooldown);
        reset.WriteUShort(12);
        reset.ResetOffset();

        Assert.Equal(30, AchievementTitleAnswer.Read(AchievementTitleAnswer.SelectSkill, select)?.Seconds);
        Assert.True(AchievementTitleAnswer.Read(AchievementTitleAnswer.ResetCover, reset)?.Cooldown);
    }

    [Fact]
    public void AResetAnswerIsOnlyTheResult()
    {
        var p = new Packet(GameOpcodes.GS_ACHIEVEMENT);
        p.WriteShort(AchievementTitleAnswer.Done);
        p.ResetOffset();

        var answer = AchievementTitleAnswer.Read(AchievementTitleAnswer.ResetCover, p);

        Assert.True(answer?.Reset);
        Assert.Equal(AchievementTitleAnswer.Done, answer?.Result);
    }
}
