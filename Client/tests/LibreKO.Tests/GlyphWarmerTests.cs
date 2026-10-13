using Xunit;

namespace LibreKO.Tests;

public class GlyphWarmerTests
{
    [Theory]
    [InlineData(1f, 64)]
    [InlineData(0.8f, 51)]
    [InlineData(1.15f, 73)]
    [InlineData(1.25f, 80)]
    [InlineData(1.5f, 96)]
    [InlineData(2f, 128)]
    [InlineData(0.01f, 6)]
    [InlineData(500f, 6400)]
    public void OversamplingTruncatesToSixtyFourthsLikeTheTextServer(float oversampling, int level)
        => Assert.Equal(level, GlyphWarmer.Level(oversampling));

    [Theory]
    [InlineData(11, 64, 11)]
    [InlineData(11, 128, 22)]
    [InlineData(14, 96, 21)]
    [InlineData(13, 96, 0)]
    [InlineData(12, 80, 15)]
    [InlineData(11, 80, 0)]
    [InlineData(11, 51, 0)]
    [InlineData(64, 51, 51)]
    public void OnlyWholePixelRasterSizesCanBeRenderedAhead(int size, int level, int raster)
        => Assert.Equal(raster, GlyphWarmer.RasterSize(size, level));

    [Theory]
    [InlineData(4, 64, 4)]
    [InlineData(4, 128, 8)]
    [InlineData(4, 80, 5)]
    [InlineData(4, 51, 3)]
    [InlineData(5, 96, 7)]
    [InlineData(0, 128, 0)]
    public void OutlineScalesWithTheLevelAndTruncates(int outline, int level, int raster)
        => Assert.Equal(raster, GlyphWarmer.RasterOutline(outline, level));
}
