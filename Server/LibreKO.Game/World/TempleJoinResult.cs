namespace LibreKO.Game.World;

public enum TempleJoinResult : byte
{
    Joined = 0,
    AlreadyJoined = 1,
    NeedsMapOfChaos = 3,
    LevelTooLow = 6,
    NotOpen = 9,
    LevelTooHigh = 11,
}
