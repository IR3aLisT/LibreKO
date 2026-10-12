using System;

namespace LibreKO.Domain;

public static class ServerHosts
{
    public static string Migrate(string host, string retired, string current) =>
        retired.Length > 0 && string.Equals(host.Trim(), retired.Trim(), StringComparison.OrdinalIgnoreCase) ? current : host;
}
