namespace LibreKO.Game.Protocol;

public static class ClientVersionGate
{
    public static readonly TimeSpan CloseDelay = TimeSpan.FromSeconds(1);

    public static bool Accepts(string? minimum, string? clientVersion)
    {
        if (string.IsNullOrWhiteSpace(minimum) || !Version.TryParse(minimum.Trim(), out var required))
            return true;

        return clientVersion != null
            && Version.TryParse(clientVersion.Trim(), out var actual)
            && actual >= required;
    }
}
