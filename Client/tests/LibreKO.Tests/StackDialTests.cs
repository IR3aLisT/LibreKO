using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class StackDialTests
{
    private const double Quick = 0.05;
    private const double Pause = StackDial.StreakPauseSeconds + 0.1;

    private static int[] Spin(StackDial dial, int direction, int notches, ref double now, double gap = Quick)
    {
        var seen = new int[notches];
        for (int i = 0; i < notches; i++)
        {
            now += gap;
            seen[i] = dial.Turn(direction, now);
        }
        return seen;
    }

    [Fact]
    public void ADragStartsAtTheWholeStack()
    {
        var dial = new StackDial(48);

        Assert.Equal(48, dial.Amount);
        Assert.True(dial.Whole);
    }

    [Fact]
    public void UpFromTheWholeStackStartsAtOneAndDownFromOneWrapsToTheWholeStack()
    {
        var dial = new StackDial(48);

        Assert.Equal(1, dial.Turn(StackDial.Up, 1));
        Assert.Equal(2, dial.Turn(StackDial.Up, 2));
        Assert.Equal(1, dial.Turn(StackDial.Down, 3));
        Assert.Equal(48, dial.Turn(StackDial.Down, 4));
        Assert.True(dial.Whole);
    }

    [Fact]
    public void QuickNotchesUpSpeedUpOnRoundSteps()
    {
        var dial = new StackDial(800);
        double now = 0;

        Assert.Equal(
            new[] { 1, 2, 3, 4, 5, 10, 15, 20, 30, 40, 50, 60, 100, 150, 200, 250, 300, 400, 500, 600 },
            Spin(dial, StackDial.Up, 20, ref now));
    }

    [Fact]
    public void QuickNotchesDownFromTheWholeStackReachFiveHundredFiftyOfEightHundred()
    {
        var dial = new StackDial(800);
        double now = 0;

        Assert.Equal(
            new[] { 799, 798, 797, 796, 795, 790, 785, 780, 770, 760, 750, 740, 700, 650, 600, 550 },
            Spin(dial, StackDial.Down, 16, ref now));
    }

    [Fact]
    public void APauseResetsTheStep()
    {
        var dial = new StackDial(800);
        double now = 0;
        Spin(dial, StackDial.Up, 13, ref now);
        Assert.Equal(100, dial.Amount);

        Assert.Equal(new[] { 101 }, Spin(dial, StackDial.Up, 1, ref now, Pause));
    }

    [Fact]
    public void ADirectionChangeResetsTheStep()
    {
        var dial = new StackDial(800);
        double now = 0;
        Spin(dial, StackDial.Up, 13, ref now);

        Assert.Equal(new[] { 99, 98 }, Spin(dial, StackDial.Down, 2, ref now));
    }

    [Fact]
    public void ABigStepStopsAtTheStackBeforeWrapping()
    {
        var dial = new StackDial(120);
        double now = 0;

        Assert.Equal(
            new[] { 119, 118, 117, 116, 115, 110, 105, 100, 90, 80, 70, 60, 50, 1, 120 },
            Spin(dial, StackDial.Down, 15, ref now));
    }

    [Fact]
    public void ABigStepUpStopsAtTheWholeStack()
    {
        var dial = new StackDial(48);
        double now = 0;

        Assert.Equal(
            new[] { 1, 2, 3, 4, 5, 10, 15, 20, 30, 40, 48, 1 },
            Spin(dial, StackDial.Up, 12, ref now));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(4, 1)]
    [InlineData(5, 5)]
    [InlineData(8, 5)]
    [InlineData(9, 10)]
    [InlineData(13, 50)]
    [InlineData(17, 100)]
    [InlineData(400, 100)]
    public void TheStepGrowsEveryFewNotches(int streak, int step) =>
        Assert.Equal(step, StackDial.StepFor(streak));

    [Fact]
    public void ASingleItemIgnoresTheWheel()
    {
        var dial = new StackDial(1);

        Assert.Equal(1, dial.Turn(StackDial.Up, 0));
        Assert.Equal(1, dial.Turn(StackDial.Down, 1));
    }

    [Theory]
    [InlineData(1, 2, true)]
    [InlineData(1, 1, false)]
    [InlineData(0, 20, false)]
    public void OnlyACountableStackPicksAnAmount(int countable, int count, bool applies) =>
        Assert.Equal(applies, StackDial.Applies(countable, count));
}
