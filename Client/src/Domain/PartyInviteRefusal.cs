namespace LibreKO.Domain;

public static class PartyInviteRefusal
{
    public const int Declined = -1;
    public const int LevelGap = -2;
    public const int OtherNation = -3;
    public const int OtherClan = -4;
    public const int OtherZone = -5;
    public const int NotOnline = -6;
    public const int CannotParty = -7;
    public const int NotSameClan = -8;
    public const int RegionBarred = -9;
    public const int MissingItem = -10;

    private const int TextDeclined = 3409;
    private const int TextLevelGap = 3408;
    private const int TextOtherNation = 3407;
    private const int TextOtherClan = 3416;
    private const int TextOtherZone = 3417;
    private const int TextNotOnline = 3418;
    private const int TextCannotParty = 3419;
    private const int TextNotSameClan = 3420;
    private const int TextRegionBarred = 3430;
    private const int TextMissingItem = 37309;
    private const int TextRefused = 3406;

    public static int TextId(int code) => code switch
    {
        Declined => TextDeclined,
        LevelGap => TextLevelGap,
        OtherNation => TextOtherNation,
        OtherClan => TextOtherClan,
        OtherZone => TextOtherZone,
        NotOnline => TextNotOnline,
        CannotParty => TextCannotParty,
        NotSameClan => TextNotSameClan,
        RegionBarred => TextRegionBarred,
        MissingItem => TextMissingItem,
        _ => TextRefused,
    };

    public static string Fallback(int code) => code switch
    {
        LevelGap => "You cannot form a party because of the Level difference.",
        OtherNation => "You cannot form a party with a user from the other nation.",
        OtherClan => "A clan party cannot invite users from a different clan.",
        OtherZone => "You cannot form a party with a user in a different zone.",
        NotOnline => "You cannot form a party with a user that's not online.",
        CannotParty => "You cannot form a party with this user.",
        NotSameClan => "Not a same clan",
        RegionBarred => "You cannot form a party in this region.",
        MissingItem => "You cannot invite this player to a party. Invitee does not have required item",
        _ => "The invitation to the party has been declined.",
    };
}
