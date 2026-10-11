using FluentAssertions;
using LibreKO.Game.World;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LibreKO.Game.Tests;

public class CastleSiegeTests : GameTestBase
{
    private const int Owner = 40;
    private const int Ally = 41;
    private const int Other = 50;
    private const byte Saturday = 7;
    private const byte Sunday = 1;

    [Theory]
    [InlineData(Owner, 0, Owner, SiegeSide.Owner)]
    [InlineData(Ally, Owner, Owner, SiegeSide.Owner)]
    [InlineData(Other, 0, Owner, SiegeSide.Challenger)]
    [InlineData(Owner, 0, 0, SiegeSide.Challenger)]
    public void TheOwnerSideIsTheOwnerClanAndItsAlliance(int clan, int alliance, int owner, SiegeSide side) =>
        CastleSiegeRules.SideOf(clan, alliance, owner).Should().Be(side);

    [Theory]
    [InlineData(SiegeSide.Owner, SiegeSide.Challenger, true)]
    [InlineData(SiegeSide.Challenger, SiegeSide.Owner, true)]
    [InlineData(SiegeSide.Challenger, SiegeSide.Challenger, false)]
    [InlineData(SiegeSide.Owner, SiegeSide.Owner, false)]
    [InlineData(SiegeSide.Owner, SiegeSide.None, false)]
    public void OnlyTheOwnerSideAndTheChallengersFight(SiegeSide attacker, SiegeSide target, bool hostile) =>
        CastleSiegeRules.Hostile(attacker, target).Should().Be(hostile);

    [Fact]
    public void WithNobodyHoldingTheCastleEveryoneIsAChallengerAndNobodyFights()
    {
        var a = CastleSiegeRules.SideOf(Owner, 0, 0);
        var b = CastleSiegeRules.SideOf(Other, 0, 0);
        CastleSiegeRules.Hostile(a, b).Should().BeFalse();
    }

    [Fact]
    public void TheCountdownStartsTenMinutesBeforeTheScheduledWar()
    {
        var saturdayEvening = new DateTime(2026, 10, 10, 20, 50, 0);
        CastleSiegeRules.IsScheduledStart(Saturday, 21, 0, saturdayEvening).Should().BeTrue();
        CastleSiegeRules.IsScheduledStart(Saturday, 21, 0, saturdayEvening.AddMinutes(1)).Should().BeFalse();
        CastleSiegeRules.IsScheduledStart(Sunday, 21, 0, saturdayEvening).Should().BeFalse();
    }

    [Fact]
    public void AWarJustAfterMidnightStartsItsCountdownTheDayBefore()
    {
        var saturdayNight = new DateTime(2026, 10, 10, 23, 55, 0);
        CastleSiegeRules.IsScheduledStart(Sunday, 0, 5, saturdayNight).Should().BeTrue();
    }

    [Fact]
    public void AnUnsetWeekdayNeverStartsAWar() =>
        CastleSiegeRules.IsScheduledStart(0, 0, 10, new DateTime(2026, 10, 11, 0, 0, 0)).Should().BeFalse();

    [Fact]
    public async Task TheCountdownOpensTheWarAndTheWarClockClosesIt()
    {
        using var provider = CreateProvider(_ => { });
        var siege = provider.GetRequiredService<CastleSiegeService>();
        var battle = provider.GetRequiredService<SessionManager>().Battle;
        var now = new DateTime(2026, 10, 10, 12, 0, 0);

        await siege.StartCountdownAsync();
        siege.Phase.Should().Be(CastleSiegePhase.Countdown);
        for (int i = 0; i < CastleSiegeRules.CountdownMinutes * 60; i++)
            await siege.TickAsync(now);
        siege.Phase.Should().Be(CastleSiegePhase.War);
        battle.CastleSiegeOpen.Should().BeTrue();

        for (int i = 0; i < CastleSiegeRules.WarSeconds; i++)
            await siege.TickAsync(now);
        siege.Phase.Should().Be(CastleSiegePhase.None);
        battle.CastleSiegeOpen.Should().BeFalse();
    }

    [Fact]
    public void ACaptureHoldsForSixteenMinutesOrWhatIsLeftOfTheWar()
    {
        CastleSiegeRules.HoldAfterCapture(3000).Should().Be(CastleSiegeRules.HoldSeconds);
        CastleSiegeRules.HoldAfterCapture(300).Should().Be(300);
    }
}
