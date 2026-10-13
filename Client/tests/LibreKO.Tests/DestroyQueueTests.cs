using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class DestroyQueueTests
{
    [Fact]
    public void ASlotIsQueuedOnce()
    {
        var queue = new DestroyQueue();

        Assert.True(queue.Add(14, 389010000));
        Assert.False(queue.Add(14, 389010000));
        Assert.Equal(1, queue.Count);
    }

    [Fact]
    public void ItemsGoOneAtATimeAndTheRunEndsWithTheLast()
    {
        var queue = new DestroyQueue();
        queue.Add(14, 1);
        queue.Add(15, 2);

        Assert.Equal(14, queue.Start()!.Value.Slot);
        Assert.Equal(15, queue.Advance(ok: true)!.Value.Slot);
        Assert.Null(queue.Advance(ok: true));
        Assert.False(queue.Running);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void ARefusalStopsTheRun()
    {
        var queue = new DestroyQueue();
        queue.Add(14, 1);
        queue.Add(15, 2);
        queue.Start();

        Assert.Null(queue.Advance(ok: false));
        Assert.False(queue.Running);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void NothingChangesWhileARunIsGoing()
    {
        var queue = new DestroyQueue();
        queue.Add(14, 1);
        queue.Start();

        Assert.False(queue.Add(15, 2));
        queue.Remove(14);
        queue.DropStale((_, _) => false);
        Assert.Equal(1, queue.Count);
    }

    [Fact]
    public void ItemsThatMovedAwayLeaveTheCard()
    {
        var queue = new DestroyQueue();
        queue.Add(14, 1);
        queue.Add(15, 2);

        queue.DropStale((slot, _) => slot == 15);

        Assert.Single(queue.Entries);
        Assert.Equal(15, queue.Entries[0].Slot);
    }
}
