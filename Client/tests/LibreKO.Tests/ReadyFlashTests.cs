using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class ReadyFlashTests
{
    [Theory]
    [InlineData(0.0, 3)]
    [InlineData(0.079, 3)]
    [InlineData(0.08, 4)]
    [InlineData(0.89, 14)]
    [InlineData(0.96, 3)]
    public void SelectedSlotLoopsTheTwelveSkillUpFramesEveryEightyMilliseconds(double seconds, int frame)
    {
        Assert.Equal(frame, ReadyFlash.SelectedFrameAt(seconds));
    }
}
