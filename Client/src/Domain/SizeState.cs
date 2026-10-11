namespace LibreKO.Domain;

public static class SizeState
{
    public const int Normal = 1;
    public const int Giant = 2;
    public const int Dwarf = 3;
    public const int GiantTarget = 6;
    public const int ChaosNormal = 7;
    public const int FlatSideways = 8;
    public const int FlatForward = 9;
    public const int Squashed = 10;

    public const int GiantTargetFxId = 30602;
    public const double TweenSeconds = 1.0;

    private const float NormalScale = 1f;
    private const float KurianScale = 1.1f;
    private const float GiantScale = 2f;
    private const float DwarfScale = 0.5f;
    private const float FlatThin = 0.1f;
    private const float FlatWide = 1.3f;
    private const float SquashedHeight = 0.7f;
    private const float SquashedDepth = 1.4f;
    private const int KarusKurianRace = 6;
    private const int ElMoradPortuRace = 14;
    private const int ChaosZone = 85;
    private const int ChaosNormalExemptZone = 35;

    public readonly record struct Scale(float X, float Y, float Z)
    {
        public static Scale Uniform(float value) => new(value, value, value);
    }

    public static float BaseScale(int race) => race is KarusKurianRace or ElMoradPortuRace ? KurianScale : NormalScale;

    public static Scale? Target(int state, int race, int zone) => state switch
    {
        Normal => Scale.Uniform(BaseScale(race)),
        Giant or GiantTarget => Scale.Uniform(GiantScale),
        Dwarf => Scale.Uniform(DwarfScale),
        ChaosNormal => zone is ChaosZone or ChaosNormalExemptZone ? null : Scale.Uniform(BaseScale(race)),
        FlatSideways => new Scale(FlatThin, NormalScale, FlatWide),
        FlatForward => new Scale(FlatWide, NormalScale, FlatThin),
        Squashed => new Scale(NormalScale, SquashedHeight, SquashedDepth),
        _ => null,
    };

    public static int OnSpawn(int recorded, int race, int zone) => Target(recorded, race, zone) != null ? recorded : Normal;

    public static bool AddsFx(int state) => state == GiantTarget;

    public static bool RemovesFx(int state) => state == Normal;

    public static float Progress(double elapsed) =>
        elapsed <= 0 ? 0f : elapsed >= TweenSeconds ? 1f : (float)(elapsed / TweenSeconds);
}
