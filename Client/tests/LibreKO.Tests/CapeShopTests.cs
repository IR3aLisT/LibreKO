using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class CapeShopTests
{
    [Fact]
    public void OnlyCastleCapesTakeTheVoucher()
    {
        Assert.True(CapeShop.VoucherAllowed(castle: true));
        Assert.False(CapeShop.VoucherAllowed(castle: false));
    }

    [Fact]
    public void PaintingAddsItsContToTheCape() =>
        Assert.Equal(150_000 + CapeShop.PaintCont, CapeShop.ContCost(150_000, painting: true));

    [Fact]
    public void CastleCapesConfirmWithTheCastellanText()
    {
        Assert.Equal(CapeShop.CastleConfirmText, CapeShop.ConfirmText(castle: true));
        Assert.Equal(CapeShop.PurchaseConfirmText, CapeShop.ConfirmText(castle: false));
    }

    [Fact]
    public void TheConfirmationFillsBothLinesAndBreaksAtTheBackslash()
    {
        string text = CapeShop.Fill("If not castellan, \n%s\n%s will be used.\\Continue?", "186,000 Clan CONT", "300,000,000 Noah");
        Assert.Equal("If not castellan, \n186,000 Clan CONT\n300,000,000 Noah will be used.\nContinue?", text);
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(-5, "0")]
    [InlineData(300000000, "300,000,000")]
    public void AmountsUseThousandSeparators(int value, string shown) => Assert.Equal(shown, CapeShop.Amount(value));
}
