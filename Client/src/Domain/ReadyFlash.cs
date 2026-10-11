namespace LibreKO.Domain;

public static class ReadyFlash
{
    public const int FrameCount = 15;
    public const float Alpha = 100f / 255f;
    public const int SelectedFirstFrame = 3;
    public const int SelectedFrames = FrameCount - SelectedFirstFrame;
    public const double SelectedFrameSeconds = 0.08;

    public static int SelectedFrameAt(double seconds) =>
        SelectedFirstFrame + (int)(seconds / SelectedFrameSeconds) % SelectedFrames;

    public static string FramePath(int frame) => $"res://assets/ui/hotbar_ready/ready_{frame:00}.png";
}
