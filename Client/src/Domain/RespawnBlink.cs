namespace LibreKO.Domain;

public static class RespawnBlink
{
    public const int Normal = 1;
    public const int Blinking = 4;
    public const int ChaosNormal = 7;
    public const float Alpha = 0.7f;
    public const float DimFactor = 0.5f;
    public const double HalfPeriod = 0.5;

    public static bool Ends(int abnormal) => abnormal is Normal or ChaosNormal;

    public static bool Dimmed(double elapsed) => elapsed % (2 * HalfPeriod) >= HalfPeriod;

    public static float AlphaAt(double elapsed) => Dimmed(elapsed) ? Alpha * DimFactor : Alpha;
}
