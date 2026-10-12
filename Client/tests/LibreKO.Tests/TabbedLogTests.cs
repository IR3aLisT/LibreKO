using System.Collections.Generic;
using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class TabbedLogTests
{
    [Fact]
    public void EveryLineReachesAllAndItsOwnTab()
    {
        var log = new TabbedLog(10);
        var dropped = new List<string>();
        Assert.True(log.Add("hp", LogTab.State, dropped));
        Assert.True(log.Add("coins", LogTab.Item, dropped));
        Assert.True(log.Add("miss", LogTab.System, dropped));

        Assert.Equal(new[] { "hp", "coins", "miss" }, log.Lines(LogTab.All));
        Assert.Equal(new[] { "hp" }, log.Lines(LogTab.State));
        Assert.Equal(new[] { "coins" }, log.Lines(LogTab.Item));
        Assert.Equal(new[] { "miss" }, log.Lines(LogTab.System));
    }

    [Fact]
    public void OnlyLinesOfTheShownTabAreDrawn()
    {
        var log = new TabbedLog(10);
        var dropped = new List<string>();
        Assert.True(log.Show(LogTab.Item));
        Assert.False(log.Show(LogTab.Item));
        Assert.False(log.Add("hp", LogTab.State, dropped));
        Assert.True(log.Add("coins", LogTab.Item, dropped));
        Assert.Equal(new[] { "coins" }, log.ShownLines);
    }

    [Fact]
    public void EachTabKeepsItsOwnLimitAndReportsWhatLeftTheView()
    {
        var log = new TabbedLog(2);
        var dropped = new List<string>();
        log.Add("coins", LogTab.Item, dropped);
        log.Add("a", LogTab.System, dropped);
        log.Add("b", LogTab.System, dropped);
        Assert.Equal(new[] { "coins" }, dropped);
        Assert.Equal(new[] { "coins" }, log.Lines(LogTab.Item));

        log.Show(LogTab.System);
        log.Add("c", LogTab.System, dropped);
        Assert.Equal(new[] { "a" }, dropped);
        Assert.Equal(new[] { "b", "c" }, log.ShownLines);
    }
}
