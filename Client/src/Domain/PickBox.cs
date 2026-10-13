using System;
using System.Collections.Generic;
using Godot;

namespace LibreKO.Domain;

public sealed class NpcRestriction
{
    public HashSet<int> Touch { get; } = new();
    public HashSet<int> Unpickable { get; } = new();
    public HashSet<int> PickTransformed { get; } = new();

    public bool Pickable(int npcId, bool transformed) =>
        !Unpickable.Contains(npcId) && (transformed || !PickTransformed.Contains(npcId));
}

public static class PickBox
{
    public const float MinThickness = 0.5f;
    public const float Widen = 0.25f;
    public const int PetType = 15;
    public const int FixedPoseType = 178;
    public const int PoisonGasType = 184;
    public const int HiddenType = 188;

    private const float Parallel = 1e-8f;

    public static bool SkippedType(int npcType) => npcType is PetType or FixedPoseType or PoisonGasType or HiddenType;

    public static Aabb FromJoints(IReadOnlyList<Vector3> joints)
    {
        if (joints.Count == 0) return new Aabb();
        var min = joints[0];
        var max = joints[0];
        foreach (var p in joints)
        {
            min = new Vector3(MathF.Min(min.X, p.X), MathF.Min(min.Y, p.Y), MathF.Min(min.Z, p.Z));
            max = new Vector3(MathF.Max(max.X, p.X), MathF.Max(max.Y, p.Y), MathF.Max(max.Z, p.Z));
        }
        for (int axis = 0; axis < 3; axis++)
        {
            if (max[axis] - min[axis] >= MinThickness) continue;
            max[axis] += Widen;
            min[axis] -= Widen;
        }
        return new Aabb(min, max - min);
    }

    public static float? Hit(Vector3 origin, Vector3 direction, Aabb box)
    {
        float near = 0f;
        float far = float.MaxValue;
        var min = box.Position;
        var max = box.End;
        for (int axis = 0; axis < 3; axis++)
        {
            if (MathF.Abs(direction[axis]) < Parallel)
            {
                if (origin[axis] < min[axis] || origin[axis] > max[axis]) return null;
                continue;
            }
            float a = (min[axis] - origin[axis]) / direction[axis];
            float b = (max[axis] - origin[axis]) / direction[axis];
            if (a > b) (a, b) = (b, a);
            near = MathF.Max(near, a);
            far = MathF.Min(far, b);
            if (near > far) return null;
        }
        return near;
    }
}
