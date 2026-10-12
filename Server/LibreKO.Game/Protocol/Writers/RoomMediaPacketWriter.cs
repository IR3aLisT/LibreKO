using LibreKO.Common.Domain.Entities;
using LibreKO.Common.Infrastructure.Network;

namespace LibreKO.Game.Protocol.Writers;

public static class RoomMediaPacketWriter
{
    public const byte History = 1;
    public const byte Posted = 2;
    public const byte NoteBegin = 10;
    public const byte NoteChunk = 11;
    public const byte NoteEnd = 12;
    public const byte NoteFetch = 13;
    public const byte NoteData = 14;
    public const byte VoiceJoin = 20;
    public const byte VoiceLeave = 21;
    public const byte VoiceState = 22;
    public const byte VoiceFrame = 23;
    public const byte VoiceTransport = 24;
    public const byte VoiceRoster = 25;
    public const byte VoiceSilence = 26;

    public const byte MemberJoined = 1;
    public const byte MemberLeft = 2;
    public const byte MemberFlags = 3;

    public const int HistoryPageMax = 50;
    public const int ChunkBytes = 8192;

    public readonly record struct VoicePeer(int CharacterId, byte Flags);

    public static Packet HistoryPage(IReadOnlyList<ChatRoomMessage> rows, bool more)
    {
        var packet = Media(History);
        packet.WriteByte((byte)rows.Count);
        foreach (var row in rows)
            WriteRow(packet, row);
        packet.WriteByte((byte)(more ? 1 : 0));
        return packet;
    }

    public static Packet PostedRow(ChatRoomMessage row)
    {
        var packet = Media(Posted);
        WriteRow(packet, row);
        return packet;
    }

    public static Packet Result(byte sub, byte result)
    {
        var packet = Media(sub);
        packet.WriteByte(result);
        return packet;
    }

    public static Packet UploadStarted(uint uploadId)
    {
        var packet = Result(NoteBegin, RoomMediaResults.Ok);
        packet.WriteUInt(uploadId);
        return packet;
    }

    public static Packet NoteHeader(long noteId, int size)
    {
        var packet = Result(NoteFetch, RoomMediaResults.Ok);
        packet.WriteLong(noteId);
        packet.WriteUInt((uint)size);
        return packet;
    }

    public static Packet NoteFetchRefused(long noteId, byte result)
    {
        var packet = Result(NoteFetch, result);
        packet.WriteLong(noteId);
        return packet;
    }

    public static Packet NoteChunkOf(long noteId, ushort index, byte[] file, int offset, int length)
    {
        var packet = Media(NoteData);
        packet.WriteLong(noteId);
        packet.WriteUShort(index);
        packet.WriteUShort((ushort)length);
        packet.WriteBytes(file[offset..(offset + length)]);
        return packet;
    }

    public static Packet Joined(ushort udpPort, uint sessionId, byte[] key, byte selfFlags, IReadOnlyList<VoicePeer> peers)
    {
        var packet = Result(VoiceJoin, RoomMediaResults.Ok);
        packet.WriteUShort(udpPort);
        packet.WriteUInt(sessionId);
        packet.WriteByte((byte)key.Length);
        packet.WriteBytes(key);
        packet.WriteByte(selfFlags);
        packet.WriteUShort((ushort)peers.Count);
        foreach (var peer in peers)
        {
            packet.WriteInt(peer.CharacterId);
            packet.WriteByte(peer.Flags);
        }
        return packet;
    }

    public static Packet Roster(IReadOnlyList<VoicePeer> peers)
    {
        var packet = Media(VoiceRoster);
        packet.WriteUShort((ushort)peers.Count);
        foreach (var peer in peers)
        {
            packet.WriteInt(peer.CharacterId);
            packet.WriteByte(peer.Flags);
        }
        return packet;
    }

    public static Packet MemberEvent(int characterId, byte memberEvent, byte flags)
    {
        var packet = Media(VoiceState);
        packet.WriteInt(characterId);
        packet.WriteByte(memberEvent);
        packet.WriteByte(flags);
        return packet;
    }

    public static Packet SilenceNotice(int targetId, bool silenced, string leaderName, string targetName)
    {
        var packet = Result(VoiceSilence, RoomMediaResults.Ok);
        packet.WriteInt(targetId);
        packet.WriteByte((byte)(silenced ? 1 : 0));
        packet.WriteString(leaderName);
        packet.WriteString(targetName);
        return packet;
    }

    public static Packet Frame(int speakerId, ushort seq, ReadOnlySpan<byte> opus)
    {
        var packet = Media(VoiceFrame);
        packet.WriteInt(speakerId);
        packet.WriteUShort(seq);
        packet.WriteUShort((ushort)opus.Length);
        packet.WriteBytes(opus.ToArray());
        return packet;
    }

    public static uint UnixSeconds(DateTime utc) => (uint)new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeSeconds();

    public static ushort Deciseconds(int ms) => (ushort)Math.Clamp((ms + 50) / 100, 0, ushort.MaxValue);

    private static void WriteRow(Packet packet, ChatRoomMessage row)
    {
        packet.WriteLong(row.Id);
        packet.WriteByte((byte)row.Kind);
        packet.WriteInt(row.SenderCharacterId);
        packet.WriteString(row.SenderName);
        packet.WriteUInt(UnixSeconds(row.CreatedAt));
        if (row.Kind == ChatRoomMessageKind.Text)
            packet.WriteString(row.Text ?? "");
        else
        {
            packet.WriteUShort(Deciseconds(row.NoteDurationMs));
            packet.WriteUInt((uint)row.NoteSizeBytes);
            var waveform = row.NoteWaveform ?? [];
            packet.WriteByte((byte)waveform.Length);
            packet.WriteBytes(waveform);
        }
    }

    private static Packet Media(byte sub)
    {
        var packet = new Packet(GameOpcodes.GS_ROOM_MEDIA);
        packet.WriteByte(sub);
        return packet;
    }
}

public static class RoomMediaResults
{
    public const byte Ok = 0;
    public const byte NotInRoom = 1;
    public const byte PublicChannel = 2;
    public const byte VoiceOff = 3;
    public const byte VoiceFull = 4;
    public const byte Silenced = 5;
    public const byte TooLong = 6;
    public const byte TooBig = 7;
    public const byte TooSoon = 8;
    public const byte DailyLimit = 9;
    public const byte BadUpload = 10;
    public const byte NotFound = 11;
    public const byte Busy = 12;
    public const byte Failed = 13;
    public const byte NotLeader = 14;
}
