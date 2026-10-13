using System.Collections.Generic;
using System.Globalization;
using Godot;

namespace LibreKO.Domain;

public readonly record struct RankMark(int Type, int Bronze, int Silver, int Gold)
{
    public int At(int tier) => tier switch { 0 => Bronze, 1 => Silver, _ => Gold };
}

public readonly record struct RankMarkCell(int Type, int Tier, int Count);

public readonly record struct UserRankRow(int Board, int Place);

public enum UserRelation
{
    None = 0,
    Friend = 1,
    Clan = 2,
    Both = 3,
}

public static class UserInfoCard
{
    public const int NoParty = 0xFFFF;
    public const int RankPlaces = 7;
    public const int UnusedBoard = 3;
    public const int AdonisBoard = 1;
    public const int AdonisUnranked = 255;
    public const int SummaryCells = 27;
    public const int RankRows = 6;
    public const int Tiers = 3;
    public const int SpecialMarkFirst = 50;
    public const int DroppedMarkType = 3;
    public const int PodiumPlaces = 3;

    public const int NoUserText = 16803;
    public const int OnlineText = 30012;
    public const int MerchantText = 31052;
    public const int GatheringText = 31053;
    public const int PartyText = 31055;
    public const int ClanGradeText = 15724;
    public const int TrainingKnightText = 15700;
    public const int AccreditedKnightText = 15701;
    public const int RoyalKnightText = 15702;

    private const int LunarWarMarkType = 7;
    private const int LunarWarMarkText = 43619;
    private const int FirstMarkText = 43620;
    private const int FirstSpecialMarkText = 43627;
    private const int LastSpecialMark = 54;
    private const int AccreditedFlagLast = 7;
    private const int RoyalFlagLast = 12;
    private const int StateMerchant = 2;
    private const int StateMerchantAlt = 6;
    private const int StateGathering = 7;
    private const int StateGatheringAlt = 8;
    private const int SignalPartyStall = 1;
    private const string EmptyMark = "simple_rank_mark_99";

    private static readonly Dictionary<int, int> ClassTexts = new()
    {
        [101] = 1420, [201] = 1420, [102] = 1418, [202] = 1418, [103] = 1421, [203] = 1421, [104] = 1417, [204] = 1417,
        [105] = 1409, [106] = 1411, [107] = 1412, [108] = 1414, [109] = 1416, [110] = 1413, [111] = 1415, [112] = 1410,
        [113] = 1422, [213] = 1422, [114] = 1425, [115] = 1426,
        [205] = 1402, [206] = 1407, [207] = 1408, [208] = 1401, [209] = 1406, [210] = 1405, [211] = 1403, [212] = 1404,
        [214] = 1423, [215] = 1424,
    };

    public static readonly Color KarusName = new(1f, 0.376f, 0.376f);
    public static readonly Color ElMoradName = new(0.502f, 0.502f, 1f);

    public static Color? NameColour(int nation) => nation switch
    {
        Nations.Karus => KarusName,
        Nations.ElMorad => ElMoradName,
        _ => null,
    };

    public static int ClassText(int cls) => ClassTexts.TryGetValue(cls, out int text) ? text : 0;

    public static string LevelLine(int level, int rebirth) => $"{level}  /  {rebirth}";

    public static string Thousands(long value) => value.ToString("#,0", CultureInfo.InvariantCulture);

    public static (int Text, int Number) ClanGrade(int flag, int grade) => flag switch
    {
        1 => (ClanGradeText, grade),
        2 => (TrainingKnightText, grade),
        >= 3 and <= AccreditedFlagLast => (AccreditedKnightText, AccreditedFlagLast + 1 - flag),
        > AccreditedFlagLast and <= RoyalFlagLast => (RoyalKnightText, RoyalFlagLast + 1 - flag),
        _ => (0, 0),
    };

    public static int NearbyState(byte signal, byte kind) => kind switch
    {
        StateMerchant => StateMerchant,
        StateMerchantAlt => signal == SignalPartyStall ? StateMerchantAlt : signal,
        0 => 0,
        _ => signal,
    };

    public static int StateText(int state) => state switch
    {
        StateGathering or StateGatheringAlt => GatheringText,
        StateMerchant or StateMerchantAlt => MerchantText,
        _ => OnlineText,
    };

    public static UserRelation Relation(bool friend, bool sameClan) =>
        (friend ? UserRelation.Friend : UserRelation.None) | (sameClan ? UserRelation.Clan : UserRelation.None);

    public static IReadOnlyList<UserRankRow> Rows(IReadOnlyList<int> places)
    {
        var rows = new List<UserRankRow>(RankRows);
        for (int board = 0; board < places.Count && board < RankPlaces; board++)
        {
            if (board == UnusedBoard) continue;
            int place = places[board];
            if (board == AdonisBoard && place == AdonisUnranked) place = 0;
            rows.Add(new UserRankRow(board, place));
        }
        return rows;
    }

    public static string PlaceText(int place) => place > 0 ? place.ToString(CultureInfo.InvariantCulture) : "-";

    public static Color PlaceColour(int place) => place switch
    {
        1 => new Color(1f, 0.843f, 0f),
        >= 2 and <= 10 => new Color(1f, 1f, 0f),
        >= 11 and <= 100 => new Color(0.118f, 0.565f, 1f),
        >= 101 and <= 1000 => new Color(0f, 0.502f, 0f),
        _ => Colors.White,
    };

    public static int BestTier(RankMark mark) =>
        mark.Type >= SpecialMarkFirst || mark.Gold > 0 ? 2 : mark.Silver > 0 ? 1 : 0;

    public static IReadOnlyList<RankMarkCell> Summary(IReadOnlyList<RankMark> marks)
    {
        var cells = new List<RankMarkCell>();
        foreach (var mark in marks)
        {
            if (cells.Count >= SummaryCells) break;
            int tier = BestTier(mark);
            int count = mark.At(tier);
            if (count > 0) cells.Add(new RankMarkCell(mark.Type, tier, count));
        }
        return cells;
    }

    public static string Ribbon(int type, int tier, int count) =>
        count <= 0 ? EmptyMark
        : type >= SpecialMarkFirst ? $"simple_rank_mark_{type}"
        : $"simple_rank_mark_{type}_{tier}";

    public static int MarkTooltip(int type) => type switch
    {
        LunarWarMarkType => LunarWarMarkText,
        >= 0 and < LunarWarMarkType when type != DroppedMarkType => FirstMarkText + type,
        >= SpecialMarkFirst and <= LastSpecialMark => FirstSpecialMarkText + type - SpecialMarkFirst,
        _ => 0,
    };
}
