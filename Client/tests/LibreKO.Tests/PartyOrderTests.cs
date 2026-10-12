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

    [Fact]
    public void ADroppedRowTakesTheSlotAndTheOthersCloseUp()
    {
        var order = new PartyOrder();
        order.Arrange(new[] { 1, 2, 3, 4 }, id => id);

        Assert.True(order.MoveTo(4, 1));
        Assert.Equal(new[] { 1, 4, 2, 3 }, order.Arrange(new[] { 1, 2, 3, 4 }, id => id));

        Assert.True(order.MoveTo(1, 3));
        Assert.Equal(new[] { 4, 2, 3, 1 }, order.Arrange(new[] { 1, 2, 3, 4 }, id => id));
    }

    [Fact]
    public void ADropPastTheEndsLandsOnTheEnds()
    {
        var order = new PartyOrder();
        order.Arrange(new[] { 1, 2, 3 }, id => id);

        Assert.True(order.MoveTo(2, 9));
        Assert.Equal(new[] { 1, 3, 2 }, order.Arrange(new[] { 1, 2, 3 }, id => id));
        Assert.True(order.MoveTo(2, -4));
        Assert.Equal(new[] { 2, 1, 3 }, order.Arrange(new[] { 1, 2, 3 }, id => id));
    }

    [Fact]
    public void DroppingInPlaceOrAStrangerChangesNothing()
    {
        var order = new PartyOrder();
        order.Arrange(new[] { 1, 2, 3 }, id => id);

        Assert.False(order.MoveTo(2, 1));
        Assert.False(order.MoveTo(9, 0));
        Assert.Equal(new[] { 1, 2, 3 }, order.Arrange(new[] { 1, 2, 3 }, id => id));
    }

    [Theory]
    [InlineData(0f, 0)]
    [InlineData(29f, 0)]
    [InlineData(31f, 1)]
    [InlineData(95f, 2)]
    [InlineData(400f, 3)]
    public void ARowDropsAfterEveryRowWhoseCentreItsTopHasPassed(float top, int index) =>
        Assert.Equal(index, PartyOrder.DropIndex(new[] { 30f, 94f, 158f }, top));
}
