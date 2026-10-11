namespace LibreKO.Domain;

public static class FrameStep
{
    public const double Stall = 1.0;
    public const double StallStep = 1.0 / 30.0;

    public static double Clamp(double delta) => delta >= Stall ? StallStep : delta;
}
