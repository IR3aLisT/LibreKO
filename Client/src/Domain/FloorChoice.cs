using System;
using System.Collections.Generic;

namespace LibreKO.Domain;

public static class FloorChoice
{
    public static float Nearest(float terrain, IReadOnlyList<float> floors, float serverY)
    {
        float best = terrain;
        foreach (float floor in floors)
            if (MathF.Abs(floor - serverY) < MathF.Abs(best - serverY)) best = floor;
        return best;
    }

    public static float Login(float terrain, IReadOnlyList<float> floors, float serverY)
    {
        if (floors.Count == 0) return terrain;
        float nearest = floors[0];
        foreach (float floor in floors)
            if (MathF.Abs(floor - serverY) < MathF.Abs(nearest - serverY)) nearest = floor;
        return MathF.Max(terrain, nearest);
    }
}
