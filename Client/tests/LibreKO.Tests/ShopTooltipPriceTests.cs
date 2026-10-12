using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class ShopTooltipPriceTests
{
    [Fact]
    public void ThePurchasePriceCarriesTheZoneTariff() =>
        Assert.Equal(1100, ShopTooltipPrice.Purchase(1000, ItemData.SaleTypeLow, 10, loyaltyShop: false));

    [Fact]
    public void AFullSaleItemPaysNoTariff() =>
        Assert.Equal(1000, ShopTooltipPrice.Purchase(1000, ItemData.SaleTypeFull, 10, loyaltyShop: false));

    [Fact]
    public void TheNationalPointShopPaysNoTariff() =>
        Assert.Equal(1000, ShopTooltipPrice.Purchase(1000, ItemData.SaleTypeLow, 10, loyaltyShop: true));
}
