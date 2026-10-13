using LibreKO.Common.Enums;
using LibreKO.Game.Configuration;
using LibreKO.Game.Protocol.Writers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LibreKO.Game.World;

public interface IKrowazService
{
    bool IsOpen { get; }
    int RemainingSeconds { get; }
    Task OpenAsync();
    Task CloseAsync();
    Task TickAsync();
}

public sealed class KrowazService(
    SessionManager sessionManager,
    IZoneTransitionService zoneTransitionService,
    IKrowazTrapService krowazTrapService,
    IOptions<GameServerSettings> settings,
    ScheduleClock scheduleClock,
    ILogger<KrowazService> logger) : IKrowazService
{
    public const byte Zone = (byte)ZoneId.KrowazDominion;
    public const int OpenMinutes = 60;
    public const byte EntryMinLevel = 70;
    public static readonly int[] CountdownMinutes = [10, 8, 5, 3, 2, 1];

    private const int SecondsPerMinute = 60;
    private const int StartMinuteOfHour = 0;

    private readonly object _lock = new();
    private DateTime _closesAt;
    private int _lastCountdown;
    private int _lastSweepMinute = -1;
    private DateTime _lastOpenMinute = DateTime.MinValue;

    public bool IsOpen => sessionManager.Battle.KrowazOpen;

    public int RemainingSeconds
    {
        get
        {
            lock (_lock)
                return IsOpen ? Math.Max(0, (int)(_closesAt - Clock()).TotalSeconds) : 0;
        }
    }

    public Task OpenAsync() => OpenAtAsync(Clock());

    public async Task OpenAtAsync(DateTime now)
    {
        lock (_lock)
        {
            if (IsOpen)
                return;
            sessionManager.Battle.KrowazOpen = true;
            _closesAt = now.AddMinutes(OpenMinutes);
            _lastCountdown = int.MaxValue;
        }

        logger.LogInformation("Krowaz's Dominion opened until {ClosesAt}", _closesAt);
        await sessionManager.BroadcastToAll(KrowazPacketWriter.Opened());
    }

    public async Task CloseAsync()
    {
        lock (_lock)
        {
            if (!IsOpen)
                return;
            sessionManager.Battle.KrowazOpen = false;
        }

        logger.LogInformation("Krowaz's Dominion closed");
        await sessionManager.BroadcastToAll(KrowazPacketWriter.Closed());
        await SweepAsync();
    }

    public async Task TickAsync()
    {
        await krowazTrapService.TickAsync();
        await TickAtAsync(Clock());
    }

    public async Task TickAtAsync(DateTime now)
    {
        var minute = now.Date.AddHours(now.Hour).AddMinutes(now.Minute);
        if (!IsOpen && now.Minute == StartMinuteOfHour && minute != _lastOpenMinute
            && settings.Value.Events.KrowazStartHours.Contains(now.Hour))
        {
            _lastOpenMinute = minute;
            await OpenAtAsync(now);
        }

        if (IsOpen)
        {
            int remaining = (int)Math.Ceiling((_closesAt - now).TotalSeconds);
            if (remaining <= 0)
            {
                await CloseAsync();
                return;
            }

            foreach (int minutes in CountdownMinutes)
            {
                if (remaining > minutes * SecondsPerMinute || _lastCountdown <= minutes)
                    continue;
                _lastCountdown = minutes;
                await sessionManager.BroadcastToAll(KrowazPacketWriter.Countdown(minutes));
                break;
            }
            return;
        }

        if (now.Minute != _lastSweepMinute)
        {
            _lastSweepMinute = now.Minute;
            await SweepAsync();
        }
    }

    private async Task SweepAsync()
    {
        var inside = sessionManager.GetAll().Where(s => s.ZoneId == Zone && !s.IsGM).ToList();
        foreach (var player in inside)
        {
            try
            {
                await zoneTransitionService.ChangeZoneAsync(player, (byte)ZoneId.Moradon, 0f, 0f);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send {Name} from Krowaz's Dominion to Moradon", player.Name);
            }
        }
    }

    private DateTime Clock() => ScheduleTime.Now(settings.Value.Events, scheduleClock.UtcNow);
}
