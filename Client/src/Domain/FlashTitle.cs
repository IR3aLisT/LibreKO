namespace LibreKO.Domain;

public static class FlashTitle
{
    public const int Drop = 1;
    public const int Exp = 2;
    public const int Contribution = 3;
    public const int Noah = 4;

    public static (int Id, string Fallback)? TextFor(int kind) => kind switch
    {
        Drop => (1495, "Item Drop +%d%%"),
        Exp => (1494, "Base Experience for Monster Kills +%d%%"),
        Contribution => (1496, "CONT +%d (Restrictions apply)"),
        Noah => (1497, "Base Noah for Monster Kills +%d%%"),
        _ => null,
    };

    public static string Format(string template, int value) => template.Replace("%d", value.ToString()).Replace("%%", "%");
}
