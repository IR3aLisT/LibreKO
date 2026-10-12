using System.Collections.Generic;
using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class PartyOrderTests
{
    [Fact]
    public void ANewPartyKeepsTheServerOrder()
    {
        var order = new PartyOrder();
        Assert.Equal(new[] { 1, 2, 3 }, order.Arrange(new[] { 1, 2, 3 }, id => id));
    }

    [Fact]
    public void MovingDownSwapsWithTheNextRowAndWrapsAtTheEnd()
    {
        var order = new PartyOrder();
        order.Arrange(new[] { 1, 2, 3 }, id => id);

        order.Move(1, PartyOrder.Down);
        Assert.Equal(new[] { 2, 1, 3 }, order.Arrange(new[] { 1, 2, 3 }, id => id));

        order.Move(3, PartyOrder.Down);
        Assert.Equal(new[] { 3, 1, 2 }, order.Arrange(new[] { 1, 2, 3 }, id => id));
    }

    [Fact]
    public void MovingUpFromTheTopSwapsWithTheLastRow()
    {
        var order = new PartyOrder();
        order.Arrange(new[] { 1, 2, 3 }, id => id);

        order.Move(1, PartyOrder.Up);

        Assert.Equal(new[] { 3, 2, 1 }, order.Arrange(new[] { 1, 2, 3 }, id => id));
    }

    [Fact]
    public void JoinersGoLastAndLeaversDropOut()
    {
        var order = new PartyOrder();
        order.Arrange(new[] { 1, 2, 3 }, id => id);
        order.Move(1, PartyOrder.Down);

        Assert.Equal(new[] { 1, 3, 4 }, order.Arrange(new[] { 1, 3, 4 }, id => id));
        Assert.Empty(order.Arrange(new List<int>(), id => id));
    }

    [Fact]
    public void ALoneMemberCannotMove()
    {
        var order = new PartyOrder();
        order.Arrange(new[] { 7 }, id => id);
        Assert.False(order.Move(7, PartyOrder.Down));
    }
}
