using LibreKO.Common.Domain.Services;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Common.Infrastructure.Persistence;
using LibreKO.Game.Configuration;
using LibreKO.Game.Protocol.Writers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LibreKO.Game.World;

public interface ICastleSiegeService
{
    CastleSiegePhase Phase { get; }
    int WarSecondsLeft { get; }
    Task StartCountdownAsync();
    Task OpenAsync();
    Task CloseAsync();
    Task OnNpcKilledAsync(NpcInstance npc, UserSession? killer);
    Packet StatusPacket();
}

public sealed class CastleSiegeService : BackgroundService, ICastleSiegeService
{
    private const int TickMilliseconds = 1000;
    private const int SecondsPerMinute = 60;
    private const int TextMinutesLeft = 173;
    private const int TextAboutToBegin = 169;
    private const int TextBegun = 171;
    private const int TextEnded = 172;
    private const int TextArtifactDestroyed = 179;

    private readonly SessionManager _sessions;
    private readonly IGameDataService _gameData;
    private readonly IZoneTransitionService _zones;
    private readonly INpcSummonService _summon;
    private readonly INpcLifecycleService _lifecycle;
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<GameServerSettings> _settings;
    private readonly ScheduleClock _scheduleClock;
    private readonly ILogger<CastleSiegeService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private int _countdownSeconds;
    private int _warSeconds;
    private int _holdSeconds;
    private int _lastScheduleMinute = -1;
    private NpcInstance? _artifact;

    public CastleSiegeService(
        SessionManager sessions,
        IGameDataService gameData,
        IZoneTransitionService zones,
        INpcSummonService summon,
        INpcLifecycleService lifecycle,
        IServiceScopeFactory scopes,
        IOptions<GameServerSettings> settings,
        ScheduleClock scheduleClock,
        ILogger<CastleSiegeService> logger)
    {
        _sessions = sessions;
        _gameData = gameData;
        _zones = zones;
        _summon = summon;
        _lifecycle = lifecycle;
        _scopes = scopes;
        _settings = settings;
        _scheduleClock = scheduleClock;
        _logger = logger;
        _zones.ZoneAbilitySent += OnZoneAbilitySentAsync;
    }

    public CastleSiegePhase Phase { get; private set; }

    public int WarSecondsLeft => Phase == CastleSiegePhase.War ? Math.Max(0, _warSeconds) : 0;

    private short OwnerClan => _gameData.SiegeWarfare?.MasterKnights ?? 0;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(TickMilliseconds));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await TickAsync(ScheduleTime.Now(_settings.Value.Events, _scheduleClock.UtcNow));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Castle siege tick failed");
            }
        }
    }

    internal async Task TickAsync(DateTime now)
    {
        switch (Phase)
        {
            case CastleSiegePhase.None:
                if (_gameData.SiegeWarfare is { } siege && now.Minute != _lastScheduleMinute
                    && CastleSiegeRules.IsScheduledStart(siege.WarDay, siege.WarTime, siege.WarMinute, now))
                {
                    _lastScheduleMinute = now.Minute;
                    await StartCountdownAsync();
                }
                break;
            case CastleSiegePhase.Countdown:
                if (--_countdownSeconds <= 0)
                    await OpenAsync();
                else if (_countdownSeconds % SecondsPerMinute == 0)
                    await AnnounceMinutesLeftAsync(_countdownSeconds / SecondsPerMinute);
                break;
            case CastleSiegePhase.War:
                _warSeconds--;
                if (OwnerClan > 0)
                    _holdSeconds--;
                if (_warSeconds <= 0 || (OwnerClan > 0 && _holdSeconds <= 0))
                    await CloseAsync();
                else if (_warSeconds % SecondsPerMinute == 0)
                    await BroadcastToDelosAsync(StatusPacket());
                break;
        }
    }

    public async Task StartCountdownAsync()
    {
        if (Phase != CastleSiegePhase.None)
            return;
        Phase = CastleSiegePhase.Countdown;
        _countdownSeconds = CastleSiegeRules.CountdownMinutes * SecondsPerMinute;
        _logger.LogInformation("Castle siege countdown started");
        await AnnounceMinutesLeftAsync(CastleSiegeRules.CountdownMinutes);
    }

    public async Task OpenAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (Phase == CastleSiegePhase.War)
                return;
            Phase = CastleSiegePhase.War;
            _warSeconds = CastleSiegeRules.WarSeconds;
            _holdSeconds = OwnerClan > 0 ? CastleSiegeRules.HoldAfterCapture(_warSeconds) : 0;
            _sessions.Battle.CastleSiegeOpen = true;
            _logger.LogInformation("Castle siege opened, owner clan {Clan}", OwnerClan);
        }
        finally
        {
            _gate.Release();
        }

        await SpawnArtifactAsync();
        await AnnounceAsync(TextBegun, "Siege has begun");
        foreach (var session in InDelos())
            await _zones.SendZoneAbilityAsync(session);
    }

    public async Task CloseAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (Phase == CastleSiegePhase.None)
                return;
            bool wasWar = Phase == CastleSiegePhase.War;
            Phase = CastleSiegePhase.None;
            _sessions.Battle.CastleSiegeOpen = false;
            _countdownSeconds = _warSeconds = _holdSeconds = 0;
            if (!wasWar)
                return;
        }
        finally
        {
            _gate.Release();
        }

        await RemoveArtifactAsync();
        await BroadcastToDelosAsync(StatusPacket(CastleSiegeRules.StateEnded));
        foreach (var session in InDelos())
        {
            session.SiegeSide = SiegeSide.None;
            await _zones.SendZoneAbilityAsync(session);
        }
        await AnnounceAsync(TextEnded, "Siege has ended");
        _logger.LogInformation("Castle siege closed, owner clan {Clan}", OwnerClan);
    }

    public async Task OnNpcKilledAsync(NpcInstance npc, UserSession? killer)
    {
        if (Phase != CastleSiegePhase.War || npc.NpcId != CastleSiegeRules.ArtifactNpc || npc.ZoneId != (byte)ZoneId.Delos)
            return;

        _artifact = null;
        short newOwner = killer?.KnightsId > 0 ? killer.KnightsId : (short)0;
        await SetOwnerAsync(newOwner);
        _holdSeconds = newOwner > 0 ? CastleSiegeRules.HoldAfterCapture(_warSeconds) : 0;
        _logger.LogInformation("Castle captured by clan {Clan} ({Killer})", newOwner, killer?.Name);

        await AnnounceAsync(TextArtifactDestroyed, "Center Artifact has been destroyed");
        foreach (var session in InDelos())
        {
            StampSide(session);
            await _zones.ChangeZoneAsync(session, (byte)ZoneId.Delos, 0f, 0f);
        }
        await SpawnArtifactAsync();
        await BroadcastToDelosAsync(StatusPacket());
    }

    public Packet StatusPacket() => StatusPacket(Phase == CastleSiegePhase.War
        ? OwnerClan > 0 ? CastleSiegeRules.StateHeld : CastleSiegeRules.StateUnheld
        : CastleSiegeRules.StateNone);

    private Packet StatusPacket(byte state)
    {
        var owner = OwnerClan > 0 ? _sessions.Knights.GetClan(OwnerClan) : null;
        return SiegePacketWriter.WarStatus(
            state,
            OwnerClan,
            owner == null || owner.MarkVersion < 0 ? (short)0 : owner.MarkVersion,
            owner?.AllianceId ?? 0,
            Phase == CastleSiegePhase.War ? _holdSeconds : 0,
            Phase == CastleSiegePhase.War ? _warSeconds : 0,
            owner?.Name ?? string.Empty);
    }

    private async Task OnZoneAbilitySentAsync(UserSession session)
    {
        if (session.ZoneId != (byte)ZoneId.Delos)
        {
            session.SiegeSide = SiegeSide.None;
            return;
        }
        if (Phase != CastleSiegePhase.War)
        {
            session.SiegeSide = SiegeSide.None;
            await session.Client.SendPacket(StatusPacket());
            return;
        }
        if (session.KnightsId <= 0 && !session.IsGM)
        {
            await _zones.ChangeZoneAsync(session, (byte)ZoneId.Moradon, 0f, 0f);
            return;
        }
        StampSide(session);
        await session.Client.SendPacket(StatusPacket());
    }

    private void StampSide(UserSession session)
    {
        var alliance = session.KnightsId > 0 ? _sessions.Knights.GetClan(session.KnightsId)?.AllianceId ?? 0 : 0;
        session.SiegeSide = CastleSiegeRules.SideOf(session.KnightsId, alliance, OwnerClan);
    }

    private async Task SetOwnerAsync(short clanId)
    {
        if (_gameData.SiegeWarfare is not { } siege)
            return;
        siege.MasterKnights = clanId;
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Attach(siege);
        db.Entry(siege).Property(s => s.MasterKnights).IsModified = true;
        await db.SaveChangesAsync();
    }

    private async Task SpawnArtifactAsync()
    {
        await RemoveArtifactAsync();
        var spawned = await _summon.SummonAsync(CastleSiegeRules.ArtifactNpc, (byte)ZoneId.Delos, 0,
            CastleSiegeRules.ArtifactX, CastleSiegeRules.ArtifactZ, 1, CastleSiegeRules.ArtifactFallbackY);
        _artifact = spawned.Count > 0 ? spawned[0] : null;
        if (_artifact == null)
            _logger.LogWarning("Castle siege artifact {Npc} could not be spawned", CastleSiegeRules.ArtifactNpc);
    }

    private async Task RemoveArtifactAsync()
    {
        if (_artifact is { IsAlive: true } artifact)
            await _lifecycle.DespawnAsync(artifact);
        _artifact = null;
    }

    private IEnumerable<UserSession> InDelos() =>
        _sessions.GetAll().Where(s => s.ZoneId == (byte)ZoneId.Delos).ToList();

    private async Task BroadcastToDelosAsync(Packet packet)
    {
        foreach (var session in InDelos())
            await session.Client.SendPacket(packet);
    }

    private Task AnnounceMinutesLeftAsync(int minutes) => minutes <= 1
        ? AnnounceAsync(TextAboutToBegin, "Castle Siege War is about to begin.")
        : _sessions.BroadcastToAll(NoticePacketWriter.Broadcast(
            (_gameData.GetServerResource(TextMinutesLeft) ?? "There are %d minutes remaining in the Siege Warfare.")
                .Replace("%d", minutes.ToString())));

    private Task AnnounceAsync(int resourceId, string fallback) =>
        _sessions.BroadcastToAll(NoticePacketWriter.Broadcast(_gameData.GetServerResource(resourceId) ?? fallback));
}
