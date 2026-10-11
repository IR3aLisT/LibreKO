using System;
using System.Collections.Generic;
using System.Linq;

namespace LibreKO.Domain;

public enum SmashCheck : byte { Allowed, NotGear, EventItem, Protected, Indestructible, Ignored }

public readonly record struct SmashClass(int Band, int Rolls);

public sealed class SmashTable(IReadOnlyDictionary<int, SmashClass> classes, IReadOnlyDictionary<int, IReadOnlyList<int>> bands)
{
    public static readonly SmashTable Empty = new(new Dictionary<int, SmashClass>(), new Dictionary<int, IReadOnlyList<int>>());

    public SmashClass? ClassOf(int gradeClass) => classes.TryGetValue(gradeClass, out var found) ? found : null;

    public IReadOnlyList<int> RewardsOf(int band) => bands.TryGetValue(band, out var items) ? items : Array.Empty<int>();
}

public static class ItemSmash
{
    public const short Succeeded = 1;
    public const short InventoryFull = 2;
    public const short BagFull = 3;
    public const short CannotDisassemble = 4;
    public const short WrongNpc = 5;
    public const short TooSoon = 7;
    public const int ResultSlots = 9;
    public const int PutItemText = 30501;
    public const int WarningText = 30506;
    public const int OkStartsText = 30507;
    public const int PressStopText = 6712;
    public const int WarningTitleText = 7494;
    public const int SpinSound = 340120;
    public const int FarFromNpcText = 30504;

    private const int NotGearText = 30500;
    private const int EventItemText = 30502;
    private const int ProtectedText = 30503;
    private const int IndestructibleText = 15106;
    private const int TooSoonText = 30505;
    private const int InventoryFullText = 10714;
    private const int BagFullText = 7434;
    private const int EventGradeLimit = 2;
    private const int EventGradePet = 7;
    private const int EventGradeNone = 255;
    private const int ItemNumberSpan = 1_000_000_000;
    private const int LastSmashableNumber = 900_000_000;
    private static readonly (int From, int To)[] GearKinds =
    [
        (11, 80), (91, 94), (110, 110), (130, 130), (140, 140), (181, 181), (210, 240),
    ];
    private static readonly int[] KeptRaces = [50, 69, 70, 71, 76];

    public static SmashCheck Check(int itemId, int kind, int race, int gradeClass, ItemFlag state)
    {
        if (!GearKinds.Any(k => kind >= k.From && kind <= k.To)) return SmashCheck.NotGear;
        if (gradeClass is <= EventGradeLimit or EventGradePet or EventGradeNone) return SmashCheck.EventItem;
        if (state is ItemFlag.Bound or ItemFlag.Rented or ItemFlag.CharacterSeal or ItemFlag.Sealed
            or ItemFlag.Duplicate or ItemFlag.SealedPremium) return SmashCheck.Protected;
        if (KeptRaces.Contains(race)) return SmashCheck.Indestructible;
        if (itemId % ItemNumberSpan >= LastSmashableNumber) return SmashCheck.Ignored;
        return SmashCheck.Allowed;
    }

    public static int? CheckText(SmashCheck check) => check switch
    {
        SmashCheck.NotGear => NotGearText,
        SmashCheck.EventItem => EventItemText,
        SmashCheck.Protected => ProtectedText,
        SmashCheck.Indestructible => IndestructibleText,
        _ => null,
    };

    public static int? ReplyText(short result) => result switch
    {
        InventoryFull => InventoryFullText,
        BagFull => BagFullText,
        CannotDisassemble => ProtectedText,
        WrongNpc => FarFromNpcText,
        TooSoon => TooSoonText,
        _ => null,
    };
}
