using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class CommanderOrdersTests
{
    [Fact]
    public void TheBarHoldsMarkingThenTheFiveOrders()
    {
        Assert.Equal(6, CommanderOrders.Bar.Length);
        Assert.Equal(CommanderOrders.MarkingSkill, CommanderOrders.Bar[0].Skill);
        Assert.Equal(CommanderOrders.None, CommanderOrders.Bar[0].Order);
        Assert.Equal(492067, CommanderOrders.SkillOf(CommanderOrders.Back));
        Assert.Equal(492072, CommanderOrders.SkillOf(CommanderOrders.Left));
    }

    [Theory]
    [InlineData(CommanderOrders.Forward, false, 340001)]
    [InlineData(CommanderOrders.Back, true, 340002)]
    [InlineData(CommanderOrders.BackHome, true, 340003)]
    [InlineData(CommanderOrders.Right, false, 340001)]
    [InlineData(CommanderOrders.Left, false, 340001)]
    public void EachOrderHasItsFlashAndSound(byte order, bool red, int sound)
    {
        Assert.Equal(red, CommanderOrders.RedFlash(order));
        Assert.Equal(sound, CommanderOrders.Sound(order));
    }

    [Theory]
    [InlineData(CommanderOrders.Forward, 0, false)]
    [InlineData(CommanderOrders.Back, 1, true)]
    [InlineData(CommanderOrders.BackHome, 2, true)]
    [InlineData(CommanderOrders.Right, 3, false)]
    [InlineData(CommanderOrders.Left, 4, false)]
    public void EachOrderHasItsBadgeGlyphAndRing(byte order, int glyph, bool redRing)
    {
        Assert.Equal(glyph, CommanderOrders.Glyph(order));
        Assert.Equal(redRing, CommanderOrders.RedRing(glyph));
    }

    [Fact]
    public void OrderFourShowsNothing() => Assert.Equal(CommanderOrders.NoGlyph, CommanderOrders.Glyph(4));

    [Fact]
    public void ThePulseRunsAtLeastFiveSecondsAndStopsWhenFaded()
    {
        var pulse = new CommanderPulse();
        pulse.Start();
        Assert.Equal(CommanderPulse.StartAlpha, pulse.Alpha);
        int frames = 0;
        while (pulse.Running && frames < 10_000)
        {
            pulse.Frame(1.0 / 60.0);
            frames++;
        }
        Assert.False(pulse.Running);
        Assert.True(pulse.Elapsed > CommanderPulse.MinSeconds);
        Assert.Equal(0, pulse.Alpha);
    }

    [Fact]
    public void TheRingMovesEveryEighthFrameAndTheSweepPlaysOnce()
    {
        var pulse = new CommanderPulse();
        pulse.Start();
        for (int i = 0; i < CommanderPulse.FramesPerImage - 1; i++) pulse.Frame(1.0 / 60.0);
        Assert.Equal(0, pulse.RingImage);
        pulse.Frame(1.0 / 60.0);
        Assert.Equal(1, pulse.RingImage);
        for (int i = 0; i < CommanderPulse.FramesPerImage * CommanderPulse.SweepImages; i++) pulse.Frame(1.0 / 60.0);
        Assert.False(pulse.Sweeping);
    }

    [Fact]
    public void TheChargeWordsUseTheForwardSlotAndTheRetreatWordsTheBackSlot()
    {
        static string Fallback(int id, string text) => text;
        Assert.Equal(1, CommanderOrders.SlashSlot("charge", Fallback));
        Assert.Equal(1, CommanderOrders.SlashSlot("GO", Fallback));
        Assert.Equal(2, CommanderOrders.SlashSlot("fallback", Fallback));
        Assert.Equal(CommanderOrders.NoSlot, CommanderOrders.SlashSlot("dance", Fallback));
    }

    [Fact]
    public void MarkingTheMarkedTargetAgainClearsIt()
    {
        Assert.True(CommanderOrders.ClearsMark(7301, 7301));
        Assert.False(CommanderOrders.ClearsMark(7301, 7302));
        Assert.False(CommanderOrders.ClearsMark(-1, 7302));
    }

    [Fact]
    public void EachOrderDrawsTheCellThe2619BadgeLayoutNames()
    {
        Assert.Equal(0, CommanderOrders.SheetCell(CommanderOrders.Forward));
        Assert.Equal(1, CommanderOrders.SheetCell(CommanderOrders.Left));
        Assert.Equal(2, CommanderOrders.SheetCell(CommanderOrders.Right));
        Assert.Equal(3, CommanderOrders.SheetCell(CommanderOrders.BackHome));
        Assert.Equal(4, CommanderOrders.SheetCell(CommanderOrders.Back));
        Assert.Equal(CommanderOrders.NoGlyph, CommanderOrders.SheetCell(CommanderOrders.None));
    }
}
