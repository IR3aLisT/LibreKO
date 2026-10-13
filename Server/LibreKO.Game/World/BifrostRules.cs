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
    LoserDelayActive = 3,
    WrongZone = 4,
    Dead = 5
}

public static class BifrostRules
{
    public const byte ZoneBifrost = (byte)ZoneId.Bifrost;
    public const byte ZoneRonarkLand = (byte)ZoneId.RonarkLand;

    public const int MonumentNpcId = 601;
    public const int GateNpcId = 602;
    public const int UltimaNpcId = 9888;

    public const int DefaultMonumentMinutes = 30;
    public const int DefaultLoserDelayMinutes = 30;
    public const int DefaultFarmingMinutes = 120;

    public const float KarusEntryX = 78f;
    public const float KarusEntryZ = 730f;

    public const float ElMoradEntryX = 245f;
    public const float ElMoradEntryZ = 950f;

    public const float RonarkLandKarusX = 1014f;
    public const float RonarkLandKarusZ = 1020f;

    public const float RonarkLandElMoradX = 1026f;
    public const float RonarkLandElMoradZ = 984f;

    public const float MoradonReturnX = 816f;
    public const float MoradonReturnZ = 532f;

    public const string NoticeStart = "### [Bifrost] The Bifrost Monument in Ronark Land is under attack! ###";
    public const string NoticeDraw = "### [Bifrost] The battle for the Bifrost Monument ended in a draw. ###";
    public const string NoticeGatesOpenAll = "### [Bifrost] The gates to Bifrost are now open to all nations! ###";
    public const string NoticeFinish = "### [Bifrost] The Bifrost event has concluded! ###";
    public const string NoticeCannotEnterYet = "Your nation cannot enter Bifrost yet. Please wait for the gates to open.";
    public const string NoticeEventNotActive = "The Bifrost event is not currently active.";
    public const string NoticeMonumentNotFallen = "The Bifrost Monument has not fallen yet.";

    public static string NoticeVictory(AccountNation nation, string killerName) =>
        nation == AccountNation.ElMorad
            ? $"### [Bifrost] El Morad's {killerName} shattered the Monument! El Morad has early access to Bifrost! ###"
            : $"### [Bifrost] Karus's {killerName} shattered the Monument! Karus has early access to Bifrost! ###";

    public static string NoticeUltimaDefeated(string killerName) =>
        $"### [Bifrost] {killerName} has defeated Ultima! Bi-Frost has concluded! ###";
}
