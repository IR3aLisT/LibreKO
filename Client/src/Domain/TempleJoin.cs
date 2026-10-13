namespace LibreKO.Domain;

public static class TempleJoin
{
    public const byte Joined = 0;
    public const byte AlreadyJoined = 1;
    public const byte WrongPoints = 2;
    public const byte NeedsMapOfChaos = 3;
    public const byte LadderPoints = 4;
    public const byte OtherDungeon = 5;
    public const byte LevelTooLow = 6;
    public const byte DuringWar = 7;
    public const byte DuplicateIp = 8;
    public const byte NotOpen = 9;
    public const byte ApplyLadderPoints = 10;
    public const byte LevelTooHigh = 11;

    public const int MapOfChaosCentreText = 18800;
    public const int ChaosSignUpEventId = 24;
    public const string LevelTooHighText = "Your level is too high to apply.";

    public static bool SignedUp(byte result) => result is Joined or AlreadyJoined;

    public static int SignUpEventId(TempleEventType type) =>
        type == TempleEventType.Chaos ? ChaosSignUpEventId : (int)type;

    public static int TextId(byte result) => result switch
    {
        AlreadyJoined => 17999,
        WrongPoints => 18002,
        NeedsMapOfChaos => 18801,
        LadderPoints or ApplyLadderPoints => 41005,
        OtherDungeon => 44542,
        LevelTooLow => 44543,
        DuringWar => 44544,
        DuplicateIp => 44422,
        NotOpen => 44545,
        _ => 0,
    };

    public static string CountLine(bool chaos, int karus, int elMorad) =>
        chaos ? $"Signed up: {karus + elMorad}" : $"Karus {karus}  ·  El Morad {elMorad}";
}
