using System.Collections.Generic;

namespace LibreKO.Domain;

public static class VipVaultAccess
{
    private static readonly HashSet<int> ButtonClosedZones = new() { 45, 84, 85, 89 };
    private static readonly HashSet<int> CommandClosedZones = new() { 45, 84, 85 };

    public static bool ButtonOpens(int zone) => !ButtonClosedZones.Contains(zone);

    public static bool CommandOpens(int zone) => !CommandClosedZones.Contains(zone);
}
