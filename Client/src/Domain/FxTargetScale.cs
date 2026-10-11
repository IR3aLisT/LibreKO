using System;

namespace LibreKO.Domain;

public static class FxTargetScale
{
    public const float MaxScale = 16f;
    public const float PlayerWidth = 1f;
    public const float PlayerHeight = 1.8f;

    public static float WorldSize(float width, float depth, float height) =>
        Math.Min(MaxScale, Math.Max(Math.Max(width, depth), height));

    public static float ForPinned(float worldSize, float bodyScale) =>
        bodyScale > 0f ? worldSize / bodyScale : worldSize;
}
