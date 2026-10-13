using LibreKO.Domain;
using LibreKO.Network;
using Xunit;

namespace LibreKO.Tests;

public class NameMarkTests
{
    private const int Moradon = 21;
    private const int AccreditedFlag = 3;

    [Fact]
    public void AnAccreditedClansChiefIsUnderlined() =>
        Assert.Equal(NameMarkKind.Underline, NameMark.For(ClanRanks.Chief, AccreditedFlag, Moradon, sameNation: false));

    [Fact]
    public void ATrainingOrPromotedClansChiefIsNot() =>
        Assert.Equal(NameMarkKind.None, NameMark.For(ClanRanks.Chief, NameMark.PromotedClanFlag, Moradon, sameNation: true));

    [Fact]
    public void TheCommandCaptainGetsABox() =>
        Assert.Equal(NameMarkKind.Box, NameMark.For(ClanRanks.CommandCaptain, 0, Moradon, sameNation: true));

    [Theory]
    [InlineData(29)]
    [InlineData(45)]
    [InlineData(85)]
    [InlineData(89)]
    public void SomeZonesShowNoMarks(int zone) =>
        Assert.Equal(NameMarkKind.None, NameMark.For(ClanRanks.Chief, AccreditedFlag, zone, sameNation: true));

    [Fact]
    public void InZones31And46OnlyOwnNationIsMarked()
    {
        Assert.Equal(NameMarkKind.None, NameMark.For(ClanRanks.Chief, AccreditedFlag, 31, sameNation: false));
        Assert.Equal(NameMarkKind.Underline, NameMark.For(ClanRanks.Chief, AccreditedFlag, 46, sameNation: true));
    }

    [Fact]
    public void OtherMembersAreNotMarked() =>
        Assert.Equal(NameMarkKind.None, NameMark.For(ClanRanks.ViceChief, AccreditedFlag, Moradon, sameNation: true));

    [Theory]
    [InlineData(29, true)]
    [InlineData(45, true)]
    [InlineData(85, true)]
    [InlineData(89, true)]
    [InlineData(Moradon, false)]
    public void TheClanIsLeftOffThePlateWhereMarksAre(int zone, bool hidden) =>
        Assert.Equal(hidden, NameMark.HidesClan(zone));
}
