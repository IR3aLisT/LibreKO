using System.Collections.Generic;

namespace LibreKO.Domain;

public static class AngerGauge
{
    public const int MinLevel = 50;
    public const int ImagesPerPoint = 2;
    public const int MaxImage = 10;
    public const int FullFrames = 4;
    public const double FullFrameSeconds = 0.1;
    public const int FrameWidth = 39;
    public const int FrameHeight = 59;
    public const int GageRowY = 8;
    public const int FullRowY = 72;
    public const int DefaultX = 15;

    private static readonly int[] GageX = [62, 108, 153, 195, 238, 280, 322, 365, 409, 452];
    private static readonly int[] FullX = [141, 99, 57, 15];
    private static readonly HashSet<int> Zones = [61, 62, 63, 64, 66, 71, 72, 73];

    public static bool ShownIn(int zone, int level) => level >= MinLevel && Zones.Contains(zone);

    public static int ImageFor(int gauge, int current)
    {
        int image = gauge * ImagesPerPoint;
        return image >= 0 && image <= MaxImage ? image : current;
    }

    public static (int X, int Y) GageFrame(int image) => image <= 0 ? (DefaultX, GageRowY) : (GageX[image - 1], GageRowY);

    public static (int X, int Y) FullFrame(int frame) => (FullX[frame % FullFrames], FullRowY);
}
