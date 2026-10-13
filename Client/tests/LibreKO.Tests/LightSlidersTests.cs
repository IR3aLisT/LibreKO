using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class LightSlidersTests
{
    [Theory]
    [InlineData(40, 60)]
    [InlineData(103, 105)]
    [InlineData(102, 100)]
    [InlineData(500, 160)]
    public void BrightnessSnapsToFivePercentInsideItsRange(int asked, int kept)
    {
        Assert.Equal(kept, LightSliders.Brightness.Clamp(asked));
    }

    [Fact]
    public void TheRangesMatchTheOptionsWindow()
    {
        Assert.Equal(new LightRange(80, 130), LightSliders.Contrast);
        Assert.Equal(new LightRange(50, 200), LightSliders.NightBrightness);
        Assert.Equal(0, LightSliders.Fog.Clamp(-10));
        Assert.Equal(1.5f, LightRange.Multiplier(150));
    }
}
