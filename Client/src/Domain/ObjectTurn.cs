namespace LibreKO.Domain;

public static class ObjectTurn
{
    public const byte Increasing = 1;
    public const byte Decreasing = 2;
    public const float RadiansPerSecond = 1f;
    public const int SoundId = 2501;
    private const float FullTurn = System.MathF.PI * 2f;
    private const float RadiansPerDegree = System.MathF.PI / 180f;

    public static float TargetRadians(short degrees) => degrees * RadiansPerDegree;

    public static float StartFrom(float current, float target, byte direction) => direction switch
    {
        Increasing when current > target => current - FullTurn,
        Decreasing when target > current => current + FullTurn,
        _ => current,
    };

    public static double Seconds(float from, float to) => System.MathF.Abs(to - from) / RadiansPerSecond;
}
