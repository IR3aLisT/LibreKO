using Godot;

namespace LibreKO;

public static class FxSpin
{
    public static Vector3 RateFromBaked(Vector3 bakedRotVel) => -bakedRotVel;

    public static float RollFromBaked(float bakedRollRate) => -bakedRollRate;

    public static Basis MeshOrientation(Vector3 rate, float age) => Basis.FromEuler(rate * age, EulerOrder.Zyx);
}
