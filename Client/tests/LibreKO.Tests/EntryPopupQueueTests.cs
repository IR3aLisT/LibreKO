using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class EntryPopupQueueTests
{
    [Fact]
    public void NothingOpensUntilTheLoadingScreenIsGone()
    {
        var queue = new EntryPopupQueue();
        queue.Add(EntryPopup.TimedItems);
        Assert.Null(queue.TakeNext());
        queue.Release();
        Assert.Equal(EntryPopup.TimedItems, queue.TakeNext());
    }

    [Fact]
    public void PopupsOpenOneAtATimeInTheirFixedOrder()
    {
        var queue = new EntryPopupQueue();
        queue.Add(EntryPopup.ZoneCaption);
        queue.Add(EntryPopup.ZoneGuide);
        queue.Add(EntryPopup.TimedItems);
        queue.Release();

        Assert.Equal(EntryPopup.TimedItems, queue.TakeNext());
        Assert.Null(queue.TakeNext());
        queue.Closed();
        Assert.Equal(EntryPopup.ZoneGuide, queue.TakeNext());
        queue.Closed();
        Assert.Equal(EntryPopup.ZoneCaption, queue.TakeNext());
        queue.Closed();
        Assert.Null(queue.TakeNext());
    }

    [Fact]
    public void AddingTheSamePopupTwiceShowsItOnce()
    {
        var queue = new EntryPopupQueue();
        queue.Add(EntryPopup.TimedItems);
        queue.Add(EntryPopup.TimedItems);
        Assert.Equal(1, queue.Count);
    }
}
