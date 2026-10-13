namespace LibreKO.Domain;

public enum ShineStyle { Glow, Classic }

public static class ShineStyles
{
    public static string Label(ShineStyle style) => style switch
    {
        ShineStyle.Classic => "Classic",
        _ => "Gold glow",
    };
}
