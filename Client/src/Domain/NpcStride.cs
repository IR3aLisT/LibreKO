namespace LibreKO.Domain;

public static class NpcStride
{
    public const double Hold = 0.4;
    public const double NoStep = double.NegativeInfinity;

    public static bool Continues(bool moving, double now, double lastStepAt) =>
        moving || now - lastStepAt < Hold;
}
