using System.Buffers.Binary;
using FluentAssertions;
using LibreKO.Common.Domain.Entities;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Common.Infrastructure.Persistence;
using LibreKO.Game.Configuration;
using LibreKO.Game.Protocol;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace LibreKO.Game.Tests;

public sealed class RoomMediaTests : IDisposable
{
    private sealed class Clock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }

    private readonly SessionManager _sessions = new();
    private readonly Dictionary<int, List<Packet>> _sent = [];
    private readonly Clock _clock = new(new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero));
    private readonly string _notes = Path.Combine(Path.GetTempPath(), "libreko-notes-" + Guid.NewGuid().ToString("N"));
    private readonly ServiceProvider _provider;
    private readonly ChatRoomHistory _history;
    private readonly NationChatService _rooms;
    private readonly RoomMediaService _media;
    private readonly VoiceRoster _roster = new();
    private readonly GameServerSettings _settings = new();

    public RoomMediaTests()
    {
        _settings.ChatRooms.NotesPath = _notes;
        var services = new ServiceCollection();
        var root = new InMemoryDatabaseRoot();
        string name = $"RoomMedia_{Guid.NewGuid():N}";
        services.AddDbContext<AppDbContext>(options => options
            .UseInMemoryDatabase(name, root)
            .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)));
        _provider = services.BuildServiceProvider();
        var options = Options.Create(_settings);
        _history = new ChatRoomHistory(_provider.GetRequiredService<IServiceScopeFactory>(), options,
            Substitute.For<ILogger<ChatRoomHistory>>(), _clock);
        _rooms = new NationChatService(_sessions, _history, Substitute.For<ILogger<NationChatService>>());
        var relay = new VoiceRelayService(_roster, _sessions, options, Substitute.For<ILogger<VoiceRelayService>>(), _clock);
        _media = new RoomMediaService(_sessions, _rooms, _history, _roster, relay, options,
            Substitute.For<ILogger<RoomMediaService>>(), _clock);
    }

    public void Dispose()
    {
        _provider.Dispose();
        if (Directory.Exists(_notes)) Directory.Delete(_notes, recursive: true);
    }

    private UserSession Player(int id, AccountNation nation = AccountNation.Karus)
    {
        var client = Substitute.For<IClient>();
        client.Id.Returns(Guid.NewGuid());
        var log = _sent[id] = [];
        client.SendPacket(Arg.Do<Packet>(p => log.Add(p)), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var session = _sessions.CreateSession(client, characterId: id, accountId: id * 10);
        session.Name = $"Player{id}";
        session.Nation = nation;
        session.ZoneId = 21;
        return session;
    }

    private static Packet Nation(byte sub)
    {
        var packet = new Packet(GameOpcodes.GS_NATION_CHAT);
        packet.WriteByte(NationChatPacketWriter.NationRooms);
        packet.WriteByte(sub);
        return packet;
    }

    private static Packet Media(byte sub)
    {
        var packet = new Packet(GameOpcodes.GS_ROOM_MEDIA);
        packet.WriteByte(sub);
        return packet;
    }

    private async Task ToRooms(UserSession who, Packet packet)
    {
        packet.ResetOffset();
        await _rooms.HandleAsync(who.Client, packet);
    }

    private async Task ToMedia(UserSession who, Packet packet)
    {
        packet.ResetOffset();
        await _media.HandleAsync(who.Client, packet);
    }

    private async Task<ushort> CreateRoom(UserSession owner)
    {
        var packet = Nation(NationChatPacketWriter.Create);
        packet.WriteString("Voice room");
        packet.WriteByte(0);
        packet.WriteUShort(10);
        await ToRooms(owner, packet);
        return NationChatService.PrivateRoomIdBase;
    }

    private async Task EnterRoom(UserSession who, ushort roomId)
    {
        var packet = Nation(NationChatPacketWriter.PrivateEnter);
        packet.WriteUShort(roomId);
        packet.WriteByte(0);
        await ToRooms(who, packet);
    }

    private async Task EnterPublic(UserSession who, ushort channel)
    {
        var packet = Nation(NationChatPacketWriter.PublicEnter);
        packet.WriteUShort(channel);
        await ToRooms(who, packet);
    }

    private List<Packet> MediaOf(int id, byte sub) =>
        _sent[id].Where(p => p.GetOpcode() == (byte)GameOpcodes.GS_ROOM_MEDIA && SubOf(p) == sub).ToList();

    private Packet LastMedia(int id, byte sub)
    {
        var packet = MediaOf(id, sub).Last();
        packet.ResetOffset();
        packet.ReadByte();
        return packet;
    }

    private static byte SubOf(Packet p)
    {
        p.ResetOffset();
        return p.ReadByte();
    }

    internal static byte[] OggOpus(int channels, int durationMs, int bodyBytes = 64)
    {
        const int preSkip = 312;
        var head = new byte[19];
        "OpusHead"u8.CopyTo(head);
        head[8] = 1;
        head[9] = (byte)channels;
        BinaryPrimitives.WriteUInt16LittleEndian(head.AsSpan(10), preSkip);
        BinaryPrimitives.WriteUInt32LittleEndian(head.AsSpan(12), 48000);
        var tags = "OpusTags"u8.ToArray().Concat(new byte[8]).ToArray();
        long granule = preSkip + (long)durationMs * 48;
        using var file = new MemoryStream();
        Page(file, 0x02, 0, head);
        Page(file, 0x00, 0, tags);
        Page(file, 0x04, granule, new byte[bodyBytes]);
        return file.ToArray();
    }

    private static void Page(Stream file, byte flags, long granule, byte[] body)
    {
        var header = new byte[27];
        "OggS"u8.CopyTo(header);
        header[5] = flags;
        BinaryPrimitives.WriteInt64LittleEndian(header.AsSpan(6), granule);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(14), 0x4B4F);
        var segments = new List<byte>();
        int left = body.Length;
        while (left >= 255) { segments.Add(255); left -= 255; }
        segments.Add((byte)left);
        header[26] = (byte)segments.Count;
        file.Write(header);
        file.Write(segments.ToArray());
        file.Write(body);
    }

    internal static readonly byte[] Wave = Enumerable.Range(0, ChatRoomMessage.WaveformMaxSamples).Select(i => (byte)(i * 4)).ToArray();

    private async Task<byte> Upload(UserSession who, byte[] file, int durationDs, int chunk = 1000)
    {
        var begin = Media(RoomMediaPacketWriter.NoteBegin);
        begin.WriteUShort((ushort)durationDs);
        begin.WriteUInt((uint)file.Length);
        begin.WriteByte((byte)Wave.Length);
        begin.WriteBytes(Wave);
        await ToMedia(who, begin);
        var started = LastMedia(who.CharacterId, RoomMediaPacketWriter.NoteBegin);
        byte result = started.ReadByte();
        if (result != RoomMediaResults.Ok) return result;
        uint uploadId = started.ReadUInt();
        ushort index = 0;
        for (int offset = 0; offset < file.Length; offset += chunk, index++)
        {
            int length = Math.Min(chunk, file.Length - offset);
            var part = Media(RoomMediaPacketWriter.NoteChunk);
            part.WriteUInt(uploadId);
            part.WriteUShort(index);
            part.WriteUShort((ushort)length);
            part.WriteBytes(file[offset..(offset + length)]);
            await ToMedia(who, part);
        }
        var end = Media(RoomMediaPacketWriter.NoteEnd);
        end.WriteUInt(uploadId);
        await ToMedia(who, end);
        return LastMedia(who.CharacterId, RoomMediaPacketWriter.NoteEnd).ReadByte();
    }

    [Fact]
    public async Task PublicChannelsRefuseLiveVoice()
    {
        var player = Player(1);
        await EnterPublic(player, 1);

        await ToMedia(player, Media(RoomMediaPacketWriter.VoiceJoin));

        LastMedia(1, RoomMediaPacketWriter.VoiceJoin).ReadByte().Should().Be(RoomMediaResults.PublicChannel);
    }

    [Fact]
    public async Task JoiningVoiceInAPrivateRoomListsThePeersAndTellsThem()
    {
        var owner = Player(1);
        var friend = Player(2);
        ushort roomId = await CreateRoom(owner);
        await EnterRoom(friend, roomId);

        await ToMedia(owner, Media(RoomMediaPacketWriter.VoiceJoin));
        await ToMedia(friend, Media(RoomMediaPacketWriter.VoiceJoin));

        var joined = LastMedia(2, RoomMediaPacketWriter.VoiceJoin);
        joined.ReadByte().Should().Be(RoomMediaResults.Ok);
        joined.ReadUShort().Should().Be(0, "the relay is not bound in this test, so the client falls back to TCP");
        joined.ReadUInt().Should().NotBe(0);
        joined.ReadByte().Should().Be(VoiceSeal.KeyLength);
        joined.ReadBytes(VoiceSeal.KeyLength);
        joined.ReadByte().Should().Be(0);
        joined.ReadUShort().Should().Be(1);
        joined.ReadInt().Should().Be(1);

        var told = LastMedia(1, RoomMediaPacketWriter.VoiceState);
        told.ReadInt().Should().Be(2);
        told.ReadByte().Should().Be(RoomMediaPacketWriter.MemberJoined);
    }

    [Fact]
    public async Task LeavingOrBeingKickedFromTheRoomDropsTheVoiceSeat()
    {
        var owner = Player(1);
        var friend = Player(2);
        var third = Player(3);
        ushort roomId = await CreateRoom(owner);
        await EnterRoom(friend, roomId);
        await EnterRoom(third, roomId);
        foreach (var who in new[] { owner, friend, third })
            await ToMedia(who, Media(RoomMediaPacketWriter.VoiceJoin));

        var kick = Nation(NationChatPacketWriter.Kick);
        kick.WriteUShort(roomId);
        kick.WriteInt(2);
        await ToRooms(owner, kick);
        await ToRooms(third, Nation(NationChatPacketWriter.PrivateLeave));
        await Task.Delay(50);

        var left = MediaOf(1, RoomMediaPacketWriter.VoiceState)
            .Select(p => { p.ResetOffset(); p.ReadByte(); return (Id: p.ReadInt(), Event: p.ReadByte()); })
            .Where(e => e.Event == RoomMediaPacketWriter.MemberLeft)
            .Select(e => e.Id).ToList();
        left.Should().BeEquivalentTo([2, 3]);
    }

    [Fact]
    public async Task MembersOutsideTheCallSeeWhoIsInItAndCanAskForTheRoster()
    {
        var owner = Player(1);
        var listener = Player(2);
        var outsider = Player(3);
        ushort roomId = await CreateRoom(owner);
        await EnterRoom(listener, roomId);
        await EnterRoom(outsider, roomId);
        await ToMedia(owner, Media(RoomMediaPacketWriter.VoiceJoin));
        await ToMedia(listener, Media(RoomMediaPacketWriter.VoiceJoin));
        var mute = Media(RoomMediaPacketWriter.VoiceState);
        mute.WriteByte(VoiceFlags.MicMuted);
        await ToMedia(listener, mute);

        await ToMedia(outsider, Media(RoomMediaPacketWriter.VoiceRoster));
        var roster = LastMedia(3, RoomMediaPacketWriter.VoiceRoster);
        int count = roster.ReadUShort();
        var inCall = Enumerable.Range(0, count).ToDictionary(_ => roster.ReadInt(), _ => roster.ReadByte());
        inCall.Should().BeEquivalentTo(new Dictionary<int, byte> { [1] = 0, [2] = VoiceFlags.MicMuted });

        await ToMedia(owner, Media(RoomMediaPacketWriter.VoiceLeave));
        var seen = MediaOf(3, RoomMediaPacketWriter.VoiceState)
            .Select(p => { p.ResetOffset(); p.ReadByte(); return (Id: p.ReadInt(), Event: p.ReadByte(), Flags: p.ReadByte()); })
            .ToList();
        seen.Should().Equal(
            (1, RoomMediaPacketWriter.MemberJoined, (byte)0),
            (2, RoomMediaPacketWriter.MemberJoined, (byte)0),
            (2, RoomMediaPacketWriter.MemberFlags, VoiceFlags.MicMuted),
            (1, RoomMediaPacketWriter.MemberLeft, (byte)0));

        var lonely = Player(4);
        await EnterPublic(lonely, 1);
        await ToMedia(lonely, Media(RoomMediaPacketWriter.VoiceRoster));
        LastMedia(4, RoomMediaPacketWriter.VoiceRoster).ReadUShort().Should().Be(0);
    }

    private async Task JoinOverTcp(UserSession who)
    {
        await ToMedia(who, Media(RoomMediaPacketWriter.VoiceJoin));
        var transport = Media(RoomMediaPacketWriter.VoiceTransport);
        transport.WriteByte(1);
        await ToMedia(who, transport);
    }

    private async Task Speak(UserSession who, ushort seq)
    {
        var frame = Media(RoomMediaPacketWriter.VoiceFrame);
        frame.WriteUShort(seq);
        frame.WriteUShort(2);
        frame.WriteBytes([1, 2]);
        await ToMedia(who, frame);
    }

    private static Packet Silence(int targetId, bool on)
    {
        var packet = Media(RoomMediaPacketWriter.VoiceSilence);
        packet.WriteInt(targetId);
        packet.WriteByte((byte)(on ? 1 : 0));
        return packet;
    }

    [Fact]
    public async Task TheRoomLeaderSilencesAMicrophoneThatStaysSilencedUntilLifted()
    {
        var owner = Player(1);
        var talker = Player(2);
        var listener = Player(3);
        ushort roomId = await CreateRoom(owner);
        await EnterRoom(talker, roomId);
        await EnterRoom(listener, roomId);
        await JoinOverTcp(talker);
        await JoinOverTcp(listener);

        await ToMedia(listener, Silence(2, true));
        LastMedia(3, RoomMediaPacketWriter.VoiceSilence).ReadByte().Should().Be(RoomMediaResults.NotLeader);

        await ToMedia(owner, Silence(2, true));
        var told = LastMedia(2, RoomMediaPacketWriter.VoiceState);
        told.ReadInt().Should().Be(2);
        told.ReadByte().Should().Be(RoomMediaPacketWriter.MemberFlags);
        told.ReadByte().Should().Be(VoiceFlags.Silenced);
        MediaOf(1, RoomMediaPacketWriter.VoiceState).Should().NotBeEmpty("the leader sees the change while outside the call");
        foreach (int member in new[] { 1, 2, 3 })
        {
            var notice = LastMedia(member, RoomMediaPacketWriter.VoiceSilence);
            notice.ReadByte().Should().Be(RoomMediaResults.Ok);
            notice.ReadInt().Should().Be(2);
            notice.ReadByte().Should().Be(1);
            notice.ReadString().Should().Be("Player1");
            notice.ReadString().Should().Be("Player2");
        }
        int notices = MediaOf(3, RoomMediaPacketWriter.VoiceSilence).Count;
        await ToMedia(owner, Silence(2, true));
        MediaOf(3, RoomMediaPacketWriter.VoiceSilence).Should().HaveCount(notices, "silencing twice changes nothing");

        await Speak(talker, 1);
        MediaOf(3, RoomMediaPacketWriter.VoiceFrame).Should().BeEmpty();

        await ToMedia(talker, Media(RoomMediaPacketWriter.VoiceLeave));
        await JoinOverTcp(talker);
        var rejoined = MediaOf(2, RoomMediaPacketWriter.VoiceJoin).Last();
        rejoined.ResetOffset();
        rejoined.ReadByte();
        rejoined.ReadByte().Should().Be(RoomMediaResults.Ok);
        rejoined.ReadUShort();
        rejoined.ReadUInt();
        rejoined.ReadBytes(rejoined.ReadByte());
        rejoined.ReadByte().Should().Be(VoiceFlags.Silenced);

        await ToMedia(owner, Silence(2, false));
        var lifted = LastMedia(3, RoomMediaPacketWriter.VoiceSilence);
        lifted.ReadByte().Should().Be(RoomMediaResults.Ok);
        lifted.ReadInt().Should().Be(2);
        lifted.ReadByte().Should().Be(0);
        await Speak(talker, 2);
        MediaOf(3, RoomMediaPacketWriter.VoiceFrame).Should().HaveCount(1);
    }

    [Fact]
    public async Task JoiningACallLeavesACallStillOpenInAnotherRoom()
    {
        var first = Player(1);
        var second = Player(2);
        var mover = Player(3);
        await CreateRoom(first);
        await CreateRoom(second);
        _rooms.TryGetSeat(1, out var oldRoom).Should().BeTrue();
        _rooms.TryGetSeat(2, out var newRoom).Should().BeTrue();
        await EnterRoom(mover, newRoom.RoomId);
        _roster.Join(3, oldRoom.Key, true, 0, out _);

        await ToMedia(mover, Media(RoomMediaPacketWriter.VoiceJoin));

        _roster.Get(3)!.RoomKey.Should().Be(newRoom.Key);
        _roster.CountIn(oldRoom.Key).Should().Be(0);
        var told = LastMedia(1, RoomMediaPacketWriter.VoiceState);
        told.ReadInt().Should().Be(3);
        told.ReadByte().Should().Be(RoomMediaPacketWriter.MemberLeft);
    }

    [Fact]
    public async Task MutedPlayersMayListenButAreMarkedAsUnableToSpeak()
    {
        var owner = Player(1);
        var muted = Player(2);
        muted.IsMuted = true;
        ushort roomId = await CreateRoom(owner);
        await EnterRoom(muted, roomId);

        await ToMedia(muted, Media(RoomMediaPacketWriter.VoiceJoin));

        var joined = LastMedia(2, RoomMediaPacketWriter.VoiceJoin);
        joined.ReadByte().Should().Be(RoomMediaResults.Ok);
        joined.ReadUShort();
        joined.ReadUInt();
        joined.ReadBytes(joined.ReadByte());
        joined.ReadByte().Should().Be(VoiceFlags.CannotSpeak);
    }

    [Fact]
    public async Task TcpFramesReachTheOtherVoiceMembersButNotTheSpeaker()
    {
        var owner = Player(1);
        var friend = Player(2);
        ushort roomId = await CreateRoom(owner);
        await EnterRoom(friend, roomId);
        foreach (var who in new[] { owner, friend })
        {
            await ToMedia(who, Media(RoomMediaPacketWriter.VoiceJoin));
            var transport = Media(RoomMediaPacketWriter.VoiceTransport);
            transport.WriteByte(1);
            await ToMedia(who, transport);
        }

        var frame = Media(RoomMediaPacketWriter.VoiceFrame);
        frame.WriteUShort(77);
        frame.WriteUShort(3);
        frame.WriteBytes([9, 8, 7]);
        await ToMedia(owner, frame);

        MediaOf(1, RoomMediaPacketWriter.VoiceFrame).Should().BeEmpty();
        var heard = LastMedia(2, RoomMediaPacketWriter.VoiceFrame);
        heard.ReadInt().Should().Be(1);
        heard.ReadUShort().Should().Be(77);
        heard.ReadBytes(heard.ReadUShort()).Should().Equal(9, 8, 7);
    }

    [Fact]
    public async Task AVoiceNoteIsStoredPostedToTheRoomAndFetchableByMembersOnly()
    {
        var owner = Player(1);
        var friend = Player(2);
        var stranger = Player(3);
        ushort roomId = await CreateRoom(owner);
        await EnterRoom(friend, roomId);
        await EnterPublic(stranger, 1);
        var file = OggOpus(channels: 1, durationMs: 4200, bodyBytes: 20000);

        (await Upload(owner, file, 42, chunk: RoomMediaPacketWriter.ChunkBytes)).Should().Be(RoomMediaResults.Ok);

        var posted = LastMedia(2, RoomMediaPacketWriter.Posted);
        long noteId = posted.ReadLong();
        posted.ReadByte().Should().Be((byte)ChatRoomMessageKind.VoiceNote);
        posted.ReadInt().Should().Be(1);
        posted.ReadString().Should().Be("Player1");
        posted.ReadUInt();
        posted.ReadUShort().Should().Be(42);
        posted.ReadUInt().Should().Be((uint)file.Length);
        posted.ReadBytes(posted.ReadByte()).Should().Equal(Wave);
        posted.RemainingBytes.Should().Be(0);
        Directory.EnumerateFiles(_notes, "*.opus", SearchOption.AllDirectories).Should().HaveCount(1);

        var fetch = Media(RoomMediaPacketWriter.NoteFetch);
        fetch.WriteLong(noteId);
        await ToMedia(friend, fetch);
        var header = LastMedia(2, RoomMediaPacketWriter.NoteFetch);
        header.ReadByte().Should().Be(RoomMediaResults.Ok);
        header.ReadLong().Should().Be(noteId);
        header.ReadUInt().Should().Be((uint)file.Length);
        var received = new List<byte>();
        foreach (var chunk in MediaOf(2, RoomMediaPacketWriter.NoteData))
        {
            chunk.ResetOffset();
            chunk.ReadByte();
            chunk.ReadLong().Should().Be(noteId);
            chunk.ReadUShort();
            received.AddRange(chunk.ReadBytes(chunk.ReadUShort()));
        }
        received.Should().Equal(file);

        await ToMedia(stranger, fetch);
        LastMedia(3, RoomMediaPacketWriter.NoteFetch).ReadByte().Should().Be(RoomMediaResults.NotFound);
    }

    [Fact]
    public async Task NotesOverTheLengthOrTooSoonAreRefused()
    {
        var owner = Player(1);
        await CreateRoom(owner);

        (await Upload(owner, OggOpus(1, 61_000), 600)).Should().Be(RoomMediaResults.TooLong);
        (await Upload(owner, OggOpus(2, 1_000), 10)).Should().Be(RoomMediaResults.BadUpload);
        (await Upload(owner, OggOpus(1, 1_000), 10)).Should().Be(RoomMediaResults.Ok);
        (await Upload(owner, OggOpus(1, 1_000), 10)).Should().Be(RoomMediaResults.TooSoon);
        _clock.Advance(TimeSpan.FromSeconds(_settings.ChatRooms.NoteMinIntervalSeconds));
        (await Upload(owner, OggOpus(1, 1_000), 10)).Should().Be(RoomMediaResults.Ok);
    }

    [Fact]
    public async Task HistoryInterleavesStoredTextAndNotesAndPages()
    {
        var owner = Player(1);
        var late = Player(2);
        ushort roomId = await CreateRoom(owner);
        for (int i = 0; i < RoomMediaPacketWriter.HistoryPageMax + 2; i++)
            await _rooms.SayAsync(owner, roomId, $"line {i}");
        (await Upload(owner, OggOpus(1, 2_000), 20)).Should().Be(RoomMediaResults.Ok);
        await EnterRoom(late, roomId);

        var ask = Media(RoomMediaPacketWriter.History);
        ask.WriteLong(0);
        await ToMedia(late, ask);

        var page = LastMedia(2, RoomMediaPacketWriter.History);
        int count = page.ReadByte();
        count.Should().Be(RoomMediaPacketWriter.HistoryPageMax);
        long firstId = 0;
        ChatRoomMessageKind lastKind = default;
        string lastText = "";
        for (int i = 0; i < count; i++)
        {
            long id = page.ReadLong();
            if (i == 0) firstId = id;
            lastKind = (ChatRoomMessageKind)page.ReadByte();
            page.ReadInt();
            page.ReadString();
            page.ReadUInt();
            if (lastKind == ChatRoomMessageKind.Text) lastText = page.ReadString();
            else
            {
                page.ReadUShort();
                page.ReadUInt();
                page.ReadBytes(page.ReadByte()).Should().Equal(Wave);
            }
        }
        lastKind.Should().Be(ChatRoomMessageKind.VoiceNote);
        page.ReadByte().Should().Be(1);

        var older = Media(RoomMediaPacketWriter.History);
        older.WriteLong(firstId);
        await ToMedia(late, older);
        var rest = LastMedia(2, RoomMediaPacketWriter.History);
        rest.ReadByte().Should().Be(3);
        rest.ReadLong();
        rest.ReadByte();
        rest.ReadInt();
        rest.ReadString();
        rest.ReadUInt();
        rest.ReadString().Should().Be("line 0");
    }

    [Fact]
    public async Task ThePruneRemovesOldRowsAndTheirFilesAndKeepsNewOnes()
    {
        var owner = Player(1);
        ushort roomId = await CreateRoom(owner);
        await _rooms.SayAsync(owner, roomId, "old line");
        (await Upload(owner, OggOpus(1, 1_000), 10)).Should().Be(RoomMediaResults.Ok);
        _clock.Advance(TimeSpan.FromDays(_settings.ChatRooms.HistoryDays) + TimeSpan.FromHours(1));
        await _rooms.SayAsync(owner, roomId, "new line");

        var pruned = await _history.PruneAsync();

        pruned.Should().Be(new ChatRoomPruneResult(2, 1));
        Directory.EnumerateFiles(_notes, "*.opus", SearchOption.AllDirectories).Should().BeEmpty();
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ChatRoomMessages.Select(m => m.Text).Should().BeEquivalentTo(["new line"]);
        (await _history.PruneAsync()).Should().Be(new ChatRoomPruneResult(0, 0));
    }

    [Fact]
    public async Task APrivateRoomKeepsOneKeyAndARecreatedRoomGetsANewOne()
    {
        var owner = Player(1);
        ushort roomId = await CreateRoom(owner);
        _rooms.TryGetSeat(1, out var first).Should().BeTrue();
        _rooms.TryGetSeat(1, out var again).Should().BeTrue();
        again.Key.Should().Be(first.Key);
        first.Public.Should().BeFalse();

        await ToRooms(owner, Nation(NationChatPacketWriter.PrivateLeave));
        await CreateRoom(owner);
        _rooms.TryGetSeat(1, out var recreated).Should().BeTrue();
        recreated.RoomId.Should().NotBe(roomId);
        recreated.Key.Should().NotBe(first.Key);

        var pub = Player(2, AccountNation.ElMorad);
        await EnterPublic(pub, 3);
        _rooms.TryGetSeat(2, out var channel).Should().BeTrue();
        channel.Key.Should().Be("pub:2:3");
    }
}
