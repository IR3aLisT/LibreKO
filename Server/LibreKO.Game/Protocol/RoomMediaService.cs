using LibreKO.Common.Domain.Entities;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Configuration;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LibreKO.Game.Protocol;

public interface IRoomMediaService
{
    Task HandleAsync(IClient client, Packet packet);
}

public sealed class RoomMediaService : IRoomMediaService
{
    public const int NoteLengthSlackMs = 500;
    public const int DecisecondsPerSecond = 10;

    private sealed class Upload(uint id, string roomKey, int size, byte[] waveform)
    {
        public uint Id { get; } = id;
        public string RoomKey { get; } = roomKey;
        public byte[] Waveform { get; } = waveform;
        public byte[] Data { get; } = new byte[size];
        public int Received { get; set; }
        public ushort NextIndex { get; set; }
    }

    private sealed class NoteQuota
    {
        public DateTime Last { get; set; } = DateTime.MinValue;
        public DateOnly Day { get; set; }
        public int Count { get; set; }
    }

    private readonly SessionManager _sessions;
    private readonly INationChatService _rooms;
    private readonly IChatRoomHistory _history;
    private readonly VoiceRoster _roster;
    private readonly IVoiceRelay _relay;
    private readonly IOptions<GameServerSettings> _settings;
    private readonly ILogger<RoomMediaService> _logger;
    private readonly TimeProvider _clock;
    private readonly object _gate = new();
    private readonly Dictionary<int, Upload> _uploads = [];
    private readonly Dictionary<int, NoteQuota> _quotas = [];
    private readonly HashSet<(string RoomKey, int CharacterId)> _silenced = [];
    private uint _nextUpload;

    public RoomMediaService(
        SessionManager sessions,
        INationChatService rooms,
        IChatRoomHistory history,
        VoiceRoster roster,
        IVoiceRelay relay,
        IOptions<GameServerSettings> settings,
        ILogger<RoomMediaService> logger,
        TimeProvider? clock = null)
    {
        _sessions = sessions;
        _rooms = rooms;
        _history = history;
        _roster = roster;
        _relay = relay;
        _settings = settings;
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
        _rooms.SeatReleased += OnSeatReleased;
    }

    private ChatRoomSettings Settings => _settings.Value.ChatRooms;

    public async Task HandleAsync(IClient client, Packet packet)
    {
        var session = _sessions.GetByClientId(client.Id);
        if (session == null || packet.RemainingBytes < 1)
            return;
        byte sub = packet.ReadByte();
        switch (sub)
        {
            case RoomMediaPacketWriter.History: await SendHistoryAsync(session, packet); break;
            case RoomMediaPacketWriter.NoteBegin: await BeginNoteAsync(session, packet); break;
            case RoomMediaPacketWriter.NoteChunk: await TakeChunkAsync(session, packet); break;
            case RoomMediaPacketWriter.NoteEnd: await EndNoteAsync(session, packet); break;
            case RoomMediaPacketWriter.NoteFetch: await FetchNoteAsync(session, packet); break;
            case RoomMediaPacketWriter.VoiceJoin: await JoinVoiceAsync(session); break;
            case RoomMediaPacketWriter.VoiceLeave: await LeaveVoiceAsync(session); break;
            case RoomMediaPacketWriter.VoiceState: await ChangeVoiceStateAsync(session, packet); break;
            case RoomMediaPacketWriter.VoiceFrame: await RelayTcpFrameAsync(session, packet); break;
            case RoomMediaPacketWriter.VoiceTransport: UseTransport(session, packet); break;
            case RoomMediaPacketWriter.VoiceRoster: await SendRosterAsync(session); break;
            case RoomMediaPacketWriter.VoiceSilence: await SilenceAsync(session, packet); break;
            default: _logger.LogDebug("Unhandled room media sub {Sub} from {Name}", sub, session.Name); break;
        }
    }

    private static bool Silenced(UserSession session) =>
        !session.IsGM && (session.IsMuted || session.ZoneId == (byte)ZoneId.Prison);

    private async Task SendHistoryAsync(UserSession session, Packet packet)
    {
        if (packet.RemainingBytes < 8) return;
        long before = packet.ReadLong();
        if (!_rooms.TryGetSeat(session.CharacterId, out var seat))
        {
            await session.Client.SendPacket(RoomMediaPacketWriter.HistoryPage([], false));
            return;
        }
        var rows = await _history.PageAsync(seat.Key, before, RoomMediaPacketWriter.HistoryPageMax + 1);
        bool more = rows.Count > RoomMediaPacketWriter.HistoryPageMax;
        var page = more ? rows.Skip(rows.Count - RoomMediaPacketWriter.HistoryPageMax).ToList() : rows;
        await session.Client.SendPacket(RoomMediaPacketWriter.HistoryPage(page, more));
    }

    private async Task BeginNoteAsync(UserSession session, Packet packet)
    {
        if (packet.RemainingBytes < 6) return;
        int durationDs = packet.ReadUShort();
        int size = (int)packet.ReadUInt();
        byte[] waveform = [];
        if (packet.RemainingBytes >= 1)
        {
            int samples = packet.ReadByte();
            if (samples > ChatRoomMessage.WaveformMaxSamples || samples > packet.RemainingBytes) return;
            waveform = packet.ReadBytes(samples);
        }
        byte result;
        uint uploadId = 0;
        var now = _clock.GetUtcNow().UtcDateTime;
        if (!_rooms.TryGetSeat(session.CharacterId, out var seat)) result = RoomMediaResults.NotInRoom;
        else if (Silenced(session)) result = RoomMediaResults.Silenced;
        else if (durationDs > Settings.NoteMaxSeconds * DecisecondsPerSecond) result = RoomMediaResults.TooLong;
        else if (size <= 0 || size > Settings.NoteMaxBytes) result = RoomMediaResults.TooBig;
        else
        {
            lock (_gate)
            {
                var quota = QuotaOf(session.CharacterId, now);
                if ((now - quota.Last).TotalSeconds < Settings.NoteMinIntervalSeconds) result = RoomMediaResults.TooSoon;
                else if (quota.Count >= Settings.NotesPerDay) result = RoomMediaResults.DailyLimit;
                else
                {
                    uploadId = ++_nextUpload;
                    _uploads[session.CharacterId] = new Upload(uploadId, seat.Key, size, waveform);
                    result = RoomMediaResults.Ok;
                }
            }
        }
        await session.Client.SendPacket(result == RoomMediaResults.Ok
            ? RoomMediaPacketWriter.UploadStarted(uploadId)
            : RoomMediaPacketWriter.Result(RoomMediaPacketWriter.NoteBegin, result));
    }

    private async Task TakeChunkAsync(UserSession session, Packet packet)
    {
        if (packet.RemainingBytes < 8) return;
        uint id = packet.ReadUInt();
        ushort index = packet.ReadUShort();
        int length = packet.ReadUShort();
        bool bad;
        lock (_gate)
        {
            bad = !_uploads.TryGetValue(session.CharacterId, out var upload) || upload.Id != id
                || index != upload.NextIndex || length > RoomMediaPacketWriter.ChunkBytes
                || length > packet.RemainingBytes || upload.Received + length > upload.Data.Length;
            if (!bad)
            {
                packet.ReadBytes(length).CopyTo(upload!.Data, upload.Received);
                upload.Received += length;
                upload.NextIndex++;
            }
            else _uploads.Remove(session.CharacterId);
        }
        if (bad)
            await session.Client.SendPacket(RoomMediaPacketWriter.Result(RoomMediaPacketWriter.NoteEnd, RoomMediaResults.BadUpload));
    }

    private async Task EndNoteAsync(UserSession session, Packet packet)
    {
        if (packet.RemainingBytes < 4) return;
        uint id = packet.ReadUInt();
        Upload? upload;
        lock (_gate)
        {
            if (!_uploads.Remove(session.CharacterId, out upload) || upload.Id != id) upload = null;
        }
        byte result = RoomMediaResults.Ok;
        OggOpusInfo info = default;
        if (upload == null || upload.Received != upload.Data.Length) result = RoomMediaResults.BadUpload;
        else if (!_rooms.TryGetSeat(session.CharacterId, out var seat) || seat.Key != upload.RoomKey) result = RoomMediaResults.NotInRoom;
        else if (!OggOpusInfo.TryRead(upload.Data, out info) || info.Channels != 1) result = RoomMediaResults.BadUpload;
        else if (info.DurationMs > Settings.NoteMaxSeconds * 1000 + NoteLengthSlackMs) result = RoomMediaResults.TooLong;
        if (result != RoomMediaResults.Ok)
        {
            await session.Client.SendPacket(RoomMediaPacketWriter.Result(RoomMediaPacketWriter.NoteEnd, result));
            return;
        }

        ChatRoomMessage note;
        try
        {
            note = await _history.AppendNoteAsync(upload!.RoomKey, session.CharacterId, session.Name, info.DurationMs, upload.Data, upload.Waveform);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Voice note from {Name} could not be stored", session.Name);
            await session.Client.SendPacket(RoomMediaPacketWriter.Result(RoomMediaPacketWriter.NoteEnd, RoomMediaResults.Failed));
            return;
        }
        lock (_gate)
        {
            var quota = QuotaOf(session.CharacterId, note.CreatedAt);
            quota.Last = note.CreatedAt;
            quota.Count++;
        }
        await session.Client.SendPacket(RoomMediaPacketWriter.Result(RoomMediaPacketWriter.NoteEnd, RoomMediaResults.Ok));
        if (_rooms.TryGetSeat(session.CharacterId, out var room))
            await SendToAsync(room.Members, RoomMediaPacketWriter.PostedRow(note));
    }

    private async Task FetchNoteAsync(UserSession session, Packet packet)
    {
        if (packet.RemainingBytes < 8) return;
        long noteId = packet.ReadLong();
        var note = await _history.FindNoteAsync(noteId);
        byte[]? file = null;
        if (note != null && _rooms.TryGetSeat(session.CharacterId, out var seat) && seat.Key == note.RoomKey)
            file = await _history.ReadNoteAsync(note);
        if (file == null)
        {
            await session.Client.SendPacket(RoomMediaPacketWriter.NoteFetchRefused(noteId, RoomMediaResults.NotFound));
            return;
        }
        await session.Client.SendPacket(RoomMediaPacketWriter.NoteHeader(noteId, file.Length));
        ushort index = 0;
        for (int offset = 0; offset < file.Length; offset += RoomMediaPacketWriter.ChunkBytes, index++)
        {
            int length = Math.Min(RoomMediaPacketWriter.ChunkBytes, file.Length - offset);
            await session.Client.SendPacket(RoomMediaPacketWriter.NoteChunkOf(noteId, index, file, offset, length));
        }
    }

    private async Task JoinVoiceAsync(UserSession session)
    {
        byte result;
        VoiceMember? member = null;
        List<VoiceMember> others = [];
        if (!_relay.Enabled) result = RoomMediaResults.VoiceOff;
        else if (!_rooms.TryGetSeat(session.CharacterId, out var seat)) result = RoomMediaResults.NotInRoom;
        else if (seat.Public) result = RoomMediaResults.PublicChannel;
        else
        {
            await LeaveOtherCallAsync(session.CharacterId, seat.Key);
            member = _roster.Join(session.CharacterId, seat.Key, !Silenced(session), Settings.Voice.MaxVoicePerRoom, out others);
            result = member == null ? RoomMediaResults.VoiceFull : RoomMediaResults.Ok;
            if (member != null)
                lock (_gate) member.Silenced = _silenced.Contains((seat.Key, session.CharacterId));
        }
        if (member == null)
        {
            await session.Client.SendPacket(RoomMediaPacketWriter.Result(RoomMediaPacketWriter.VoiceJoin, result));
            return;
        }
        var peers = others.Select(o => new RoomMediaPacketWriter.VoicePeer(o.CharacterId, o.WireFlags)).ToList();
        await session.Client.SendPacket(RoomMediaPacketWriter.Joined(_relay.UdpPort, member.SessionId, member.Key, member.WireFlags, peers));
        await SendToAsync(RoomAudience(member.RoomKey, session.CharacterId),
            RoomMediaPacketWriter.MemberEvent(session.CharacterId, RoomMediaPacketWriter.MemberJoined, member.WireFlags));
    }

    private async Task LeaveOtherCallAsync(int characterId, string roomKey)
    {
        if (_roster.Get(characterId) is not { } current || current.RoomKey == roomKey) return;
        if (_roster.Leave(characterId, out _) is not { } left) return;
        await SendToAsync(RoomAudience(left.RoomKey, characterId),
            RoomMediaPacketWriter.MemberEvent(characterId, RoomMediaPacketWriter.MemberLeft, 0));
    }

    private async Task SendRosterAsync(UserSession session)
    {
        var peers = _rooms.TryGetSeat(session.CharacterId, out var seat) && !seat.Public
            ? _roster.MembersOf(seat.Key).Select(m => new RoomMediaPacketWriter.VoicePeer(m.CharacterId, m.WireFlags)).ToList()
            : [];
        await session.Client.SendPacket(RoomMediaPacketWriter.Roster(peers));
    }

    private async Task SilenceAsync(UserSession session, Packet packet)
    {
        if (packet.RemainingBytes < 5) return;
        int targetId = packet.ReadInt();
        bool silence = packet.ReadByte() != 0;
        if (!_rooms.TryGetSeat(session.CharacterId, out var seat) || seat.Public || seat.OwnerId != session.CharacterId)
        {
            await session.Client.SendPacket(RoomMediaPacketWriter.Result(RoomMediaPacketWriter.VoiceSilence, RoomMediaResults.NotLeader));
            return;
        }
        if (targetId == session.CharacterId || !seat.Members.Contains(targetId)) return;
        bool changed;
        lock (_gate)
            changed = silence ? _silenced.Add((seat.Key, targetId)) : _silenced.Remove((seat.Key, targetId));
        if (!changed) return;
        string targetName = _sessions.GetByCharacterId(targetId)?.Name ?? "";
        await SendToAsync(_rooms.MembersOf(seat.Key), RoomMediaPacketWriter.SilenceNotice(targetId, silence, session.Name, targetName));
        var target = _roster.Get(targetId);
        if (target == null || target.RoomKey != seat.Key) return;
        target.Silenced = silence;
        await SendToAsync(_rooms.MembersOf(seat.Key),
            RoomMediaPacketWriter.MemberEvent(targetId, RoomMediaPacketWriter.MemberFlags, target.WireFlags));
    }

    private IEnumerable<int> RoomAudience(string roomKey, int except) =>
        _rooms.MembersOf(roomKey).Where(id => id != except);

    private async Task LeaveVoiceAsync(UserSession session)
    {
        var member = _roster.Leave(session.CharacterId, out _);
        await session.Client.SendPacket(RoomMediaPacketWriter.Result(RoomMediaPacketWriter.VoiceLeave,
            member == null ? RoomMediaResults.NotInRoom : RoomMediaResults.Ok));
        if (member != null)
            await SendToAsync(RoomAudience(member.RoomKey, session.CharacterId),
                RoomMediaPacketWriter.MemberEvent(session.CharacterId, RoomMediaPacketWriter.MemberLeft, 0));
    }

    private async Task ChangeVoiceStateAsync(UserSession session, Packet packet)
    {
        if (packet.RemainingBytes < 1) return;
        byte flags = (byte)(packet.ReadByte() & VoiceFlags.ClientSettable);
        var member = _roster.Get(session.CharacterId);
        if (member == null || member.Flags == flags) return;
        member.Flags = flags;
        await SendToAsync(RoomAudience(member.RoomKey, session.CharacterId),
            RoomMediaPacketWriter.MemberEvent(session.CharacterId, RoomMediaPacketWriter.MemberFlags, member.WireFlags));
    }

    private async Task RelayTcpFrameAsync(UserSession session, Packet packet)
    {
        if (packet.RemainingBytes < 4) return;
        ushort seq = packet.ReadUShort();
        int length = packet.ReadUShort();
        if (length > packet.RemainingBytes) return;
        var member = _roster.Get(session.CharacterId);
        if (member == null) return;
        var opus = packet.ReadBytes(length);
        if (_roster.AllowFrame(member, opus.Length, _clock.GetUtcNow().ToUnixTimeMilliseconds()))
            await _relay.ForwardAsync(member, seq, opus);
    }

    private void UseTransport(UserSession session, Packet packet)
    {
        if (packet.RemainingBytes < 1) return;
        bool tcp = packet.ReadByte() != 0;
        if (_roster.Get(session.CharacterId) is { } member)
            member.Tcp = tcp;
    }

    private void OnSeatReleased(int characterId, string roomKey)
    {
        lock (_gate)
        {
            _uploads.Remove(characterId);
            _silenced.Remove((roomKey, characterId));
        }
        var member = _roster.Leave(characterId, out _);
        if (member == null) return;
        _ = SendToAsync(RoomAudience(roomKey, characterId), RoomMediaPacketWriter.MemberEvent(characterId, RoomMediaPacketWriter.MemberLeft, 0));
    }

    private NoteQuota QuotaOf(int characterId, DateTime now)
    {
        if (!_quotas.TryGetValue(characterId, out var quota))
            _quotas[characterId] = quota = new NoteQuota();
        var today = DateOnly.FromDateTime(now);
        if (quota.Day != today)
        {
            quota.Day = today;
            quota.Count = 0;
        }
        return quota;
    }

    private async Task SendToAsync(IEnumerable<int> characterIds, Packet packet)
    {
        foreach (var id in characterIds)
        {
            var member = _sessions.GetByCharacterId(id);
            if (member == null) continue;
            try { await member.Client.SendPacket(packet); }
            catch (Exception ex) { _logger.LogDebug(ex, "Room media send failed for {Id}", id); }
        }
    }
}
