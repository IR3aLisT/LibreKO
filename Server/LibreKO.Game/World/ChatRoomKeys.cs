namespace LibreKO.Game.World;

public static class ChatRoomKeys
{
    private const string DirectPrefix = "dm:";
    private const char Separator = ':';

    public static string Direct(int a, int b) => $"{DirectPrefix}{Math.Min(a, b)}{Separator}{Math.Max(a, b)}";

    public static bool IsDirect(string key) => key.StartsWith(DirectPrefix, StringComparison.Ordinal);

    public static int PartnerOf(string key, int characterId)
    {
        if (!IsDirect(key)) return 0;
        var parts = key[DirectPrefix.Length..].Split(Separator);
        if (parts.Length != 2 || !int.TryParse(parts[0], out int a) || !int.TryParse(parts[1], out int b)) return 0;
        return a == characterId ? b : b == characterId ? a : 0;
    }
}
