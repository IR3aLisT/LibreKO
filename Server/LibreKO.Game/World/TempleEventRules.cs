using LibreKO.Common.Enums;

namespace LibreKO.Game.World;

public static class TempleEventRules
{
    public const int SecondsPerMinute = 60;
    public const int JoinWindowSeconds = 600;
    public const byte DefaultCountdownMinutes = 10;
    public const int ChaosDurationSeconds = 1200;
    public const int BorderDefenseWarDurationSeconds = 1800;
    public const int JuraidMountainDurationSeconds = 2700;
    public const int UnderTheCastleDurationSeconds = 3600;
    public const byte JuraidMountainDefaultMinLevel = 70;
    public const byte JuraidMountainDefaultMaxLevel = 83;
    public const byte BorderDefenseWarDefaultMinLevel = 20;
    public const byte BorderDefenseWarDefaultMaxLevel = 83;
    public const byte UnderTheCastleDefaultMinLevel = 70;
    public const byte UnderTheCastleDefaultMaxLevel = 83;
    public const int ForgottenTempleDurationSeconds = 2400;
    public const byte ForgottenTempleLowMinLevel = 46;
    public const byte ForgottenTempleLowMaxLevel = 59;
    public const byte ForgottenTempleHighMinLevel = 60;
    public const byte ForgottenTempleHighMaxLevel = 83;

    public const int ChaosPlayersPerRoom = ChaosRules.PlayersPerRoom;
    public const ushort BorderDefenseWarIndunEvent = 2;
    public const int MapOfChaos = 910246000;
    public const int StartMinuteOfHour = 0;

    public static ushort SignUpEventId(TempleEvent contest) =>
        contest == TempleEvent.Chaos ? ChaosRules.IndunEvent : (ushort)contest;

    public static short JoinReplyId(TempleEvent contest, byte zone) =>
        contest == TempleEvent.Chaos ? (short)ChaosRules.IndunEvent : zone;

    public static byte ZoneFor(TempleEvent contest) => contest switch
    {
        TempleEvent.Chaos => (byte)ZoneId.ChaosDungeon,
        TempleEvent.BorderDefenseWar => (byte)ZoneId.BorderDefenseWar,
        TempleEvent.JuraidMountain => (byte)ZoneId.JuradMountain,
        TempleEvent.UnderTheCastle => (byte)ZoneId.UnderCastle,
        TempleEvent.ForgottenTemple => (byte)ZoneId.ForgottenTemple,
        _ => 0,
    };

    public static int DurationSecondsFor(TempleEvent contest) => contest switch
    {
        TempleEvent.Chaos => ChaosDurationSeconds,
        TempleEvent.BorderDefenseWar => BorderDefenseWarDurationSeconds,
        TempleEvent.JuraidMountain => JuraidMountainDurationSeconds,
        TempleEvent.UnderTheCastle => UnderTheCastleDurationSeconds,
        TempleEvent.ForgottenTemple => ForgottenTempleDurationSeconds,
        _ => 0,
    };

    public static bool Fits(byte level, byte minLevel, byte maxLevel) =>
        (minLevel == 0 || level >= minLevel) && (maxLevel == 0 || level <= maxLevel);

    public static byte DefaultMinLevel(TempleEvent contest) => contest switch
    {
        TempleEvent.BorderDefenseWar => BorderDefenseWarDefaultMinLevel,
        TempleEvent.JuraidMountain => JuraidMountainDefaultMinLevel,
        TempleEvent.UnderTheCastle => UnderTheCastleDefaultMinLevel,
        TempleEvent.ForgottenTemple => ForgottenTempleLowMinLevel,
        _ => 0,
    };

    public static byte DefaultMaxLevel(TempleEvent contest) => contest switch
    {
        TempleEvent.BorderDefenseWar => BorderDefenseWarDefaultMaxLevel,
        TempleEvent.JuraidMountain => JuraidMountainDefaultMaxLevel,
        TempleEvent.UnderTheCastle => UnderTheCastleDefaultMaxLevel,
        TempleEvent.ForgottenTemple => ForgottenTempleHighMaxLevel,
        _ => 0,
    };

    public static string DescriptionFor(TempleEvent contest) => contest switch
    {
        TempleEvent.Chaos => $"Free-for-all arena, {ChaosRules.PlayersPerRoom} players a room",
        TempleEvent.BorderDefenseWar => $"{BorderDefenseWarService.MaxPlayersPerNationPerRoom} vs {BorderDefenseWarService.MaxPlayersPerNationPerRoom} capture the flag",
        TempleEvent.JuraidMountain => $"{JuraidMountainService.MaxPlayersPerNationPerRoom} vs {JuraidMountainService.MaxPlayersPerNationPerRoom} race to slay Devabird",
        TempleEvent.UnderTheCastle => "Boss dungeon, one hour",
        TempleEvent.ForgottenTemple => "Hold out against the monster waves",
        _ => string.Empty,
    };

    public static string NameFor(TempleEvent contest) => contest switch
    {
        TempleEvent.Chaos => "Chaos Dungeon",
        TempleEvent.BorderDefenseWar => "Border Defense War",
        TempleEvent.JuraidMountain => "Juraid Mountain",
        TempleEvent.UnderTheCastle => "Under The Castle",
        TempleEvent.ForgottenTemple => "Forgotten Temple",
        _ => "Event",
    };
}
