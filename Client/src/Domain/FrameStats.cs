using System;
using System.Collections.Generic;

namespace LibreKO.Domain;

public static class FrameStats
{
    public static double Percentile(IReadOnlyList<double> samples, double fraction)
    {
        if (samples.Count == 0) return 0;
        var sorted = new List<double>(samples);
        sorted.Sort();
        int index = (int)Math.Ceiling(Math.Clamp(fraction, 0, 1) * sorted.Count) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
    }

    public static double Mean(IReadOnlyList<double> samples)
    {
        if (samples.Count == 0) return 0;
        double sum = 0;
        foreach (double s in samples) sum += s;
        return sum / samples.Count;
    }
}
