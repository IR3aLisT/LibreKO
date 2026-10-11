namespace LibreKO.Domain;

public static class PremiumNames
{
    public const int Bronze = 3;
    public const int Gold = 5;
    public const int Platinum = 7;
    public const int Dc = 10;
    public const int Exp = 11;
    public const int War = 12;
    public const int Switching = 13;
    public const string Fallback = "Premium";

    public static string Of(int type) => type switch
    {
        Bronze => "Bronze Premium",
        Gold => "Gold Premium",
        Platinum => "Platinum Premium",
        Dc => "DC Premium",
        Exp => "EXP Premium",
        War => "WAR Premium",
        Switching => "Switching Premium",
        _ => Fallback,
    };
}
