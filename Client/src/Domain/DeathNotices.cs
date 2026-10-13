using System.Collections.Generic;

namespace LibreKO.Domain;

public static class DeathNotices
{
    public const int CoordinatesKind = 0;
    public const int DefeatedKind = 1;
    public const int AvengedKind = 2;
    public const int VanguardKind = 3;

    public const int CoordinatesText = 7109;
    public const int DefeatedText = 7108;
    public const int AvengedText = 18600;
    public const int ElMoradKingDownText = 7113;
    public const int KarusKingDownText = 7114;

    public const int OwnKillZone = 85;
    public const int NoKingZone = 45;
    public const string KarusNameColour = "00bfff";
    public const string ElMoradNameColour = "f08080";
    public const string LineColour = "ffff00";
    public const string OwnKillColour = "da70d6";

    private static readonly HashSet<int> KingZones = new() { 61, 62, 63, 64, 66 };

    public static int TextFor(int kind, int zone, bool victimIsKing, int killerNation, bool victimListed = false) => kind switch
    {
        DefeatedKind => DefeatedText,
        AvengedKind => AvengedText,
        VanguardKind => Vanguard.SlainText(killerNation),
        _ when zone != OwnKillZone && zone != NoKingZone && victimIsKing && KingZones.Contains(zone) =>
            killerNation == Nations.Karus ? ElMoradKingDownText : KarusKingDownText,
        _ when victimListed && zone != NoKingZone => Vanguard.KarusSlainText,
        _ => CoordinatesText,
    };

    public static string? NameColour(int nation) => nation switch
    {
        Nations.Karus => KarusNameColour,
        Nations.ElMorad => ElMoradNameColour,
        _ => null,
    };

    public static string LineColourFor(int zone, bool killedByMe, int kind = CoordinatesKind) =>
        kind == VanguardKind ? Vanguard.ListColour : zone == OwnKillZone && killedByMe ? OwnKillColour : LineColour;
}
