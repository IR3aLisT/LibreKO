using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class CastBreakTests
{
    private const int FlyingArrow = 1201;

    private static short[] Fail(short code)
    {
        var data = new short[7];
        data[MagicFail.ResultSlot] = code;
        return data;
    }

    private static CastBreakSkill Arrow() =>
        new(Ranged: true, FlyingFxId: FlyingArrow, Type1: MagicType.Ranged, Type2: 0, Attribute: 0, EndDamage: 0, TimeDamage: 0);

    private static CastBreakSkill Lightning(int endDamage = -100, int timeDamage = 0, int flying = FlyingArrow) =>
        new(Ranged: false, FlyingFxId: flying, Type1: MagicType.DotHeal, Type2: 0, Attribute: CastBreak.LightningAttribute,
            EndDamage: endDamage, TimeDamage: timeDamage);

    [Fact]
    public void AnArrowKilledInFlightBreaksTheCast()
    {
        Assert.True(CastBreak.InFlightFailBreaks(Arrow(), Fail(MagicFail.ProjectileKilled)));
        Assert.False(CastBreak.InFlightFailBreaks(Arrow(), Fail(MagicFail.ProjectileKilledByType)));
        Assert.False(CastBreak.InFlightFailBreaks(Arrow(), Fail(MagicFail.Casting)));
    }

    [Fact]
    public void ALightningBoltKilledInFlightBreaksTheCastForEitherCode()
    {
        Assert.True(CastBreak.InFlightFailBreaks(Lightning(), Fail(MagicFail.ProjectileKilled)));
        Assert.True(CastBreak.InFlightFailBreaks(Lightning(), Fail(MagicFail.ProjectileKilledByType)));
    }

    [Fact]
    public void NoFlyingEffectMeansNoInFlightBreak() =>
        Assert.False(CastBreak.InFlightFailBreaks(Lightning(flying: 0), Fail(MagicFail.ProjectileKilled)));

    [Fact]
    public void OnlyHarmfulLightningBreaksACastOnLanding()
    {
        Assert.True(CastBreak.LightningHurts(Lightning(endDamage: -50)));
        Assert.True(CastBreak.LightningHurts(Lightning(endDamage: 0, timeDamage: -10)));
        Assert.False(CastBreak.LightningHurts(Lightning(endDamage: 0, timeDamage: 10)));
        Assert.False(CastBreak.LightningHurts(Lightning(endDamage: 30)));
        Assert.False(CastBreak.LightningHurts(Lightning() with { Attribute = FireAttribute }));
    }

    private const int FireAttribute = 1;
}
