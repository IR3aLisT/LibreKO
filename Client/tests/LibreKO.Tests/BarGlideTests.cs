using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class BarGlideTests
{
    [Fact]
    public void DamageGlidesDownWhileTheTrailHoldsThenCatchesUp()
    {
        var glide = new BarGlide();
        glide.Snap(1f);

        glide.Aim(0.5f);
        glide.Step(0.1);

        Assert.True(glide.Shown < 1f && glide.Shown > 0.5f);
        Assert.Equal(1f, glide.Trail);

        while (glide.Step(0.05)) { }

        Assert.Equal(0.5f, glide.Shown);
        Assert.Equal(0.5f, glide.Trail);
    }

    [Fact]
    public void HealingRisesWithoutATrail()
    {
        var glide = new BarGlide();
        glide.Snap(0.2f);

        glide.Aim(0.6f);
        glide.Step(0.1);

        Assert.Equal(glide.Shown, glide.Trail);
        Assert.True(glide.Shown > 0.2f);
    }
}
