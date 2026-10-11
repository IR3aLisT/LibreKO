using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class NpcTouchTests
{
    private const int ScarecrowModel = 4081;

    [Fact]
    public void AContactIsHalfTheTwoRadiiExceptForRockslides()
    {
        Assert.Equal(1f, NpcTouch.ContactDistance(NpcTouch.SawBladeModel, 0.9f, 1.1f), 3);
        Assert.Equal(2f, NpcTouch.ContactDistance(NpcTouch.RockslideModel, 0.9f, 1.1f), 3);
    }

    [Fact]
    public void ARollingStoneTremblesByTheInverseSquareOfItsDistance()
    {
        var near = NpcTouch.TremorFrom(NpcTouch.RollingStoneModel, 2f, moving: false);
        Assert.Equal(NpcTouch.RollingStonePower / 4f, near.Power, 3);
        Assert.Equal(NpcTouch.TremorCap, near.Cap);
    }

    [Fact]
    public void ARockslideTremblesOnlyWhileItMoves()
    {
        Assert.Equal(0f, NpcTouch.TremorFrom(NpcTouch.RockslideModel, 2f, moving: false).Power);
        var moving = NpcTouch.TremorFrom(NpcTouch.RockslideModel, 2f, moving: true);
        Assert.Equal(NpcTouch.RockslidePower / 4f, moving.Power, 3);
        Assert.Equal(NpcTouch.RockslideCap, moving.Cap);
    }

    [Fact]
    public void OtherNpcsDoNotTremble() =>
        Assert.Equal(0f, NpcTouch.TremorFrom(ScarecrowModel, 1f, moving: true).Power);

    [Fact]
    public void TheAmplitudeIsCappedAndATouchShakesAtTheCap()
    {
        var far = new Tremor(0.2f, NpcTouch.TremorCap);
        Assert.Equal(0.2f, NpcTouch.Amplitude(far, touched: false), 3);
        Assert.Equal(NpcTouch.TremorCap, NpcTouch.Amplitude(new Tremor(5f, NpcTouch.TremorCap), touched: false));
        Assert.Equal(NpcTouch.TremorCap, NpcTouch.Amplitude(far, touched: true));
    }

    [Fact]
    public void OwnNationRockslidesDoNotHurt()
    {
        Assert.False(NpcTouch.HurtsOnContact(NpcTouch.RockslideModel, sameNation: true));
        Assert.True(NpcTouch.HurtsOnContact(NpcTouch.RockslideModel, sameNation: false));
        Assert.True(NpcTouch.HurtsOnContact(NpcTouch.SawBladeModel, sameNation: true));
    }

    [Fact]
    public void ChaosCubesArePickedUpOnlyInTheirZones()
    {
        Assert.True(NpcTouch.PicksUpCube(32008, 85));
        Assert.False(NpcTouch.PicksUpCube(32008, 21));
        Assert.False(NpcTouch.PicksUpCube(NpcTouch.SawBladeModel, 85));
    }

    [Fact]
    public void StatuesAndMonumentsAreNeverTouched() =>
        Assert.True(NpcTouch.Ignored(30090));
}
