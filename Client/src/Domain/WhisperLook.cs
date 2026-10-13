using System;

namespace LibreKO.Domain;

public enum WhisperSize { Small, Medium, Large }

public static class WhisperLook
{
    public const float MinAlpha = 0.25f;
    public const float MaxAlpha = 1f;
    public const float DefaultAlpha = 0.86f;
    public const WhisperSize DefaultSize = WhisperSize.Large;

    public readonly record struct Box(int Width, int Height, int Wrap);

    public static Box BoxOf(WhisperSize size) => size switch
    {
        WhisperSize.Small => new Box(200, 110, 140),
        WhisperSize.Medium => new Box(236, 140, 168),
        _ => new Box(320, 210, 240),
    };

    public static float ClampAlpha(float alpha) => Math.Clamp(alpha, MinAlpha, MaxAlpha);

    public static WhisperSize SizeFrom(int value) =>
        Enum.IsDefined(typeof(WhisperSize), value) ? (WhisperSize)value : DefaultSize;
}
