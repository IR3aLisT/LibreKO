using LibreKO.Common.Enums;

namespace LibreKO.Game.World;

public enum VanguardPhase : byte
{
    Off,
    Waiting,
    Accepting,
    Active,
}

public readonly record struct GroundRect(float MinX, float MinZ, float MaxX, float MaxZ)
{
    public bool Contains(float x, float z) => x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;
}

public static class VanguardRules
{
    public static readonly byte[] Zones = [(byte)ZoneId.RonarkLand, (byte)ZoneId.RonarkLandBase];

    public const int WaitSeconds = 60;
    public const int AcceptSeconds = 11;
    public const int ActiveSeconds = 600;
    public const int ListDelaySeconds = 30;
    public const int PositionSeconds = 60;
    public const int BaseCheckSeconds = 15;
    public const int MaxPerNation = 5;

    public const int RewardMarkSkill = 302166;
    public const int OffensiveEmblem = 914053879;
    public const int DefensiveEmblem = 914054880;
    public const int SurvivorLoyalty = 50;
    public const int KillerPartyLoyalty = 80;

    public const ushort CentreX = 10080;
    public const ushort CentreZ = 10103;

    public static readonly GroundRect ElMoradBase = new(540f, 875f, 699f, 984f);
    public static readonly GroundRect KarusBase = new(1314f, 1020f, 1444f, 1140f);

    public static bool InOwnBase(byte zone, AccountNation nation, float x, float z) =>
        zone == (byte)ZoneId.RonarkLand && nation switch
        {
            AccountNation.Karus => KarusBase.Contains(x, z),
            AccountNation.ElMorad => ElMoradBase.Contains(x, z),
            _ => false,
        };
}
