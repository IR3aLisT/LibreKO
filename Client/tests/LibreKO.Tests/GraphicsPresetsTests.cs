using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class GraphicsPresetsTests
{
    [Fact]
    public void EveryNamedPresetIsRecognisedFromItsOwnValues()
    {
        foreach (var preset in GraphicsPresets.Named)
            Assert.Equal(preset, GraphicsPresets.Match(GraphicsPresets.Of(preset)));
    }

    [Fact]
    public void AnyHandChangeReadsAsCustom()
    {
        var high = GraphicsPresets.Of(GraphicsPreset.High);

        Assert.Equal(GraphicsPreset.Custom, GraphicsPresets.Match(high with { Bloom = false }));
        Assert.Equal(GraphicsPreset.Custom, GraphicsPresets.Match(high with { ViewDistance = 0.6f }));
    }

    [Fact]
    public void HighKeepsTodaysShadowAndOcclusionLevels()
    {
        var high = GraphicsPresets.Of(GraphicsPreset.High);

        Assert.Equal(QualityLevel.Medium, high.Shadows);
        Assert.Equal(QualityLevel.Medium, high.Ssao);
    }
}

public class CospreFilterTests
{
    [Fact]
    public void HiddenKindsAreBlankedInACopy()
    {
        var gear = new int[InventoryConstants.VisualSlotCount];
        gear[InventoryConstants.VisCosWing] = 810638000;
        gear[InventoryConstants.VisCosFairy] = 700039000;
        gear[InventoryConstants.VisCosGloveLeft] = 520001000;

        var shown = CospreFilter.Hide(gear, CospreKinds.Wings | CospreKinds.PathosGlow)!;

        Assert.Equal(0, shown[InventoryConstants.VisCosWing]);
        Assert.Equal(700039000, shown[InventoryConstants.VisCosFairy]);
        Assert.Equal(0, shown[InventoryConstants.VisCosGloveLeft]);
        Assert.Equal(810638000, gear[InventoryConstants.VisCosWing]);
    }

    [Fact]
    public void NothingHiddenReturnsTheSameGear()
    {
        var gear = new int[InventoryConstants.VisualSlotCount];

        Assert.Same(gear, CospreFilter.Hide(gear, CospreKinds.None));
    }
}
