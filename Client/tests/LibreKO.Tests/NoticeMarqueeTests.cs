using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class NoticeMarqueeTests
{
    [Fact]
    public void APassRunsFromTheRightEdgeUntilTheTextHasLeft()
    {
        Assert.Equal(10f, NoticeMarquee.PassSeconds(520f, 230f), 3);
        Assert.Equal(520f, NoticeMarquee.OffsetAt(520f, 0f), 3);
        Assert.Equal(-230f, NoticeMarquee.OffsetAt(520f, 10f), 3);
    }

    [Fact]
    public void ALineScrollsTwice()
    {
        Assert.Equal(20f, NoticeMarquee.ShowSeconds(520f, 230f), 3);
    }
}
