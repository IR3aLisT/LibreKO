using FluentAssertions;
using LibreKO.Common.Enums;
using LibreKO.Game.World;

namespace LibreKO.Game.Tests;

public class ZoneTerrainTests
{
    [Theory]
    [InlineData(300f, 500f, GroundEffect.Hay)]
    [InlineData(900f, 900f, GroundEffect.Swamp)]
    [InlineData(200f, 250f, GroundEffect.DriedRiver)]
    [InlineData(500f, 300f, GroundEffect.None)]
    public void OreadsTerrainComesFromItsRegions(float x, float z, GroundEffect expected) =>
        ZoneTerrain.At((byte)ZoneId.Oreads, x, z).Should().Be(expected);

    [Fact]
    public void RonarkLandSwampsSlowNearTheirCentres()
    {
        ZoneTerrain.At((byte)ZoneId.RonarkLand, 1400f, 860f).Should().Be(GroundEffect.SwampSlow);
        ZoneTerrain.At((byte)ZoneId.RonarkLand, 1400f, 1000f).Should().Be(GroundEffect.None);
    }

    [Fact]
    public void OtherZonesHaveNoTerrain() =>
        ZoneTerrain.At((byte)ZoneId.Moradon, 900f, 900f).Should().Be(GroundEffect.None);
}
