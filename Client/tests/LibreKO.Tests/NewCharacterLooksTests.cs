using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class NewCharacterLooksTests
{
    private const string Row =
        "{\"11_201\": {\"right\": 1110531575, \"left\": 0, \"top\": 220001005, \"bottom\": 220002005, "
        + "\"gloves\": 220004005, \"boots\": 220005005, \"helmet\": 220003005, \"stats\": [65,65,60,50,50], \"bonus\": 10}}";

    [Fact]
    public void ARowIsKeyedByRaceAndClass()
    {
        var looks = NewCharacterLooks.Parse(Row);
        Assert.True(looks.TryGetValue((11, 201), out var look));
        Assert.Equal(1110531575, look.Right);
        Assert.Equal(220003005, look.Helmet);
    }

    [Fact]
    public void ThePreviewWearsTheSetAndOnlyLosesTheHelmetWhenAsked()
    {
        var look = NewCharacterLooks.Parse(Row)[(11, 201)];
        var dressed = NewCharacterLooks.Gear(look, helmet: true);
        var bareHead = NewCharacterLooks.Gear(look, helmet: false);
        Assert.Equal(220001005, dressed[InventoryConstants.VisBreast]);
        Assert.Equal(220003005, dressed[InventoryConstants.VisHead]);
        Assert.Equal(0, bareHead[InventoryConstants.VisHead]);
        Assert.Equal(1110531575, bareHead[InventoryConstants.VisRightHand]);
    }
}
