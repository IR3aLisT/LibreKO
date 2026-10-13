using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;

namespace LibreKO.Game.Protocol.Writers;

public static class NationChatPacketWriter
{
    public const byte NationRooms = 0x0B;

    public const byte PublicList = 1;
    public const byte PublicEnter = 2;
    public const byte PublicLeave = 3;
    public const byte PrivateList = 4;
    public const byte Create = 5;
    public const byte PrivateEnter = 6;
    public const byte PrivateLeave = 7;
    public const byte Search = 8;
    public const byte Options = 9;
    public const byte HandOver = 10;
    public const byte Kick = 11;
    public const byte Members = 12;

    public const byte SayMessage = 0;
    public const byte SayEntered = 1;
    public const byte SayLeft = 2;
    public const byte SayNewOwner = 3;
    public const byte SayKicked = 4;

    private const byte NoFlag = 0;
    private const ushort UnknownRoomField = 0;

    public readonly record struct ChannelRow(ushort Id, string Name, ushort Members);

    public readonly record struct RoomRow(ushort Id, string Name, bool Locked, byte Nation, ushort Members, ushort MaxMembers);

    public readonly record struct RoomInfo(ushort Id, string Name, bool Private, string Password, ushort MaxMembers);

    public readonly record struct MemberRow(int CharacterId, string Name);

    public static Packet ChannelList(IReadOnlyList<ChannelRow> channels)
    {
        var packet = Nation(PublicList);
        packet.WriteUShort((ushort)channels.Count);
        foreach (var channel in channels)
        {
            packet.WriteUShort(channel.Id);
            packet.WriteString(channel.Name);
            packet.WriteUShort(channel.Members);
        }
        return packet;
    }

    public static Packet RoomList(IReadOnlyList<RoomRow> rooms) => WriteRooms(Nation(PrivateList), rooms);

    public static Packet SearchResult(byte result, IReadOnlyList<RoomRow> rooms)
    {
        var packet = Nation(Search);
        packet.WriteByte(result);
        return result == 0 ? WriteRooms(packet, rooms) : packet;
    }

    public static Packet Result(byte nationSub, byte result)
    {
        var packet = Nation(nationSub);
        packet.WriteByte(result);
        return packet;
    }

    public static Packet Entered(ushort roomId)
    {
        var packet = Result(PrivateEnter, 0);
        packet.WriteUShort(roomId);
        return packet;
    }

    public static Packet RoomCreated(RoomInfo room) => WriteRoomInfo(Result(Create, NationChatResults.Created), room);

    public static Packet NewOwner(RoomInfo room) => WriteRoomInfo(Result(HandOver, NationChatResults.YouOwnIt), room);

    public static Packet MemberList(ushort roomId, string title, int ownerId, ushort maxMembers, IReadOnlyList<MemberRow> members)
    {
        var packet = Result(Members, 0);
        packet.WriteUShort(roomId);
        packet.WriteString(title);
        packet.WriteInt(ownerId);
        packet.WriteUShort(maxMembers);
        packet.WriteUShort((ushort)members.Count);
        foreach (var member in members)
        {
            packet.WriteInt(member.CharacterId);
            packet.WriteString(member.Name);
        }
        return packet;
    }

    public static Packet Say(int senderId, string name, string text, ushort zoneId)
    {
        var packet = RoomChat(SayMessage);
        packet.WriteInt(senderId);
        packet.WriteString(name);
        packet.WriteString(text);
        packet.WriteUShort(zoneId);
        if (senderId >= 0) packet.WriteByte(NoFlag);
        return packet;
    }

    public static Packet MemberMoved(byte sayKind, ushort members, string name)
    {
        var packet = RoomChat(sayKind);
        packet.WriteUShort(members);
        packet.WriteString(name);
        return packet;
    }

    public static Packet NamedEvent(byte sayKind, string name)
    {
        var packet = RoomChat(sayKind);
        packet.WriteString(name);
        return packet;
    }

    private static Packet RoomChat(byte sayKind)
    {
        var packet = new Packet(GameOpcodes.GS_CHAT);
        packet.WriteByte((byte)ChatType.ChatRoom);
        packet.WriteByte(sayKind);
        return packet;
    }

    private static Packet WriteRooms(Packet packet, IReadOnlyList<RoomRow> rooms)
    {
        packet.WriteUShort((ushort)rooms.Count);
        foreach (var room in rooms)
        {
            packet.WriteUShort(room.Id);
            packet.WriteString(room.Name);
            packet.WriteByte((byte)(room.Locked ? 1 : 0));
            packet.WriteUShort(room.Nation);
            packet.WriteUShort(room.Members);
            packet.WriteUShort(room.MaxMembers);
        }
        return packet;
    }

    private static Packet WriteRoomInfo(Packet packet, RoomInfo room)
    {
        packet.WriteUShort(room.Id);
        packet.WriteString(room.Name);
        packet.WriteByte((byte)(room.Private ? 1 : 0));
        if (room.Private) packet.WriteString(room.Password);
        packet.WriteUShort(UnknownRoomField);
        packet.WriteUShort(room.MaxMembers);
        return packet;
    }

    private static Packet Nation(byte nationSub)
    {
        var packet = new Packet(GameOpcodes.GS_NATION_CHAT);
        packet.WriteByte(NationRooms);
        packet.WriteByte(nationSub);
        return packet;
    }
}

public static class NationChatResults
{
    public const byte Ok = 0;
    public const byte AlreadyInRoom = 1;
    public const byte NoRoom = 2;
    public const byte PasswordLength = 3;
    public const byte PasswordWrong = 4;
    public const byte NationMismatch = 5;
    public const byte RoomFull = 6;

    public const byte PublicFull = 5;

    public const byte Created = 1;
    public const byte CreateNameLength = 2;
    public const byte CreatePasswordLength = 3;
    public const byte CreateRefused = 4;

    public const byte SearchNone = 1;
    public const byte SearchNameLength = 2;

    public const byte NotOwner = 1;
    public const byte OptionsNameLength = 2;
    public const byte OptionsPasswordLength = 3;
    public const byte OptionsNoRoom = 4;

    public const byte HandOverNoTarget = 2;
    public const byte YouOwnIt = 5;

    public const byte KickSelf = 2;
    public const byte KickNoTarget = 3;
    public const byte YouWereKicked = 5;

    public const byte MembersNoRoom = 4;
}
