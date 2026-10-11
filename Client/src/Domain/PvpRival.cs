namespace LibreKO.Domain;

public static class PvpRival
{
    public const string MarkerFx = "z_revenge_a01_2009";
    public const int MinLevel = 50;
    public const int AssignedText = 18602;
    public const int CancelledText = 18603;

    private const int NapiesGorge = 61;
    private const int AlseidsPrairie = 62;
    private const int NiedsTriangle = 63;
    private const int NereidsIsland = 64;
    private const int Oreads = 66;
    private const int RonarkLand = 71;
    private const int Ardream = 72;
    private const int RonarkLandBase = 73;

    public static bool MarksIn(int zone) => zone is RonarkLand or Ardream or RonarkLandBase
        or NapiesGorge or AlseidsPrairie or NiedsTriangle or NereidsIsland or Oreads;
}
