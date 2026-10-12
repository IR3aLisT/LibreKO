using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class MaestroPotionsTests
{
    [Theory]
    [InlineData(MaestroPotions.Health, 100_000, true)]
    [InlineData(MaestroPotions.Mana, MaestroPotions.MinimumCoins, true)]
    [InlineData(MaestroPotions.Health, MaestroPotions.MinimumCoins - 1, false)]
    [InlineData(MaestroPotions.Mana, 99_999, false)]
    [InlineData(MaestroPotions.Health, 0, false)]
    [InlineData(389010000, 0, true)]
    public void AMaestroPotionNeedsOneHundredThousandNoah(int itemId, int gold, bool usable) =>
        Assert.Equal(usable, MaestroPotions.CanUse(itemId, gold));

    private const int WaterOfFavors = 389014000;
    private const int WaterOfLove = 389012000;
    private const int Arrow = 391010000;
    private const int MaestroHeal = 720;
    private const int WaterOfFavorsHeal = 720;
    private const int WaterOfLoveHeal = 360;

    private static readonly Dictionary<int, int> Heals = new()
    {
        [MaestroPotions.Health] = MaestroHeal,
        [WaterOfFavors] = WaterOfFavorsHeal,
        [WaterOfLove] = WaterOfLoveHeal,
    };

    private static int Pick(int gold, params int[] bag) =>
        MaestroPotions.PickStrongest(bag, id => Heals.GetValueOrDefault(id), gold);

    [Fact]
    public void AnOrdinaryPotionIsChosenOverAMaestroThatRestoresAsMuch() =>
        Assert.Equal(WaterOfFavors, Pick(MaestroPotions.MinimumCoins, MaestroPotions.Health, WaterOfFavors));

    [Fact]
    public void AWeakerOrdinaryPotionIsStillChosenOverAMaestro() =>
        Assert.Equal(WaterOfLove, Pick(MaestroPotions.MinimumCoins, MaestroPotions.Health, WaterOfLove));

    [Fact]
    public void TheStrongestOrdinaryPotionIsChosen() =>
        Assert.Equal(WaterOfFavors, Pick(0, WaterOfLove, 0, WaterOfFavors, Arrow));

    [Fact]
    public void AMaestroIsChosenWhenNoOrdinaryPotionRestores() =>
        Assert.Equal(MaestroPotions.Health, Pick(MaestroPotions.MinimumCoins, Arrow, MaestroPotions.Health));

    [Fact]
    public void AMaestroIsSkippedWithoutTheCoinsItCharges() =>
        Assert.Equal(0, Pick(MaestroPotions.MinimumCoins - 1, Arrow, MaestroPotions.Health));
}
