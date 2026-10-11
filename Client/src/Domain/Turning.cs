using System;

namespace LibreKO.Domain;

public static class Turning
{
    public const float KeyboardDegreesPerSecond = 60f;
    public const float FacingDegreesPerSecond = 270f;
    private const float FullTurn = 360f;
    private const float HalfTurn = 180f;

    public static float Toward(float currentDegrees, float goalDegrees, float delta)
    {
        float diff = Wrap(goalDegrees - currentDegrees);
        float step = FacingDegreesPerSecond * delta;
        if (MathF.Abs(diff) <= step) return goalDegrees;
        return currentDegrees + MathF.CopySign(step, diff);
    }

    public static float Wrap(float degrees)
    {
        degrees %= FullTurn;
        if (degrees > HalfTurn) degrees -= FullTurn;
        else if (degrees < -HalfTurn) degrees += FullTurn;
        return degrees;
    }
}
