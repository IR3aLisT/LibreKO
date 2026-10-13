using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class PartyInviteRefusalTests
{
    [Theory]
    [InlineData(PartyInviteRefusal.Declined, 3409)]
    [InlineData(PartyInviteRefusal.LevelGap, 3408)]
    [InlineData(PartyInviteRefusal.OtherNation, 3407)]
    [InlineData(PartyInviteRefusal.OtherZone, 3417)]
    [InlineData(PartyInviteRefusal.NotOnline, 3418)]
    [InlineData(PartyInviteRefusal.CannotParty, 3419)]
    [InlineData(PartyInviteRefusal.RegionBarred, 3430)]
    [InlineData(PartyInviteRefusal.MissingItem, 37309)]
    [InlineData(-42, 3406)]
    public void EachCodeShowsTheRetailText(int code, int textId)
    {
        Assert.Equal(textId, PartyInviteRefusal.TextId(code));
    }

    [Fact]
    public void ZoneAndNationAreNoLongerConfused()
    {
        Assert.Contains("other nation", PartyInviteRefusal.Fallback(PartyInviteRefusal.OtherNation));
        Assert.Contains("different zone", PartyInviteRefusal.Fallback(PartyInviteRefusal.OtherZone));
    }
}
