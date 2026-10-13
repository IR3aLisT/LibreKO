using System.Collections.Generic;

namespace LibreKO.Domain;

public enum AreaNoticeKind { None, Banner, WarningBanner, Log, Hint }

public readonly record struct AreaNotice(AreaNoticeKind Kind, int TextId, bool LongHold);

public static class AreaNames
{
    public const int Left = 65534;
    public const int Unchanged = 0xFFFF;
    private const int NamedFirst = 10101;
    private const int NamedLast = NamedFirst + 0xD888;

    private const int KarusTexts = 12000;
    private const int ElMoradTexts = 12100;
    private const int MoradonTexts = 12200;
    private const int BifrostTexts = 12300;
    private const int UnderTheCastleTexts = 12350;
    private const int FireDragonTexts = 12902;

    private static readonly Dictionary<int, AreaNotice> Special = new()
    {
        [30] = new(AreaNoticeKind.Banner, 7642, true),
        [31] = new(AreaNoticeKind.Banner, 7643, true),
        [32] = new(AreaNoticeKind.Banner, 7644, true),
        [35] = new(AreaNoticeKind.Log, 7645, false),
        [37] = new(AreaNoticeKind.Log, 7646, false),
        [38] = new(AreaNoticeKind.Banner, 11619, true),
        [39] = new(AreaNoticeKind.Banner, 11619, true),
        [42] = new(AreaNoticeKind.WarningBanner, 11618, true),
        [50] = new(AreaNoticeKind.WarningBanner, 11631, true),
        [51] = new(AreaNoticeKind.Banner, 11721, true),
        [52] = new(AreaNoticeKind.Banner, 11722, true),
        [53] = new(AreaNoticeKind.Banner, 11723, true),
        [54] = new(AreaNoticeKind.Banner, 11724, true),
        [55] = new(AreaNoticeKind.Banner, 11725, true),
        [56] = new(AreaNoticeKind.Banner, 11726, true),
        [57] = new(AreaNoticeKind.Banner, 11727, true),
        [68] = new(AreaNoticeKind.Hint, 18107, true),
        [69] = new(AreaNoticeKind.Hint, 18114, true),
        [70] = new(AreaNoticeKind.Hint, 18117, true),
        [80] = new(AreaNoticeKind.Log, 30040, false),
        [20200] = new(AreaNoticeKind.Banner, 19403, true),
    };

    public static AreaNotice NoticeFor(int zoneId, int regionId)
    {
        if (regionId is >= NamedFirst and <= NamedLast)
            return NameTextId(zoneId, regionId) is var text and > 0
                ? new AreaNotice(AreaNoticeKind.Banner, text, false)
                : default;
        return Special.TryGetValue(regionId, out var notice) ? notice : default;
    }

    public static int NameTextId(int zoneId, int regionId)
    {
        int local = regionId % 10000;
        return zoneId switch
        {
            1 or 3 or 5 or 6 or 18 => KarusTexts + local % 100,
            2 or 4 or 7 or 8 or 28 => ElMoradTexts + local % 200,
            >= 21 and <= 25 => MoradonTexts + local % 2100,
            31 or 46 => BifrostTexts + regionId % 20000,
            34 => FireDragonTexts + regionId % FireDragonTexts,
            86 => UnderTheCastleTexts + local % 8600,
            _ => 0,
        };
    }

    public static int Entered(IReadOnlyList<int> current, ISet<int> previous)
    {
        int entered = current.Count == 0 ? Left : Unchanged;
        foreach (int id in current)
            if (!previous.Contains(id)) { entered = id; break; }
        previous.Clear();
        foreach (int id in current) previous.Add(id);
        return entered;
    }
}
