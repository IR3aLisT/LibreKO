using System;
using System.Collections.Generic;

namespace LibreKO.Domain;

public static class VoiceWaveform
{
    public const int Samples = 64;
    public const byte Peak = 255;
    private const double QuietDb = -50;
    private const double Floor = 1e-5;
    private const double MinShown = 0.06;

    public static byte[] FromLevels(IReadOnlyList<float> frameRms)
    {
        if (frameRms.Count == 0) return [];
        var loudness = new double[Samples];
        double loudest = 0;
        for (int i = 0; i < Samples; i++)
        {
            int start = i * frameRms.Count / Samples;
            int end = Math.Max(start + 1, (i + 1) * frameRms.Count / Samples);
            double rms = 0;
            for (int f = start; f < end && f < frameRms.Count; f++) rms = Math.Max(rms, frameRms[f]);
            double db = 20 * Math.Log10(Math.Max(rms, Floor));
            loudness[i] = Math.Clamp((db - QuietDb) / -QuietDb, 0, 1);
            loudest = Math.Max(loudest, loudness[i]);
        }
        var samples = new byte[Samples];
        for (int i = 0; i < Samples; i++)
        {
            double shown = loudest > 0 ? loudness[i] / loudest : 0;
            samples[i] = (byte)Math.Round(Math.Max(MinShown, shown) * Peak);
        }
        return samples;
    }

    public static float Rms(ReadOnlySpan<float> frame)
    {
        if (frame.IsEmpty) return 0;
        double sum = 0;
        foreach (float sample in frame) sum += sample * sample;
        return (float)Math.Sqrt(sum / frame.Length);
    }
}
