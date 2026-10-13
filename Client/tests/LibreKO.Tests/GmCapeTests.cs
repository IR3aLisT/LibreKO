using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class GmCapeTests
{
    private const int ClanCape = 12;

    [Theory]
    [InlineData(GmCape.Id)]
    [InlineData(ClanCape)]
    [InlineData(GmCape.None)]
    public void AGmInGmModeWearsTheWhiteCapeOverWhateverTheRecordCarries(int recordCape) =>
        Assert.Equal(GmCape.Id, GmCape.Worn(recordCape, gmShown: true));

    [Fact]
    public void AClanlessGmOutOfGmModeDropsTheWhiteCapeTheRecordStillCarries() =>
        Assert.Equal(GmCape.None, GmCape.Worn(GmCape.Id, gmShown: false));

    [Fact]
    public void AGmOutOfGmModeWearsTheClanCape() =>
        Assert.Equal(ClanCape, GmCape.Worn(ClanCape, gmShown: false));
}
