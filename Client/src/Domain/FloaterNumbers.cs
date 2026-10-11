using System.Globalization;

namespace LibreKO.Domain;

public static class FloaterNumbers
{
    public const long AbbreviateFrom = 10_000;
    public const double MergeSeconds = 0.12;
    private const double DecimalBelow = 100;
    private static readonly (long Unit, string Suffix)[] Units =
    {
        (1_000_000_000_000, "T"), (1_000_000_000, "B"), (1_000_000, "M"), (1_000, "K"),
    };

    public static string Format(long amount)
    {
        long magnitude = System.Math.Abs(amount);
        if (magnitude < AbbreviateFrom) return amount.ToString("n0", CultureInfo.InvariantCulture);
        string sign = amount < 0 ? "-" : "";
        foreach (var (unit, suffix) in Units)
        {
            if (magnitude < unit) continue;
            double scaled = (double)magnitude / unit;
            string number = scaled < DecimalBelow
                ? (System.Math.Floor(scaled * 10) / 10).ToString("0.#", CultureInfo.InvariantCulture)
                : System.Math.Floor(scaled).ToString("0", CultureInfo.InvariantCulture);
            return sign + number + suffix;
        }
        return amount.ToString("n0", CultureInfo.InvariantCulture);
    }

    public static bool Merges(double bornAt, double now) => now - bornAt <= MergeSeconds;
}
