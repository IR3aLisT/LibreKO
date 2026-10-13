namespace LibreKO.Domain;

public enum QualityLevel { Off, Low, Medium, High }

public enum GraphicsPreset { Low, Medium, High, Ultra, Custom }

public readonly record struct GraphicsChoice(
    QualityLevel Shadows, QualityLevel Ssao, bool VolumetricFog, bool Bloom, bool Clouds, int AntiAlias, float ViewDistance);

public static class GraphicsPresets
{
    public const int AaOff = 0;
    public const int AaFxaa = 1;
    public const int AaMsaa2X = 2;
    public const int AaMsaa4X = 3;
    private const float ViewNear = 0.5f;
    private const float ViewMid = 0.75f;
    private const float ViewFar = 1f;
    private const float ViewTolerance = 0.01f;

    public static readonly GraphicsPreset[] Named = { GraphicsPreset.Low, GraphicsPreset.Medium, GraphicsPreset.High, GraphicsPreset.Ultra };

    public static GraphicsChoice Of(GraphicsPreset preset) => preset switch
    {
        GraphicsPreset.Low => new(QualityLevel.Low, QualityLevel.Off, false, false, false, AaOff, ViewNear),
        GraphicsPreset.Medium => new(QualityLevel.Medium, QualityLevel.Low, false, true, true, AaFxaa, ViewMid),
        GraphicsPreset.Ultra => new(QualityLevel.High, QualityLevel.High, true, true, true, AaMsaa4X, ViewFar),
        _ => new(QualityLevel.Medium, QualityLevel.Medium, false, true, true, AaMsaa2X, ViewFar),
    };

    public static GraphicsPreset Match(GraphicsChoice choice)
    {
        foreach (var preset in Named)
        {
            var named = Of(preset);
            if (named with { ViewDistance = choice.ViewDistance } == choice
                && System.Math.Abs(named.ViewDistance - choice.ViewDistance) <= ViewTolerance)
                return preset;
        }
        return GraphicsPreset.Custom;
    }
}
