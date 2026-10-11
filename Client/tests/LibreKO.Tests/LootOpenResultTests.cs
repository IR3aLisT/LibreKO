using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class LootOpenResultTests
{
    [Theory]
    [InlineData(LootOpenResult.Empty, 6065, true)]
    [InlineData(LootOpenResult.Gone, 6065, true)]
    [InlineData(LootOpenResult.Expired, 6065, true)]
    [InlineData(LootOpenResult.NotYours, 6514, false)]
    [InlineData(LootOpenResult.NotYourParty, 6514, false)]
    [InlineData(LootOpenResult.Captcha, 43583, false)]
    public void ARefusedOpenShowsTheRetailTextAndDropsOnlyEmptyBoxes(int result, int textId, bool empty)
    {
        Assert.Equal(textId, LootOpenResult.TextId(result));
        Assert.Equal(empty, LootOpenResult.BoxIsEmpty(result));
    }
}
