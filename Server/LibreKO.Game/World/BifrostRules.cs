using LibreKO.Common.Enums;

namespace LibreKO.Game.World;

public enum BifrostPhase : byte
{
    Inactive = 0,
    MonumentFight = 1,
    Farming = 2
}

public enum BifrostEntryResult : byte
{
    Success = 0,
    NotActive = 1,
    MonumentNotFallen = 2,
    LoserDelayActive = 3
}

public static class BifrostRules
{
    public const byte ZoneBifrost = (byte)ZoneId.Bifrost;
    public const byte ZoneRonarkLand = (byte)ZoneId.RonarkLand;

    public const int MonumentNpcId = 601;
    public const int GateNpcId = 602;

    public const int DefaultMonumentMinutes = 30;
    public const int DefaultLoserDelayMinutes = 30;
    public const int DefaultFarmingMinutes = 60;

    public const float KarusEntryX = 78f;
    public const float KarusEntryZ = 730f;

    public const float ElMoradEntryX = 245f;
    public const float ElMoradEntryZ = 950f;

    public const string NoticeStart = "Bifrost event has started!";
    public const string NoticeGatesOpenAll = "The gates to Bifrost are now open to all nations!";
    public const string NoticeFinish = "Bifrost event is now over!";
    public const string NoticeCannotEnterYet = "Your nation cannot enter Bifrost yet. Please wait for the gates to open.";
    public const string NoticeEventNotActive = "The Bifrost event is not currently active.";
    public const string NoticeMonumentNotFallen = "The Bifrost Monument has not fallen yet.";

    public static string NoticeVictory(AccountNation nation, string killerName = "") =>
        nation == AccountNation.ElMorad
            ? "The El Morad warriors have destroyed the monument and can now enter Bifrost!"
            : "The Karus warriors have destroyed the monument and can now enter Bifrost!";
}
