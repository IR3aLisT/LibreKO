using System;

namespace LibreKO.Domain;

public readonly record struct LightRange(int Min, int Max)
{
    public const int Step = 5;
    public const int Neutral = 100;

    public int Clamp(int percent) =>
        Math.Clamp((int)Math.Round(percent / (double)Step) * Step, Min, Max);

    public static float Multiplier(int percent) => percent / (float)Neutral;
}

public static class LightSliders
{
    public static readonly LightRange Brightness = new(60, 160);
    public static readonly LightRange Contrast = new(80, 130);
    public static readonly LightRange NightBrightness = new(50, 200);
    public static readonly LightRange Fog = new(0, 200);
}
