using LibreKO.Common.Domain.Entities;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol.Writers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LibreKO.Game.World;

public interface IClanMatchService
{
    bool Running { get; }
    Task<string> StartAsync(string clanA, string clanB);
    Task StopAsync();
    Task OnPlayerDiedAsync(UserSession victim, UserSession? killer);
}

public sealed class ClanMatchService : BackgroundService, IClanMatchService
{
    public const byte Zone = (byte)ZoneId.PartyClan1;
    public const int MaxFighters = 8;
    public const int MatchSeconds = 1200;
    public const int SendHomeSeconds = 10;
    public const byte NoTeam = 0;
    public const byte TeamA = 1;
    public const byte TeamB = 2;
    private const int TickMilliseconds = 1000;
    private const float StartAX = 132f, StartAZ = 31f, StartBX = 131f, StartBZ = 191f;
    private const float HomeX = 0f, HomeZ = 0f;
    private const short Draw = 0;

    private readonly SessionManager _sessions;
    private readonly IZoneTransitionService _zones;
    private readonly ILogger<ClanMatchService> _logger;
    private readonly Dictionary<int, (short Hp, short Mp)> _shown = [];
    private KnightsEntity? _clanA, _clanB;
    private int _scoreA, _scoreB, _secondsLeft, _homeLeft;

    public ClanMatchService(SessionManager sessions, IZoneTransitionService zones, ILogger<ClanMatchService> logger)
    {
        _sessions = sessions;
        _zones = zones;
        _logger = logger;
        zones.ZoneAbilitySent += OnZoneAbilitySentAsync;
        zones.PlayerLeavingZone += (session, _) => LeaveAsync(session);
    }

    public bool Running => _clanA != null && _secondsLeft > 0;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(TickMilliseconds));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await TickAsync();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Clan match tick failed");
            }
        }
    }

    public async Task<string> StartAsync(string clanA, string clanB)
    {
        if (_clanA != null)
            return "A clan match is already running.";
        var a = ClanNamed(clanA);
        var b = ClanNamed(clanB);
        if (a == null || b == null || a.Id == b.Id)
            return "Name two different clans.";

        _clanA = a;
        _clanB = b;
        _scoreA = _scoreB = 0;
        _secondsLeft = MatchSeconds;
        _homeLeft = 0;
        _shown.Clear();
        int moved = 0;
        foreach (var (clan, team, x, z) in new[] { (a, TeamA, StartAX, StartAZ), (b, TeamB, StartBX, StartBZ) })
            foreach (var fighter in _sessions.GetAll().Where(s => s.KnightsId == clan.Id && s.Hp > 0).Take(MaxFighters).ToList())
            {
                fighter.MatchTeam = team;
                moved++;
                await _zones.ChangeZoneAsync(fighter, Zone, x, z);
            }
        _logger.LogInformation("Clan match {A} vs {B} started with {Count} fighters", a.Name, b.Name, moved);
        return $"Clan match {a.Name} vs {b.Name} started with {moved} fighters.";
    }

    public async Task StopAsync()
    {
        if (_clanA != null && _secondsLeft > 0)
            await EndAsync();
    }

    internal async Task TickAsync()
    {
        if (_clanA == null)
            return;
        if (_secondsLeft > 0)
        {
            await SendVitalsAsync();
            if (--_secondsLeft <= 0)
                await EndAsync();
            return;
        }
        if (--_homeLeft <= 0)
            await SendEveryoneHomeAsync();
    }

    public async Task OnPlayerDiedAsync(UserSession victim, UserSession? killer)
    {
        if (!Running || killer == null || victim.MatchTeam == NoTeam || killer.MatchTeam == NoTeam || victim.MatchTeam == killer.MatchTeam)
            return;
        if (killer.MatchTeam == TeamA) _scoreA++;
        else _scoreB++;
        await Task.CompletedTask;
    }

    private async Task EndAsync()
    {
        _secondsLeft = 0;
        _homeLeft = SendHomeSeconds;
        short winner = _scoreA == _scoreB ? Draw : _scoreA > _scoreB ? _clanA!.Id : _clanB!.Id;
        await BroadcastAsync(ClanMatchPacketWriter.Result(winner));
        _logger.LogInformation("Clan match ended {A}:{B}", _scoreA, _scoreB);
    }

    private async Task SendEveryoneHomeAsync()
    {
        var inZone = InZone();
        _clanA = _clanB = null;
        foreach (var session in inZone)
        {
            session.MatchTeam = NoTeam;
            await _zones.ChangeZoneAsync(session, (byte)ZoneId.Moradon, HomeX, HomeZ);
        }
    }

    private async Task OnZoneAbilitySentAsync(UserSession session)
    {
        if (session.ZoneId != Zone || _clanA == null)
            return;
        if (session.MatchTeam == NoTeam && !session.IsGM)
        {
            await _zones.ChangeZoneAsync(session, (byte)ZoneId.Moradon, HomeX, HomeZ);
            return;
        }
        await session.Client.SendPacket(ClanMatchPacketWriter.Pair(Zone, _clanA.Id, _clanA.MarkVersion, _clanA.Name,
            _clanB!.Id, _clanB.MarkVersion, _clanB.Name));
        await BroadcastAsync(RosterPacket());
    }

    private async Task LeaveAsync(UserSession session)
    {
        if (session.MatchTeam == NoTeam)
            return;
        session.MatchTeam = NoTeam;
        _shown.Remove(session.CharacterId);
        if (_clanA != null)
            await BroadcastAsync(RosterPacket());
    }

    private Packet RosterPacket() =>
        ClanMatchPacketWriter.Roster(Fighters().Select(s => new ClanMatchPacketWriter.Fighter(
            s.KnightsId, s.CharacterId, s.Name, s.MaxHp, s.Hp, s.Class, s.MaxMp, s.Mp, (byte)s.Nation)).ToList());

    private async Task SendVitalsAsync()
    {
        foreach (var fighter in Fighters())
        {
            if (_shown.TryGetValue(fighter.CharacterId, out var last) && last == (fighter.Hp, fighter.Mp))
                continue;
            _shown[fighter.CharacterId] = (fighter.Hp, fighter.Mp);
            await BroadcastAsync(ClanMatchPacketWriter.Vitals(fighter.CharacterId, fighter.MaxHp, fighter.Hp, fighter.MaxMp, fighter.Mp));
        }
    }

    private List<UserSession> Fighters() => InZone().Where(s => s.MatchTeam != NoTeam).ToList();

    private List<UserSession> InZone() => _sessions.GetAll().Where(s => s.ZoneId == Zone).ToList();

    private async Task BroadcastAsync(Packet packet)
    {
        foreach (var session in InZone())
            await session.Client.SendPacket(packet);
    }

    private KnightsEntity? ClanNamed(string name) =>
        _sessions.Knights.GetAll().FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
}
