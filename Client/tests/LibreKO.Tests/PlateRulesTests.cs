using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class PlateRulesTests
{
    private const int Moradon = 21;
    private const int BiFrost = 31;
    private const int ClanA = 10;
    private const int ClanB = 11;

    [Theory]
    [InlineData(Moradon, true)]
    [InlineData(57, false)]
    [InlineData(76, false)]
    [InlineData(85, false)]
    public void FriendsAreGreenOutsideTheEventZones(int zone, bool green) =>
        Assert.Equal(green, PlateRules.FriendColour(zone));

    [Fact]
    public void ClanPlayersOfTheOtherNationGoByTheirHeroTitleInBiFrost()
    {
        Assert.Equal(PlateRules.ElMoradHeroText,
            PlateRules.AnonymousNameText(BiFrost, Nations.Karus, ClanA, Nations.ElMorad, ClanB));
        Assert.Equal(0, PlateRules.AnonymousNameText(BiFrost, Nations.Karus, ClanA, Nations.Karus, ClanB));
        Assert.Equal(0, PlateRules.AnonymousNameText(BiFrost, Nations.Karus, 0, Nations.ElMorad, ClanB));
        Assert.Equal(0, PlateRules.AnonymousNameText(Moradon, Nations.Karus, ClanA, Nations.ElMorad, ClanB));
    }

    [Fact]
    public void InDelosBasementEveryoneOutsideYourClanIsTheHeroFromDelos()
    {
        Assert.Equal(PlateRules.DelosHeroText,
            PlateRules.AnonymousNameText(PlateRules.DelosBasement, Nations.Karus, ClanA, Nations.Karus, ClanB));
        Assert.Equal(0, PlateRules.AnonymousNameText(PlateRules.DelosBasement, Nations.Karus, ClanA, Nations.ElMorad, ClanA));
    }
}
