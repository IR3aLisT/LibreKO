using System.Collections.Generic;
using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class AreaNamesTests
{
    [Theory]
    [InlineData(1, 10105, 12005)]
    [InlineData(2, 10214, 12114)]
    [InlineData(21, 12102, 12202)]
    [InlineData(23, 12113, 12213)]
    [InlineData(31, 20002, 12302)]
    [InlineData(48, 10105, 0)]
    public void RegionIdsResolveToTheZonesTextRange(int zone, int region, int text) =>
        Assert.Equal(text, AreaNames.NameTextId(zone, region));

    [Fact]
    public void SpecialRegionsUseTheirOwnNotices()
    {
        Assert.Equal(new AreaNotice(AreaNoticeKind.WarningBanner, 11618, true), AreaNames.NoticeFor(21, 42));
        Assert.Equal(new AreaNotice(AreaNoticeKind.Banner, 7642, true), AreaNames.NoticeFor(30, 30));
        Assert.Equal(AreaNoticeKind.Log, AreaNames.NoticeFor(30, 35).Kind);
        Assert.Equal(new AreaNotice(AreaNoticeKind.Banner, 12202, false), AreaNames.NoticeFor(21, 12102));
        Assert.Equal(new AreaNotice(AreaNoticeKind.Hint, 18107, true), AreaNames.NoticeFor(21, 68));
        Assert.Equal(new AreaNotice(AreaNoticeKind.Hint, 18114, true), AreaNames.NoticeFor(21, 69));
        Assert.Equal(AreaNoticeKind.None, AreaNames.NoticeFor(21, 71).Kind);
    }

    [Fact]
    public void OnlyARegionNotAlreadyAroundYouAnnouncesItself()
    {
        var before = new HashSet<int>();

        Assert.Equal(12110, AreaNames.Entered(new[] { 12110 }, before));
        Assert.Equal(AreaNames.Unchanged, AreaNames.Entered(new[] { 12110 }, before));
        Assert.Equal(12111, AreaNames.Entered(new[] { 12110, 12111 }, before));
        Assert.Equal(AreaNames.Left, AreaNames.Entered(new int[0], before));
        Assert.Equal(12110, AreaNames.Entered(new[] { 12110 }, before));
    }
}
