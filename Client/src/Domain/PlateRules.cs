namespace LibreKO.Domain;

public static class PlateRules
{
    public const int KarusHeroText = 1450;
    public const int ElMoradHeroText = 1451;
    public const int DelosHeroText = 19600;
    public const int DelosBasement = 35;
    public const uint PartyNameRgba = 0xFFFF00FF;
    public const uint PathosPartyNameRgba = 0xADFF2FFF;
    public const uint DelosPartyNameRgba = 0x1064FFFF;

    private static readonly int[] NoFriendColourZones = { 57, 58, 59, 60, 76, 85 };
    private static readonly int[] HeroTitleZones = { 31, 46 };

    public static bool FriendColour(int zone) => System.Array.IndexOf(NoFriendColourZones, zone) < 0;

    public static uint PartyNameColour(int zone) =>
        ZoneLooks.IsPathosZone(zone) ? PathosPartyNameRgba : zone == DelosBasement ? DelosPartyNameRgba : PartyNameRgba;

    public static int AnonymousNameText(int zone, int selfNation, int selfClan, int otherNation, int otherClan)
    {
        if (selfClan <= 0) return 0;
        if (System.Array.IndexOf(HeroTitleZones, zone) >= 0 && otherClan > 0 && otherNation != selfNation)
            return otherNation switch
            {
                Nations.Karus => KarusHeroText,
                Nations.ElMorad => ElMoradHeroText,
                _ => 0,
            };
        return zone == DelosBasement && otherClan != selfClan ? DelosHeroText : 0;
    }
}
