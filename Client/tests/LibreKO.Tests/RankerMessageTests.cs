using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class RankerMessageTests
{
    [Theory]
    [InlineData(-1, 44906)]
    [InlineData(-2, 44907)]
    public void ViewRefusalsUseTheirTexts(short result, int text) =>
        Assert.Equal(text, RankerMessage.ViewTextId(result));

    [Theory]
    [InlineData(RankerMessage.Done, 44908)]
    [InlineData(-1, 44910)]
    [InlineData(-2, 44911)]
    [InlineData(-3, 44912)]
    [InlineData(-4, 44913)]
    public void WriteResultsUseTheirTexts(short result, int text) =>
        Assert.Equal(text, RankerMessage.WriteTextId(result));

    [Theory]
    [InlineData(RankerMessage.Done, 44916)]
    [InlineData(-3, 44907)]
    [InlineData(-5, 44913)]
    [InlineData(-6, 44917)]
    [InlineData(0, 0)]
    public void EquipmentChangeResultsUseTheirTexts(short result, int text) =>
        Assert.Equal(text, RankerMessage.RedressTextId(result));

    [Fact]
    public void TheFirstThreeStatueTypesAreKarus()
    {
        Assert.True(RankerMessage.Karus(RankerStatue.KarusFirstType + 2));
        Assert.False(RankerMessage.Karus(RankerStatue.ElMoradFirstType));
    }
}
