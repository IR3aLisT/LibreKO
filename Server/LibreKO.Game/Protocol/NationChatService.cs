using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;
using Microsoft.Extensions.Logging;

namespace LibreKO.Game.Protocol;

public interface INationChatService
{
    Task HandleAsync(IClient client, Packet packet);
    Task SayAsync(UserSession session, ushort roomId, string text);
    Task LeaveAllAsync(UserSession session);
}

public sealed class NationChatService(
    SessionManager sessionManager,
    ILogger<NationChatService> logger) : INationChatService
{
    public const int PublicCapacity = 500;
    public const int TitleMaxLength = 30;
    public const int PasswordMaxLength = 8;
    public const int PrivateListMax = 500;
    public const ushort PrivateRoomIdBase = 1000;
    public static readonly ushort[] RoomSizes = [10, 30, 50, 100, 150, 200];
    public static readonly string[] PublicChannelNames = ["General", "Trade", "Party", "Help"];

    private sealed class PublicChannel(ushort id, string name)
    {
        public ushort Id { get; } = id;
        public string Name { get; } = name;
        public Dictionary<AccountNation, HashSet<int>> Members { get; } = [];

        public HashSet<int> Of(AccountNation nation) =>
            Members.TryGetValue(nation, out var set) ? set : Members[nation] = [];
    }

    private sealed class PrivateRoom
    {
        public ushort Id { get; init; }
        public string Name { get; set; } = "";
        public AccountNation Nation { get; init; }
        public string Password { get; set; } = "";
        public ushort MaxMembers { get; set; }
        public int OwnerId { get; set; }
        public List<int> Members { get; } = [];

        public bool Locked => Password.Length > 0;
    }

    private readonly record struct Seat(bool Public, ushort RoomId);

    private readonly object _gate = new();
    private readonly List<PublicChannel> _channels = PublicChannelNames
        .Select((name, i) => new PublicChannel((ushort)(i + 1), name)).ToList();
    private readonly Dictionary<ushort, PrivateRoom> _rooms = [];
    private readonly Dictionary<int, Seat> _seats = [];
    private ushort _nextRoomId = PrivateRoomIdBase;

    public async Task HandleAsync(IClient client, Packet packet)
    {
        var session = sessionManager.GetByClientId(client.Id);
        if (session == null || packet.RemainingBytes < 2)
            return;
        if (packet.ReadByte() != NationChatPacketWriter.NationRooms)
            return;

        byte sub = packet.ReadByte();
        switch (sub)
        {
            case NationChatPacketWriter.PublicList: await SendChannelsAsync(session); break;
            case NationChatPacketWriter.PublicEnter: await EnterPublicAsync(session, packet); break;
            case NationChatPacketWriter.PublicLeave: await LeavePublicAsync(session); break;
            case NationChatPacketWriter.PrivateList: await SendRoomsAsync(session); break;
            case NationChatPacketWriter.Create: await CreateAsync(session, packet); break;
            case NationChatPacketWriter.PrivateEnter: await EnterPrivateAsync(session, packet); break;
            case NationChatPacketWriter.PrivateLeave: await LeavePrivateAsync(session); break;
            case NationChatPacketWriter.Search: await SearchAsync(session, packet); break;
            case NationChatPacketWriter.Options: await ChangeOptionsAsync(session, packet); break;
            case NationChatPacketWriter.HandOver: await HandOverAsync(session, packet); break;
            case NationChatPacketWriter.Kick: await KickAsync(session, packet); break;
            case NationChatPacketWriter.Members: await SendMembersAsync(session, packet); break;
            default: logger.LogDebug("Unhandled nation chat sub {Sub} from {Name}", sub, session.Name); break;
        }
    }

    public async Task SayAsync(UserSession session, ushort roomId, string text)
    {
        List<int> audience;
        lock (_gate)
        {
            if (!_seats.TryGetValue(session.CharacterId, out var seat) || seat.RoomId != roomId)
                return;
            audience = seat.Public
                ? [.. _channels.First(c => c.Id == roomId).Of(session.Nation)]
                : [.. _rooms[roomId].Members];
        }
        await SendToAsync(audience, NationChatPacketWriter.Say(session.CharacterId, session.Name, text, session.ZoneId));
    }

    public async Task LeaveAllAsync(UserSession session)
    {
        Seat seat;
        lock (_gate)
        {
            if (!_seats.TryGetValue(session.CharacterId, out seat))
                return;
        }
        if (seat.Public) await LeavePublicAsync(session, reply: false);
        else await LeavePrivateAsync(session, reply: false);
    }

    private Task SendChannelsAsync(UserSession session)
    {
        List<NationChatPacketWriter.ChannelRow> rows;
        lock (_gate)
            rows = _channels.Select(c => new NationChatPacketWriter.ChannelRow(c.Id, c.Name, (ushort)c.Of(session.Nation).Count)).ToList();
        return session.Client.SendPacket(NationChatPacketWriter.ChannelList(rows));
    }

    private async Task EnterPublicAsync(UserSession session, Packet packet)
    {
        if (packet.RemainingBytes < 2) return;
        ushort id = packet.ReadUShort();
        byte result;
        lock (_gate)
        {
            var channel = _channels.FirstOrDefault(c => c.Id == id);
            if (_seats.ContainsKey(session.CharacterId)) result = NationChatResults.AlreadyInRoom;
            else if (channel == null) result = NationChatResults.NoRoom;
            else if (channel.Of(session.Nation).Count >= PublicCapacity) result = NationChatResults.PublicFull;
            else
            {
                channel.Of(session.Nation).Add(session.CharacterId);
                _seats[session.CharacterId] = new Seat(true, id);
                result = NationChatResults.Ok;
            }
        }
        await session.Client.SendPacket(NationChatPacketWriter.Result(NationChatPacketWriter.PublicEnter, result));
    }

    private Task LeavePublicAsync(UserSession session) => LeavePublicAsync(session, reply: true);

    private async Task LeavePublicAsync(UserSession session, bool reply)
    {
        lock (_gate)
        {
            if (!_seats.TryGetValue(session.CharacterId, out var seat) || !seat.Public)
                return;
            _channels.First(c => c.Id == seat.RoomId).Of(session.Nation).Remove(session.CharacterId);
            _seats.Remove(session.CharacterId);
        }
        if (reply)
            await session.Client.SendPacket(NationChatPacketWriter.Result(NationChatPacketWriter.PublicLeave, NationChatResults.Ok));
    }

    private Task SendRoomsAsync(UserSession session)
    {
        List<NationChatPacketWriter.RoomRow> rows;
        lock (_gate)
            rows = _rooms.Values.OrderBy(r => r.Id).Take(PrivateListMax).Select(RowOf).ToList();
        return session.Client.SendPacket(NationChatPacketWriter.RoomList(rows));
    }

    private async Task CreateAsync(UserSession session, Packet packet)
    {
        if (!TryReadRoomSettings(packet, out var name, out bool isPrivate, out var password, out ushort maxMembers))
            return;

        byte result;
        NationChatPacketWriter.RoomInfo info = default;
        lock (_gate)
        {
            result = SettingsError(name, isPrivate, password, NationChatResults.CreateNameLength, NationChatResults.CreatePasswordLength);
            if (result == NationChatResults.Ok && (_seats.ContainsKey(session.CharacterId) || _nextRoomId == ushort.MaxValue))
                result = NationChatResults.CreateRefused;
            if (result == NationChatResults.Ok)
            {
                var room = new PrivateRoom
                {
                    Id = _nextRoomId++,
                    Name = name,
                    Nation = session.Nation,
                    Password = isPrivate ? password : "",
                    MaxMembers = RoomSize(maxMembers),
                    OwnerId = session.CharacterId,
                };
                room.Members.Add(session.CharacterId);
                _rooms[room.Id] = room;
                _seats[session.CharacterId] = new Seat(false, room.Id);
                info = InfoOf(room);
                result = NationChatResults.Created;
            }
        }
        await session.Client.SendPacket(result == NationChatResults.Created
            ? NationChatPacketWriter.RoomCreated(info)
            : NationChatPacketWriter.Result(NationChatPacketWriter.Create, result));
    }

    private async Task EnterPrivateAsync(UserSession session, Packet packet)
    {
        if (packet.RemainingBytes < 3) return;
        ushort roomId = packet.ReadUShort();
        bool withPassword = packet.ReadByte() != 0;
        string password = withPassword && packet.RemainingBytes >= 2 ? packet.ReadString() : "";

        byte result;
        List<int> others = [];
        ushort count = 0;
        lock (_gate)
        {
            _rooms.TryGetValue(roomId, out var room);
            if (_seats.ContainsKey(session.CharacterId)) result = NationChatResults.AlreadyInRoom;
            else if (room == null) result = NationChatResults.NoRoom;
            else if (room.Nation != session.Nation) result = NationChatResults.NationMismatch;
            else if (room.Locked && password.Length is 0 or > PasswordMaxLength) result = NationChatResults.PasswordLength;
            else if (room.Locked && password != room.Password) result = NationChatResults.PasswordWrong;
            else if (room.Members.Count >= room.MaxMembers) result = NationChatResults.RoomFull;
            else
            {
                others = [.. room.Members];
                room.Members.Add(session.CharacterId);
                _seats[session.CharacterId] = new Seat(false, roomId);
                count = (ushort)room.Members.Count;
                result = NationChatResults.Ok;
            }
        }
        await session.Client.SendPacket(result == NationChatResults.Ok
            ? NationChatPacketWriter.Entered(roomId)
            : NationChatPacketWriter.Result(NationChatPacketWriter.PrivateEnter, result));
        if (result == NationChatResults.Ok)
            await SendToAsync(others, NationChatPacketWriter.MemberMoved(NationChatPacketWriter.SayEntered, count, session.Name));
    }

    private Task LeavePrivateAsync(UserSession session) => LeavePrivateAsync(session, reply: true);

    private async Task LeavePrivateAsync(UserSession session, bool reply)
    {
        List<int> rest;
        ushort count;
        UserSession? heir = null;
        PrivateRoom room;
        lock (_gate)
        {
            if (!_seats.TryGetValue(session.CharacterId, out var seat) || seat.Public || !_rooms.TryGetValue(seat.RoomId, out room!))
                return;
            room.Members.Remove(session.CharacterId);
            _seats.Remove(session.CharacterId);
            if (room.Members.Count == 0)
                _rooms.Remove(room.Id);
            else if (room.OwnerId == session.CharacterId)
            {
                room.OwnerId = room.Members[0];
                heir = sessionManager.GetByCharacterId(room.OwnerId);
            }
            rest = [.. room.Members];
            count = (ushort)rest.Count;
        }
        if (reply)
            await session.Client.SendPacket(NationChatPacketWriter.Result(NationChatPacketWriter.PrivateLeave, NationChatResults.Ok));
        if (rest.Count == 0) return;
        await SendToAsync(rest, NationChatPacketWriter.MemberMoved(NationChatPacketWriter.SayLeft, count, session.Name));
        if (heir != null)
        {
            await heir.Client.SendPacket(NationChatPacketWriter.NewOwner(InfoOf(room)));
            await SendToAsync(rest, NationChatPacketWriter.NamedEvent(NationChatPacketWriter.SayNewOwner, heir.Name));
        }
    }

    private async Task SearchAsync(UserSession session, Packet packet)
    {
        if (packet.RemainingBytes < 2) return;
        string word = packet.ReadString().Trim();
        if (word.Length is 0 or > TitleMaxLength)
        {
            await session.Client.SendPacket(NationChatPacketWriter.SearchResult(NationChatResults.SearchNameLength, []));
            return;
        }
        List<NationChatPacketWriter.RoomRow> rows;
        lock (_gate)
            rows = _rooms.Values.Where(r => r.Name.Contains(word, StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r.Id).Take(PrivateListMax).Select(RowOf).ToList();
        await session.Client.SendPacket(NationChatPacketWriter.SearchResult(
            rows.Count == 0 ? NationChatResults.SearchNone : NationChatResults.Ok, rows));
    }

    private async Task ChangeOptionsAsync(UserSession session, Packet packet)
    {
        if (packet.RemainingBytes < 2) return;
        ushort roomId = packet.ReadUShort();
        if (!TryReadRoomSettings(packet, out var name, out bool isPrivate, out var password, out ushort maxMembers))
            return;

        byte result;
        lock (_gate)
        {
            if (!_rooms.TryGetValue(roomId, out var room)) result = NationChatResults.OptionsNoRoom;
            else if (room.OwnerId != session.CharacterId) result = NationChatResults.NotOwner;
            else
            {
                result = SettingsError(name, isPrivate, password, NationChatResults.OptionsNameLength, NationChatResults.OptionsPasswordLength);
                if (result == NationChatResults.Ok)
                {
                    room.Name = name;
                    room.Password = isPrivate ? password : "";
                    room.MaxMembers = (ushort)Math.Max(RoomSize(maxMembers), room.Members.Count);
                }
            }
        }
        await session.Client.SendPacket(NationChatPacketWriter.Result(NationChatPacketWriter.Options, result));
    }

    private async Task HandOverAsync(UserSession session, Packet packet)
    {
        if (packet.RemainingBytes < 6) return;
        ushort roomId = packet.ReadUShort();
        int targetId = packet.ReadInt();

        byte result;
        UserSession? heir = null;
        List<int> members = [];
        NationChatPacketWriter.RoomInfo info = default;
        lock (_gate)
        {
            if (!_rooms.TryGetValue(roomId, out var room)) result = NationChatResults.OptionsNoRoom;
            else if (room.OwnerId != session.CharacterId) result = NationChatResults.NotOwner;
            else if (targetId == session.CharacterId || !room.Members.Contains(targetId)
                || (heir = sessionManager.GetByCharacterId(targetId)) == null)
                result = NationChatResults.HandOverNoTarget;
            else
            {
                room.OwnerId = targetId;
                members = [.. room.Members];
                info = InfoOf(room);
                result = NationChatResults.Ok;
            }
        }
        await session.Client.SendPacket(NationChatPacketWriter.Result(NationChatPacketWriter.HandOver, result));
        if (result != NationChatResults.Ok || heir == null) return;
        await heir.Client.SendPacket(NationChatPacketWriter.NewOwner(info));
        await SendToAsync(members, NationChatPacketWriter.NamedEvent(NationChatPacketWriter.SayNewOwner, heir.Name));
    }

    private async Task KickAsync(UserSession session, Packet packet)
    {
        if (packet.RemainingBytes < 6) return;
        ushort roomId = packet.ReadUShort();
        int targetId = packet.ReadInt();

        byte result;
        UserSession? target = null;
        List<int> rest = [];
        lock (_gate)
        {
            if (!_rooms.TryGetValue(roomId, out var room)) result = NationChatResults.OptionsNoRoom;
            else if (room.OwnerId != session.CharacterId) result = NationChatResults.NotOwner;
            else if (targetId == session.CharacterId) result = NationChatResults.KickSelf;
            else if (!room.Members.Remove(targetId)) result = NationChatResults.KickNoTarget;
            else
            {
                _seats.Remove(targetId);
                target = sessionManager.GetByCharacterId(targetId);
                rest = [.. room.Members];
                result = NationChatResults.Ok;
            }
        }
        await session.Client.SendPacket(NationChatPacketWriter.Result(NationChatPacketWriter.Kick, result));
        if (result != NationChatResults.Ok) return;
        if (target != null)
            await target.Client.SendPacket(NationChatPacketWriter.Result(NationChatPacketWriter.Kick, NationChatResults.YouWereKicked));
        string name = target?.Name ?? "";
        await SendToAsync(rest, NationChatPacketWriter.NamedEvent(NationChatPacketWriter.SayKicked, name));
    }

    private async Task SendMembersAsync(UserSession session, Packet packet)
    {
        if (packet.RemainingBytes < 2) return;
        ushort roomId = packet.ReadUShort();
        Packet reply;
        lock (_gate)
        {
            if (!_rooms.TryGetValue(roomId, out var room) || !room.Members.Contains(session.CharacterId))
                reply = NationChatPacketWriter.Result(NationChatPacketWriter.Members, NationChatResults.MembersNoRoom);
            else
            {
                var rows = room.Members
                    .Select(id => new NationChatPacketWriter.MemberRow(id, sessionManager.GetByCharacterId(id)?.Name ?? ""))
                    .ToList();
                reply = NationChatPacketWriter.MemberList(room.Id, room.Name, room.OwnerId, room.MaxMembers, rows);
            }
        }
        await session.Client.SendPacket(reply);
    }

    private static bool TryReadRoomSettings(Packet packet, out string name, out bool isPrivate, out string password, out ushort maxMembers)
    {
        name = "";
        password = "";
        isPrivate = false;
        maxMembers = 0;
        if (packet.RemainingBytes < 2) return false;
        name = packet.ReadString().Trim();
        if (packet.RemainingBytes < 1) return false;
        isPrivate = packet.ReadByte() != 0;
        if (isPrivate)
        {
            if (packet.RemainingBytes < 2) return false;
            password = packet.ReadString();
        }
        if (packet.RemainingBytes < 2) return false;
        maxMembers = packet.ReadUShort();
        return true;
    }

    private static byte SettingsError(string name, bool isPrivate, string password, byte nameError, byte passwordError) =>
        name.Length is 0 or > TitleMaxLength ? nameError
        : isPrivate && password.Length is 0 or > PasswordMaxLength ? passwordError
        : NationChatResults.Ok;

    private static ushort RoomSize(ushort asked) =>
        RoomSizes.Contains(asked) ? asked : RoomSizes[0];

    private static NationChatPacketWriter.RoomRow RowOf(PrivateRoom room) =>
        new(room.Id, room.Name, room.Locked, (byte)room.Nation, (ushort)room.Members.Count, room.MaxMembers);

    private static NationChatPacketWriter.RoomInfo InfoOf(PrivateRoom room) =>
        new(room.Id, room.Name, room.Locked, room.Password, room.MaxMembers);

    private async Task SendToAsync(IEnumerable<int> characterIds, Packet packet)
    {
        foreach (var id in characterIds)
        {
            var member = sessionManager.GetByCharacterId(id);
            if (member == null) continue;
            try { await member.Client.SendPacket(packet); }
            catch (Exception ex) { logger.LogDebug(ex, "Nation chat send failed for {Id}", id); }
        }
    }
}
