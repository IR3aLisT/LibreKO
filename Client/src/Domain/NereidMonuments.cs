using System.Collections.Generic;

namespace LibreKO.Domain;

public static class NereidMonuments
{
    public const int Zone = 64;
    public const int Count = 7;
    public const byte NoOwner = 0;

    public const int OccupiedText = 13001;
    public const int LostText = 13002;
    public const int ProvisionsText = 13017;
    public const int ProvisionPointsText = 13018;
    public const int ContText = 13019;
    public const int ResultText = 13020;
    public const int KarusText = 3102;
    public const int ElMoradText = 3101;

    private static readonly int[] NameTexts = { 13015, 13006, 13009, 13010, 13011, 13014, 13005 };
    private static readonly string[] NameFallbacks =
    {
        "Karus main territory", "El Morad transport line", "Lake of Life", "Foss Castle",
        "Swamp of Shadows", "Karus provision line", "El Morad main territory",
    };
    private static readonly int[] Weights = { 2, 2, 2, 2, 2, 2, 2 };
    private static readonly (int X, int Z)[] Spots = { (687, 705), (224, 468), (346, 636), (506, 510), (661, 387), (796, 539), (338, 309) };

    public static bool Has(int index) => index >= 0 && index < Count;

    public static (int Id, string Fallback) Name(int index) => (NameTexts[index], NameFallbacks[index]);

    public static (int X, int Z) Spot(int index) => Spots[index];

    public static int Held(IReadOnlyList<byte> owners, int nation)
    {
        int held = 0;
        for (int i = 0; i < owners.Count && i < Count; i++)
            if (owners[i] == nation) held += Weights[i];
        return held;
    }

    public static (int Id, string Fallback) NationName(int nation) =>
        nation == Nations.Karus ? (KarusText, "Karus") : (ElMoradText, "El Morad");

    public static int CaptureText(int owner, int myNation) => owner == myNation ? OccupiedText : LostText;
}
