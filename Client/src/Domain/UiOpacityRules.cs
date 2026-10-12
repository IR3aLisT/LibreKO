namespace LibreKO.Domain;

public static class UiOpacityRules
{
    public const int Lowest = 1;
    public const int Opaque = 255;
    public const int FirstHudLayer = 1;
    public const int LastHudLayer = 99;

    public static int Clamp(int alpha) => System.Math.Clamp(alpha, Lowest, Opaque);

    public static float Fraction(int alpha) => Clamp(alpha) / (float)Opaque;

    public static bool Fades(int layer) => layer is >= FirstHudLayer and <= LastHudLayer;
}
