using System.Collections.Generic;

namespace LibreKO.Domain;

public readonly record struct Tremor(float Power, float Cap);

public static class NpcTouch
{
    public const double Cooldown = 1.0;
    public const double TremorInterval = 1.0;
    public const float TremorSeconds = 1f;
    public const float TremorRate = 12f;
    public const float TremorCap = 0.7f;
    public const int TremorSound = 2501;
    public const float UnknownRadius = 1.8f;
    public const float ContactShare = 0.5f;
    public const float SawBladeLockSeconds = 0.5f;

    public const int RollingStoneModel = 5810;
    public const int SawBladeModel = 5815;
    public const int RockslideModel = 30085;
    public const float RollingStonePower = 20f;
    public const float RockslidePower = 5f;
    public const float RockslideCap = 0.03f;

    private static readonly HashSet<int> IgnoredModels = new() { 30088, 30089, 30090, 30091, 30092, 30094, 30200 };
    private static readonly HashSet<int> ChaosCubeModels = new() { 32008, 32080, 32081 };
    private static readonly HashSet<int> ChaosCubeZones = new() { 30, 45, 85 };

    public static bool Ignored(int model) => IgnoredModels.Contains(model);

    public static bool PicksUpCube(int model, int zone) => ChaosCubeModels.Contains(model) && ChaosCubeZones.Contains(zone);

    public static float ContactDistance(int model, float selfRadius, float npcRadius) =>
        model == RockslideModel ? selfRadius + npcRadius : (selfRadius + npcRadius) * ContactShare;

    public static Tremor TremorFrom(int model, float distance, bool moving)
    {
        float square = distance * distance;
        if (square <= 0f) return default;
        if (model == RollingStoneModel) return new Tremor(RollingStonePower / square, TremorCap);
        if (model == RockslideModel && moving) return new Tremor(RockslidePower / square, RockslideCap);
        return default;
    }

    public static bool HurtsOnContact(int model, bool sameNation) => model != RockslideModel || !sameNation;

    public static float Amplitude(Tremor strongest, bool touched) =>
        touched ? strongest.Cap : System.MathF.Min(strongest.Cap, strongest.Power);
}
