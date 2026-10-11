using LibreKO.Common.Enums;

namespace LibreKO.Game.World;

public enum GroundEffect : byte
{
    None = 0,
    Hay = 1,
    Swamp = 2,
    DriedRiver = 3,
    SwampSlow = 4,
}

public static class ZoneTerrain
{
    private const float RonarkSwampRadius = 80f;

    private readonly record struct Area(GroundEffect Effect, float MinX, float MinZ, float MaxX, float MaxZ)
    {
        public bool Contains(float x, float z) => x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;
    }

    private static readonly Area[] OreadsAreas =
    [
        new(GroundEffect.Hay, 220, 452, 408, 560),
        new(GroundEffect.Hay, 280, 388, 408, 448),
        new(GroundEffect.Hay, 292, 316, 408, 384),
        new(GroundEffect.Hay, 644, 456, 816, 572),
        new(GroundEffect.Hay, 640, 576, 772, 736),
        new(GroundEffect.Swamp, 728, 776, 820, 972),
        new(GroundEffect.Swamp, 636, 868, 664, 948),
        new(GroundEffect.Swamp, 668, 808, 724, 968),
        new(GroundEffect.Swamp, 824, 464, 996, 976),
        new(GroundEffect.Swamp, 648, 852, 668, 940),
        new(GroundEffect.DriedRiver, 12, 312, 280, 376),
        new(GroundEffect.DriedRiver, 36, 380, 240, 452),
        new(GroundEffect.DriedRiver, 112, 456, 204, 496),
        new(GroundEffect.DriedRiver, 112, 84, 396, 192),
        new(GroundEffect.DriedRiver, 160, 8, 364, 80),
        new(GroundEffect.DriedRiver, 16, 196, 456, 308),
        new(GroundEffect.DriedRiver, 112, 84, 344, 200),
        new(GroundEffect.DriedRiver, 136, 20, 324, 80),
    ];

    private static readonly (float X, float Z)[] RonarkSwamps =
    [
        (1400f, 833f), (1310f, 736f), (1190f, 669f), (1081f, 611f),
        (671f, 1170f), (759f, 1260f), (850f, 1335f), (974f, 1379f),
    ];

    public static GroundEffect At(byte zoneId, float x, float z)
    {
        switch ((ZoneId)zoneId)
        {
            case ZoneId.Oreads:
                foreach (var area in OreadsAreas)
                    if (area.Contains(x, z)) return area.Effect;
                return GroundEffect.None;
            case ZoneId.RonarkLand:
                foreach (var (px, pz) in RonarkSwamps)
                {
                    float dx = x - px, dz = z - pz;
                    if (dx * dx + dz * dz <= RonarkSwampRadius * RonarkSwampRadius) return GroundEffect.SwampSlow;
                }
                return GroundEffect.None;
            default:
                return GroundEffect.None;
        }
    }
}
