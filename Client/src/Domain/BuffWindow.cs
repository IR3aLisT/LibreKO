namespace LibreKO.Domain;

public static class BuffWindow
{
    public const int PowerItemKind = 255;

    public static bool IsPower(int useItem, int itemKind) => useItem != 0 && itemKind == PowerItemKind;
}
