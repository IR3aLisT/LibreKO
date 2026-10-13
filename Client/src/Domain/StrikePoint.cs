using Godot;

namespace LibreKO.Domain;

public static class StrikePoint
{
    public const float FallbackShare = 1f / 3f;
    private const float Parallel = 1e-6f;

    public static Vector3? Entry(Aabb box, Vector3 from, Vector3 to)
    {
        var dir = to - from;
        float enter = 0f, leave = 1f;
        var min = box.Position;
        var max = box.End;
        for (int axis = 0; axis < 3; axis++)
        {
            if (Mathf.Abs(dir[axis]) < Parallel)
            {
                if (from[axis] < min[axis] || from[axis] > max[axis]) return null;
                continue;
            }
            float a = (min[axis] - from[axis]) / dir[axis];
            float b = (max[axis] - from[axis]) / dir[axis];
            if (a > b) (a, b) = (b, a);
            enter = Mathf.Max(enter, a);
            leave = Mathf.Min(leave, b);
            if (enter > leave) return null;
        }
        return from + dir * enter;
    }

    public static Vector3 Extended(Vector3 from, Vector3 to) => to + (to - from);

    public static Vector3 TowardAttacker(Aabb box, Vector3 attacker)
    {
        var centre = box.GetCenter();
        var dir = attacker - centre;
        return dir.LengthSquared() > 0f ? centre + dir.Normalized() * box.Size.Y * FallbackShare : centre;
    }
}
