using System;
using System.Collections.Generic;
using LibreKO.Domain;

namespace LibreKO.Network;

public readonly record struct VoicePeer(int CharacterId, byte Flags);

public sealed record VoiceSession(ushort UdpPort, uint SessionId, byte[] Key, byte SelfFlags, IReadOnlyList<VoicePeer> Peers);

public static class RoomMediaReplies
{
    public const int PeersMax = 200;
    public const int HistoryRowsMax = 50;
    private static readonly DateTime Epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static RoomPost? ReadRow(Packet p)
    {
        if (p.RemainingBytes < 15) return null;
        long id = p.ReadLong();
        var kind = (RoomPostKind)p.ReadByte();
        int sender = p.ReadInt();
        string name = p.ReadString();
        if (p.RemainingBytes < 4) return null;
        var time = Epoch.AddSeconds(p.ReadUInt()).ToLocalTime();
        if (kind == RoomPostKind.Text)
        {
            if (p.RemainingBytes < 2) return null;
            return new RoomPost(id, kind, sender, name, time, p.ReadString(), 0, 0);
        }
        if (kind != RoomPostKind.VoiceNote || p.RemainingBytes < 6) return null;
        int duration = p.ReadUShort();
        int size = (int)p.ReadUInt();
        byte[] waveform = [];
        if (p.RemainingBytes >= 1)
        {
            int samples = p.ReadByte();
            if (samples > p.RemainingBytes) return null;
            waveform = p.ReadBytes(samples);
        }
        return new RoomPost(id, kind, sender, name, time, "", duration, size) { Waveform = waveform };
    }

    public static (List<RoomPost> Rows, bool More) ReadHistory(Packet p)
    {
        var rows = new List<RoomPost>();
        if (p.RemainingBytes < 1) return (rows, false);
        int count = Math.Min((int)p.ReadByte(), HistoryRowsMax);
        for (int i = 0; i < count; i++)
        {
            if (ReadRow(p) is not { } row) return (rows, false);
            rows.Add(row);
        }
        bool more = p.RemainingBytes >= 1 && p.ReadByte() != 0;
        return (rows, more);
    }

    public readonly record struct NoteHeader(byte Result, long NoteId, int Size);

    public readonly record struct NoteChunk(long NoteId, ushort Index, byte[] Data);

    public readonly record struct VoiceChange(int CharacterId, byte Change, byte Flags);

    public readonly record struct VoiceFrame(int Speaker, ushort Seq, byte[] Opus);

    public readonly record struct SilenceNotice(int TargetId, bool Silenced, string Leader, string Target)
    {
        public string Text => Silenced ? $"{Leader} silenced {Target}'s microphone." : $"{Leader} let {Target} speak again.";
    }

    public static NoteHeader? ReadNoteHeader(Packet p)
    {
        if (p.RemainingBytes < 9) return null;
        byte result = p.ReadByte();
        long id = p.ReadLong();
        int size = result == RoomMediaResults.Ok && p.RemainingBytes >= 4 ? (int)p.ReadUInt() : 0;
        return new NoteHeader(result, id, size);
    }

    public static NoteChunk? ReadNoteChunk(Packet p)
    {
        if (p.RemainingBytes < 12) return null;
        long id = p.ReadLong();
        ushort index = p.ReadUShort();
        int length = p.ReadUShort();
        if (length > p.RemainingBytes) return null;
        return new NoteChunk(id, index, p.ReadBytes(length));
    }

    public static VoiceChange? ReadVoiceChange(Packet p)
    {
        if (p.RemainingBytes < 6) return null;
        return new VoiceChange(p.ReadInt(), p.ReadByte(), p.ReadByte());
    }

    public static VoiceFrame? ReadVoiceFrame(Packet p)
    {
        if (p.RemainingBytes < 8) return null;
        int speaker = p.ReadInt();
        ushort seq = p.ReadUShort();
        int length = p.ReadUShort();
        if (length == 0 || length > p.RemainingBytes) return null;
        return new VoiceFrame(speaker, seq, p.ReadBytes(length));
    }

    public static SilenceNotice? ReadSilenceNotice(Packet p)
    {
        if (p.RemainingBytes < 1 || p.ReadByte() != RoomMediaResults.Ok || p.RemainingBytes < 9) return null;
        int target = p.ReadInt();
        bool silenced = p.ReadByte() != 0;
        string leader = p.ReadString();
        if (p.RemainingBytes < 2) return null;
        return new SilenceNotice(target, silenced, leader, p.ReadString());
    }

    public static List<VoicePeer> ReadRoster(Packet p)
    {
        var peers = new List<VoicePeer>();
        if (p.RemainingBytes < 2) return peers;
        int count = Math.Min((int)p.ReadUShort(), PeersMax);
        for (int i = 0; i < count && p.RemainingBytes >= 5; i++)
            peers.Add(new VoicePeer(p.ReadInt(), p.ReadByte()));
        return peers;
    }

    public static VoiceSession? ReadJoined(Packet p)
    {
        if (p.RemainingBytes < 7) return null;
        ushort port = p.ReadUShort();
        uint session = p.ReadUInt();
        int keyLength = p.ReadByte();
        if (keyLength != VoiceSeal.KeyLength || p.RemainingBytes < keyLength + 3) return null;
        byte[] key = p.ReadBytes(keyLength);
        byte self = p.ReadByte();
        int count = Math.Min((int)p.ReadUShort(), PeersMax);
        var peers = new List<VoicePeer>(count);
        for (int i = 0; i < count && p.RemainingBytes >= 5; i++)
            peers.Add(new VoicePeer(p.ReadInt(), p.ReadByte()));
        return new VoiceSession(port, session, key, self, peers);
    }
}

public partial class Net
{
    public RoomTimeline RoomTimeline { get; } = new();
    public VoiceSession? Voice { get; private set; }
    public Dictionary<int, byte> VoicePeers { get; } = new();
    public string ServerHost => _host;
    public bool ChatRoomPanelShown { get; set; }
    public bool ChatRoomMembersShown { get; set; } = true;

    public event Action? RoomTimelineEvent;
    public event Action<RoomPost>? RoomPostedEvent;
    public event Action<byte, byte>? RoomMediaResultEvent;
    public event Action<long, byte[]>? VoiceNoteFetchedEvent;
    public event Action<long>? VoiceNoteMissingEvent;
    public event Action<VoiceSession>? VoiceJoinedEvent;
    public event Action? VoiceLeftEvent;
    public event Action<int, byte, byte>? VoicePeerEvent;
    public event Action<int, ushort, byte[]>? VoiceTcpFrameEvent;
    public event Action? VoiceRosterEvent;
    public event Action<RoomMediaReplies.SilenceNotice>? VoiceSilenceEvent;

    private byte[]? _pendingNote;
    private readonly VoiceNoteDownloads _noteDownloads = new();

    private void HandleRoomMedia(Packet p)
    {
        if (p.RemainingBytes < 1) return;
        byte sub = p.ReadByte();
        switch (sub)
        {
            case RoomMediaSubs.History:
            {
                var (rows, more) = RoomMediaReplies.ReadHistory(p);
                RoomTimeline.MergeHistory(rows, more);
                RoomTimelineEvent?.Invoke();
                return;
            }
            case RoomMediaSubs.Posted:
                if (RoomMediaReplies.ReadRow(p) is { } posted)
                {
                    RoomTimeline.Add(posted);
                    RoomPostedEvent?.Invoke(posted);
                }
                return;
            case RoomMediaSubs.NoteBegin:
            {
                byte result = ReadMediaResult(p);
                if (result == RoomMediaResults.Ok && p.RemainingBytes >= 4 && _pendingNote != null) SendNoteChunks(p.ReadUInt(), _pendingNote);
                else RoomMediaResultEvent?.Invoke(sub, result);
                if (result != RoomMediaResults.Ok) _pendingNote = null;
                return;
            }
            case RoomMediaSubs.NoteEnd:
                _pendingNote = null;
                RoomMediaResultEvent?.Invoke(sub, ReadMediaResult(p));
                return;
            case RoomMediaSubs.NoteFetch:
                ReadNoteHeader(p);
                return;
            case RoomMediaSubs.NoteData:
                ReadNoteChunk(p);
                return;
            case RoomMediaSubs.VoiceJoin:
            {
                byte result = ReadMediaResult(p);
                if (result == RoomMediaResults.Ok && RoomMediaReplies.ReadJoined(p) is { } session)
                {
                    Voice = session;
                    VoicePeers.Clear();
                    foreach (var peer in session.Peers) VoicePeers[peer.CharacterId] = peer.Flags;
                    VoiceJoinedEvent?.Invoke(session);
                }
                else RoomMediaResultEvent?.Invoke(sub, result);
                return;
            }
            case RoomMediaSubs.VoiceLeave:
                ReadMediaResult(p);
                DropVoice();
                return;
            case RoomMediaSubs.VoiceState:
                if (RoomMediaReplies.ReadVoiceChange(p) is not { } change) return;
                if (change.CharacterId == MyCharId)
                {
                    if (Voice != null) Voice = Voice with { SelfFlags = change.Flags };
                }
                else if (change.Change == RoomMediaSubs.MemberLeft) VoicePeers.Remove(change.CharacterId);
                else VoicePeers[change.CharacterId] = change.Flags;
                VoicePeerEvent?.Invoke(change.CharacterId, change.Change, change.Flags);
                return;
            case RoomMediaSubs.VoiceRoster:
                VoicePeers.Clear();
                foreach (var peer in RoomMediaReplies.ReadRoster(p))
                    if (peer.CharacterId != MyCharId) VoicePeers[peer.CharacterId] = peer.Flags;
                VoiceRosterEvent?.Invoke();
                return;
            case RoomMediaSubs.VoiceSilence:
                if (RoomMediaReplies.ReadSilenceNotice(p) is { } silence) VoiceSilenceEvent?.Invoke(silence);
                return;
            case RoomMediaSubs.VoiceFrame:
                if (RoomMediaReplies.ReadVoiceFrame(p) is { } frame)
                    VoiceTcpFrameEvent?.Invoke(frame.Speaker, frame.Seq, frame.Opus);
                return;
        }
    }

    private static byte ReadMediaResult(Packet p) => p.RemainingBytes >= 1 ? p.ReadByte() : RoomMediaResults.Failed;

    private void ReadNoteHeader(Packet p)
    {
        if (RoomMediaReplies.ReadNoteHeader(p) is not { } header) return;
        if (header.Result != RoomMediaResults.Ok || !_noteDownloads.Start(header.NoteId, header.Size))
            VoiceNoteMissingEvent?.Invoke(header.NoteId);
    }

    private void ReadNoteChunk(Packet p)
    {
        if (RoomMediaReplies.ReadNoteChunk(p) is { } chunk && _noteDownloads.Append(chunk.NoteId, chunk.Data) is { } file)
            VoiceNoteFetchedEvent?.Invoke(chunk.NoteId, file);
    }

    public void SendRoomHistory(long beforeId)
    {
        if (!NationChat.InRoom) return;
        var p = MediaPacket(RoomMediaSubs.History);
        p.WriteLong(beforeId);
        _conn.Send(p);
    }

    public float NoteDownloadProgress(long noteId) => _noteDownloads.Progress(noteId);

    public bool SendVoiceNote(byte[] file, double seconds, byte[] waveform)
    {
        if (!NationChat.InRoom || _pendingNote != null || file.Length == 0 || file.Length > VoiceNoteRules.MaxBytes) return false;
        _pendingNote = file;
        var p = MediaPacket(RoomMediaSubs.NoteBegin);
        p.WriteUShort((ushort)Math.Clamp((int)Math.Round(seconds * VoiceNoteRules.DecisecondsPerSecond), 0, ushort.MaxValue));
        p.WriteUInt((uint)file.Length);
        int samples = Math.Min(waveform.Length, VoiceWaveform.Samples);
        p.WriteByte((byte)samples);
        p.WriteBytes(waveform[..samples]);
        _conn.Send(p);
        return true;
    }

    private void SendNoteChunks(uint uploadId, byte[] file)
    {
        ushort index = 0;
        for (int offset = 0; offset < file.Length; offset += VoiceNoteRules.ChunkBytes, index++)
        {
            int length = Math.Min(VoiceNoteRules.ChunkBytes, file.Length - offset);
            var chunk = MediaPacket(RoomMediaSubs.NoteChunk);
            chunk.WriteUInt(uploadId);
            chunk.WriteUShort(index);
            chunk.WriteUShort((ushort)length);
            chunk.WriteBytes(file[offset..(offset + length)]);
            _conn.Send(chunk);
        }
        var end = MediaPacket(RoomMediaSubs.NoteEnd);
        end.WriteUInt(uploadId);
        _conn.Send(end);
    }

    public void SendVoiceNoteFetch(long noteId)
    {
        if (_noteDownloads.Pending(noteId)) return;
        var p = MediaPacket(RoomMediaSubs.NoteFetch);
        p.WriteLong(noteId);
        _conn.Send(p);
    }

    public void SendVoiceJoin() => _conn.Send(MediaPacket(RoomMediaSubs.VoiceJoin));

    public void SendVoiceSilence(int characterId, bool silence)
    {
        var p = MediaPacket(RoomMediaSubs.VoiceSilence);
        p.WriteInt(characterId);
        p.WriteByte((byte)(silence ? 1 : 0));
        _conn.Send(p);
    }

    public void SendVoiceRoster()
    {
        if (NationChat.InPrivate) _conn.Send(MediaPacket(RoomMediaSubs.VoiceRoster));
    }

    public void SendVoiceLeave()
    {
        if (Voice == null) return;
        _conn.Send(MediaPacket(RoomMediaSubs.VoiceLeave));
        DropVoice();
    }

    public void SendVoiceState(byte flags)
    {
        if (Voice == null) return;
        var p = MediaPacket(RoomMediaSubs.VoiceState);
        p.WriteByte(flags);
        _conn.Send(p);
    }

    public void SendVoiceFrame(ushort seq, ReadOnlySpan<byte> opus)
    {
        if (Voice == null || opus.Length == 0) return;
        var p = MediaPacket(RoomMediaSubs.VoiceFrame);
        p.WriteUShort(seq);
        p.WriteUShort((ushort)opus.Length);
        p.WriteBytes(opus.ToArray());
        _conn.Send(p);
    }

    public void SendVoiceTransport(bool tcp)
    {
        if (Voice == null) return;
        var p = MediaPacket(RoomMediaSubs.VoiceTransport);
        p.WriteByte((byte)(tcp ? 1 : 0));
        _conn.Send(p);
    }

    internal void PreviewVoice(VoiceSession? session, IReadOnlyList<VoicePeer>? others = null)
    {
        Voice = session;
        VoicePeers.Clear();
        foreach (var peer in session?.Peers ?? others ?? []) VoicePeers[peer.CharacterId] = peer.Flags;
    }

    internal void LeftChatRoom()
    {
        ChatRoomPanelShown = false;
        RoomTimeline.Clear();
        _pendingNote = null;
        _noteDownloads.Clear();
        DropVoice();
        VoicePeers.Clear();
        RoomTimelineEvent?.Invoke();
    }

    private void DropVoice()
    {
        if (Voice == null) return;
        Voice = null;
        VoiceLeftEvent?.Invoke();
    }

    private void ResetRoomMedia()
    {
        RoomTimeline.Clear();
        _pendingNote = null;
        _noteDownloads.Clear();
        DropVoice();
        VoicePeers.Clear();
    }

    private static Packet MediaPacket(byte sub)
    {
        var p = new Packet(GameOpcodes.GS_ROOM_MEDIA);
        p.WriteByte(sub);
        return p;
    }
}
