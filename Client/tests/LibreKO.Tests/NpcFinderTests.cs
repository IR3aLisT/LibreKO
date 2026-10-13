using System.Linq;
using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class NpcFinderTests
{
    private const string Json =
        "{\"spots\":[[3,20000,377,572,13003,0,\"[Potion Merchant]Clepio\"],"
        + "[3,70400,766,597,13006,0,\"[Armor Merchant]Hesta\"],"
        + "[8,30000,1200,300,22000,1,\"[InnHost]Karus\"],"
        + "[8,30000,1300,300,12000,2,\"[InnHost]Elmo\"]]}";

    private const int Moradon = 21;
    private const int KarusEslant = 11;
    private const int Karus = 1;
    private const int ElMorad = 2;
    private const int PotionShop = 2;
    private const int Inn = 3;
    private const int Repair = 4;
    private const int ArmorShop = 7;

    [Fact]
    public void KindCodesPackUpToThreeKinds()
    {
        var spot = new NpcSpot(3, 70400, 0, 0, 0, 0, "");
        Assert.True(spot.Is(ArmorShop));
        Assert.True(spot.Is(Repair));
        Assert.False(spot.Is(PotionShop));
        Assert.True(new NpcSpot(3, 110000, 0, 0, 0, 0, "").Is(11));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(5, 1)]
    [InlineData(2, 2)]
    [InlineData(11, 8)]
    [InlineData(14, 8)]
    [InlineData(12, 9)]
    [InlineData(21, 3)]
    [InlineData(25, 3)]
    [InlineData(30, 4)]
    [InlineData(66, 14)]
    [InlineData(71, 11)]
    [InlineData(72, 10)]
    [InlineData(48, 20)]
    public void ZonesMapToTheMinimapNumbers(int zone, int map)
    {
        Assert.Equal(map, NpcFinder.MapOf(zone));
    }

    [Fact]
    public void SpotsFollowTheZoneTheKindAndTheNation()
    {
        var all = NpcFinder.Parse(Json);
        Assert.Equal(4, all.Count);
        Assert.Equal(new[] { 13003 }, NpcFinder.Spots(all, Moradon, Karus, PotionShop).Select(s => s.NpcId));
        Assert.Equal(new[] { 22000 }, NpcFinder.Spots(all, KarusEslant, Karus, Inn).Select(s => s.NpcId));
        Assert.Equal(new[] { 12000 }, NpcFinder.Spots(all, KarusEslant, ElMorad, Inn).Select(s => s.NpcId));
        Assert.Empty(NpcFinder.Spots(all, Moradon, Karus, NpcFinder.NoKind));
    }

    [Fact]
    public void OnlyKindsWithASpotInTheZoneArePresent()
    {
        var present = NpcFinder.KindsPresent(NpcFinder.Parse(Json), Moradon, Karus);
        Assert.Equal(NpcFinder.KindCount, present.Length);
        Assert.True(present[PotionShop - 1]);
        Assert.True(present[ArmorShop - 1]);
        Assert.True(present[Repair - 1]);
        Assert.False(present[Inn - 1]);
    }
}
