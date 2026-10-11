using System.Collections.Generic;

namespace LibreKO.Domain;

public static class StatePlates
{
    public const int ExpeditionZone = 29;
    public const int Expedition = 0;
    public const int ContaminatedResident = 1;
    private const int ExpeditionText = 42154;
    private const int ContaminatedResidentText = 42155;
    private static readonly HashSet<int> TitleHiddenZones = new() { 31, 35, 45, 46, 85 };

    public static bool TitleShownIn(int zone) => !TitleHiddenZones.Contains(zone);

    public static int ExpeditionNameText(int side) => side switch
    {
        Expedition => ExpeditionText,
        ContaminatedResident => ContaminatedResidentText,
        _ => 0,
    };
}
