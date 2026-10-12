using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Enums;

namespace LibreKO.Game.World;

public static class SiegeRules
{
    public const ushort MaxTariff = 20;
    public const byte ChallengerSlots = 10;
    public const uint ChallengerSignUpFee = 0;
    public const byte FirstWeekday = 1;
    public const byte LastWeekday = 7;

    public static bool IsCastleLord(UserSession session, SiegeWarfareData? siege) =>
        siege is { MasterKnights: > 0 }
        && session.KnightsId == siege.MasterKnights
        && session.KnightsFame == ClanRules.FameChief;

    public static bool IsWeekday(byte day) => day is >= FirstWeekday and <= LastWeekday;

    public static bool IsSiegeTransformation(TransformationUse use) =>
        use is TransformationUse.Siege or TransformationUse.Siege3;

    private const int Catapult = 450001;
    private const int BatteringRam = 450002;
    private const int LadderTruck = 450003;
    private const int MovingTower = 450017;
    private const int GuardTowerArrow = 450018;

    private static bool LunarWarZone(byte zoneId) =>
        zoneId is (byte)ZoneId.NapiesGorge or (byte)ZoneId.AlseidsPrairie or (byte)ZoneId.NiedsTriangle;

    public static bool SiegeTransformationZone(int skillId, byte zoneId) => skillId switch
    {
        Catapult => zoneId == (byte)ZoneId.Delos || LunarWarZone(zoneId) || zoneId == (byte)ZoneId.Oreads,
        BatteringRam or MovingTower or GuardTowerArrow => zoneId is (byte)ZoneId.Delos or (byte)ZoneId.Oreads,
        LadderTruck => LunarWarZone(zoneId) || zoneId == (byte)ZoneId.Oreads,
        _ => zoneId == (byte)ZoneId.Delos,
    };

    public static bool SiegeTransformationAllowed(int skillId, byte zoneId, bool siegeOpen) =>
        SiegeTransformationZone(skillId, zoneId) && (siegeOpen || zoneId != (byte)ZoneId.Delos);

    public static byte DayBefore(byte weekday) => weekday == FirstWeekday ? LastWeekday : (byte)(weekday - 1);
}
