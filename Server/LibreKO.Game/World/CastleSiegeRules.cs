namespace LibreKO.Game.World;

public enum CastleSiegePhase : byte
{
    None,
    Countdown,
    War,
}

public enum SiegeSide : byte
{
    None,
    Owner,
    Challenger,
}

public static class CastleSiegeRules
{
    public const byte StateNone = 0;
    public const byte StateUnheld = 1;
    public const byte StateHeld = 2;
    public const byte StateEnded = 4;

    public const int CountdownMinutes = 10;
    public const int WarSeconds = 3000;
    public const int HoldSeconds = 960;
    public const int ArtifactNpc = 541;
    public const int ArtifactX = 505;
    public const int ArtifactZ = 790;
    public const float ArtifactFallbackY = 79f;

    private const byte FirstWeekday = 1;

    public static SiegeSide SideOf(int clanId, int allianceId, int ownerClan) =>
        ownerClan > 0 && (clanId == ownerClan || allianceId == ownerClan) ? SiegeSide.Owner : SiegeSide.Challenger;

    public static bool Hostile(SiegeSide attacker, SiegeSide target) =>
        attacker != SiegeSide.None && target != SiegeSide.None && attacker != target;

    public static byte WeekdayOf(DayOfWeek day) => (byte)((int)day + FirstWeekday);

    public static bool IsScheduledStart(byte warDay, byte warHour, byte warMinute, DateTime now)
    {
        if (!SiegeRules.IsWeekday(warDay))
            return false;
        foreach (var day in new[] { now.Date, now.Date.AddDays(1) })
        {
            var war = day.AddHours(warHour).AddMinutes(warMinute);
            var start = war.AddMinutes(-CountdownMinutes);
            if (WeekdayOf(war.DayOfWeek) == warDay && start.Date == now.Date
                && start.Hour == now.Hour && start.Minute == now.Minute)
                return true;
        }
        return false;
    }

    public static int HoldAfterCapture(int warSecondsLeft) => Math.Min(HoldSeconds, warSecondsLeft);
}
