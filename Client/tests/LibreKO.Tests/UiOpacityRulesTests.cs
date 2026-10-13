using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class UiOpacityRulesTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(128, 128)]
    [InlineData(300, 255)]
    public void TheAlphaStaysBetweenOneAndOpaque(int asked, int kept) => Assert.Equal(kept, UiOpacityRules.Clamp(asked));

    [Theory]
    [InlineData(1, true)]
    [InlineData(99, true)]
    [InlineData(120, false)]
    [InlineData(0, false)]
    public void OnlyTheHudLayersFade(int layer, bool fades) => Assert.Equal(fades, UiOpacityRules.Fades(layer));
}
