using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class LoadingGlideTests
{
    [Fact]
    public void AHigherValueGlidesThereOverAThirdOfASecond()
    {
        var glide = new LoadingGlide();
        glide.Set(0.5, 10);

        Assert.Equal(0, glide.At(10), 3);
        Assert.InRange(glide.At(10 + LoadingGlide.GlideSeconds / 2), 0.01, 0.49);
        Assert.Equal(0.5, glide.At(10 + LoadingGlide.GlideSeconds), 3);
    }

    [Fact]
    public void ALowerValueIsShownAtOnce()
    {
        var glide = new LoadingGlide();
        glide.Set(0.8, 0);
        glide.Set(0.3, 1);

        Assert.Equal(0.3, glide.At(1), 3);
    }

    [Fact]
    public void ANewTargetStartsFromWhereTheBarIs()
    {
        var glide = new LoadingGlide();
        glide.Set(0.4, 0);
        double midway = glide.At(LoadingGlide.GlideSeconds / 2);
        glide.Set(0.9, LoadingGlide.GlideSeconds / 2);

        Assert.Equal(midway, glide.At(LoadingGlide.GlideSeconds / 2), 3);
        Assert.Equal(0.9, glide.At(LoadingGlide.GlideSeconds * 2), 3);
    }

    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(0.624, 62)]
    [InlineData(1.2, 100)]
    public void ThePercentIsRoundedAndClamped(double value, int percent) => Assert.Equal(percent, LoadingGlide.Percent(value));
}
