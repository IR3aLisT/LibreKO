using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class WhisperLookTests
{
    [Theory]
    [InlineData(WhisperSize.Small, 200, 110, 140)]
    [InlineData(WhisperSize.Medium, 236, 140, 168)]
    [InlineData(WhisperSize.Large, 320, 210, 240)]
    public void EachSizeHasTheRetailLogBox(WhisperSize size, int width, int height, int wrap)
    {
        var box = WhisperLook.BoxOf(size);
        Assert.Equal(width, box.Width);
        Assert.Equal(height, box.Height);
        Assert.Equal(wrap, box.Wrap);
    }

    [Theory]
    [InlineData(0.1f, 0.25f)]
    [InlineData(0.5f, 0.5f)]
    [InlineData(1.4f, 1f)]
    public void TransparencyStaysBetweenAQuarterAndOpaque(float asked, float kept)
    {
        Assert.Equal(kept, WhisperLook.ClampAlpha(asked));
    }

    [Fact]
    public void AnUnknownSavedSizeFallsBackToTheDefault()
    {
        Assert.Equal(WhisperLook.DefaultSize, WhisperLook.SizeFrom(9));
        Assert.Equal(WhisperSize.Small, WhisperLook.SizeFrom(0));
    }
}
