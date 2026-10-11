using System.Globalization;

namespace LibreKO.Domain;

public static class HotbarLayout
{
    public const int Pages = 8;
    public const int SlotsPerPage = 10;
    public const int Total = Pages * SlotsPerPage;
    public const int MaxBars = 4;
    public const int LegacySlotsPerPage = 8;
    public const int LegacyTotal = Pages * LegacySlotsPerPage;
    public const float ScaleMin = 0.7f;
    public const float ScaleMax = 1.5f;
    public const float ScaleStep = 0.05f;
    public const float ScaleDefault = 1f;
    public const int GapMin = 0;
    public const int GapMax = 12;
    public const int GapDefault = 6;
    private const int VerticalGapShare = 2;

    public static float ClampScale(float scale) =>
        System.Math.Clamp(System.MathF.Round(scale / ScaleStep) * ScaleStep, ScaleMin, ScaleMax);

    public static int ClampGap(int gap) => System.Math.Clamp(gap, GapMin, GapMax);

    public static int SlotGap(int gap, bool vertical) => vertical ? gap / VerticalGapShare : gap;

    public static int[] Normalize(int[] saved)
    {
        var slots = new int[Total];
        int perPage = saved.Length == LegacyTotal ? LegacySlotsPerPage : SlotsPerPage;
        for (int i = 0; i < saved.Length; i++)
        {
            int page = i / perPage;
            if (page >= Pages) break;
            slots[page * SlotsPerPage + i % perPage] = saved[i];
        }
        return slots;
    }

    public static string KeyLabel(int slotInPage) =>
        ((slotInPage + 1) % SlotsPerPage).ToString(CultureInfo.InvariantCulture);
}
