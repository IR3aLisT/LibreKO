namespace LibreKO.Domain;

public static class Seasonal
{
    public const int Santa = 1;
    public const int Angel = 2;
    public const int SantaFx = 10042;
    public const int AngelFx = 20044;

    public static int? FxFor(int state) => state switch
    {
        Santa => SantaFx,
        Angel => AngelFx,
        _ => null,
    };
}
