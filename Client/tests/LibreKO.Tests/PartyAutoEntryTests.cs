using System.Collections.Generic;
using LibreKO.Domain;
using LibreKO.Network;
using Xunit;

namespace LibreKO.Tests;

public class PartyAutoEntryTests
{
    private const int Moradon = 21;
    private const int Rogue = 102;

    private static PartyBbsEntry Post(string name, int wanted, int level, int zone = Moradon, int members = 3, byte type = PartyAutoEntry.PartyPost) =>
        new(name, wanted, level, type, "", zone, members, 1);

    [Theory]
    [InlineData(60, 40, true)]
    [InlineData(60, 39, false)]
    [InlineData(60, 83, true)]
    [InlineData(20, 12, true)]
    [InlineData(20, 11, false)]
    [InlineData(5, 1, true)]
    public void TheLevelBandIsEightOrAThirdWhicheverIsWider(int post, int mine, bool fits) =>
        Assert.Equal(fits, PartyAutoEntry.LevelFits(post, mine));

    [Fact]
    public void TheFirstFittingPartyPostInYourZoneIsPicked()
    {
        var entries = new List<PartyBbsEntry>
        {
            Post("Solo", 0, 60, type: 2),
            Post("Elsewhere", 0, 60, zone: 1),
            Post("Full", 0, 60, members: 8),
            Post("Warriors", 1, 60),
            Post("TooLow", 0, 30),
            Post("Rikka", 2, 61),
        };

        Assert.Equal(5, PartyAutoEntry.Pick(entries, Moradon, Rogue, 60));
    }

    [Fact]
    public void OnlyTheFirstTwelveRowsAreLookedAt()
    {
        var entries = new List<PartyBbsEntry>();
        for (int i = 0; i < PartyAutoEntry.ScannedRows; i++) entries.Add(Post("Full" + i, 0, 60, members: 8));
        entries.Add(Post("Late", 0, 60));

        Assert.Equal(-1, PartyAutoEntry.Pick(entries, Moradon, Rogue, 60));
    }
}
