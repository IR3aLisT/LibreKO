using System;
using System.Collections.Generic;
using LibreKO.Domain;

namespace LibreKO.Network;

public readonly record struct NationChannel(int Id, string Name, int Members);

public readonly record struct NationRoom(int Id, string Name, bool Locked, int Nation, int Members, int MaxMembers);

public readonly record struct NationRoomInfo(int Id, string Name, bool Private, string Password, int MaxMembers);

public readonly record struct NationRoomMember(int CharacterId, string Name);

public sealed record NationRoomMembers(int RoomId, string Title, int OwnerId, int MaxMembers, IReadOnlyList<NationRoomMember> Members);

public static class NationChatReplies
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

    public const byte Created = 1;
    public const byte YouOwnIt = 5;
    public const int PublicListMax = 16;
    public const int PrivateListMax = 500;
    public const int MemberListMax = 200;

    public static List<NationChannel> ReadChannels(Packet p)
    {
        var list = new List<NationChannel>();
        if (p.RemainingBytes < 2) return list;
        int count = Math.Min((int)p.ReadUShort(), PublicListMax);
        for (int i = 0; i < count && p.RemainingBytes >= 2; i++)
        {
            int id = p.ReadUShort();
            if (p.RemainingBytes < 2) break;
            string name = p.ReadString();
            if (p.RemainingBytes < 2) break;
            list.Add(new NationChannel(id, name, p.ReadUShort()));
        }
        return list;
    }

    public static List<NationRoom> ReadRooms(Packet p)
    {
        var list = new List<NationRoom>();
        if (p.RemainingBytes < 2) return list;
        int count = Math.Min((int)p.ReadUShort(), PrivateListMax);
        for (int i = 0; i < count && p.RemainingBytes >= 2; i++)
        {
            int id = p.ReadUShort();
            if (p.RemainingBytes < 2) break;
            string name = p.ReadString();
            if (p.RemainingBytes < 7) break;
            bool locked = p.ReadByte() == 1;
            int nation = p.ReadUShort();
            int members = p.ReadUShort();
            int max = p.ReadUShort();
            list.Add(new NationRoom(id, name, locked, nation, members, max));
        }
        return list;
    }

    public static NationRoomInfo? ReadRoomInfo(Packet p)
    {
        if (p.RemainingBytes < 4) return null;
        int id = p.ReadUShort();
        string name = p.ReadString();
        if (p.RemainingBytes < 1) return null;
        bool isPrivate = p.ReadByte() == 1;
        string password = isPrivate && p.RemainingBytes >= 2 ? p.ReadString() : "";
        if (p.RemainingBytes < 4) return null;
        p.ReadUShort();
        int max = p.ReadUShort();
        return new NationRoomInfo(id, name, isPrivate, password, max);
    }

    public static NationRoomMembers? ReadMembers(Packet p)
    {
        if (p.RemainingBytes < 4) return null;
        int roomId = p.ReadUShort();
        string title = p.ReadString();
        if (p.RemainingBytes < 8) return null;
        int owner = p.ReadInt();
        int max = p.ReadUShort();
        int count = Math.Min((int)p.ReadUShort(), MemberListMax);
        var members = new List<NationRoomMember>();
        for (int i = 0; i < count && p.RemainingBytes >= 6; i++)
            members.Add(new NationRoomMember(p.ReadInt(), p.ReadString()));
        return new NationRoomMembers(roomId, title, owner, max, members);
    }
}

public readonly record struct RoomChatLine(int SenderId, string Name, string Text, int ZoneId);

public partial class Net
{
    public const byte RoomChatMessage = 0;
    public const byte RoomChatEntered = 1;
    public const byte RoomChatLeft = 2;
    public const byte RoomChatNewOwner = 3;
    public const byte RoomChatKicked = 4;

    public NationChatState NationChat { get; } = new();

    public event Action<List<NationChannel>>? NationChannelsEvent;
    public event Action<List<NationRoom>>? NationRoomsEvent;
    public event Action<byte, byte>? NationChatResultEvent;
    public event Action<List<NationRoom>>? NationSearchEvent;
    public event Action<NationRoomMembers>? NationMembersEvent;
    public event Action? NationChatStateEvent;
    public event Action<RoomChatLine>? RoomChatEvent;
    public event Action<byte, int, string>? RoomChatNoticeEvent;

    private int _pendingPublicChannel = NationChatRules.NoRoom;
    private (string Name, bool Locked, int Members, int Max) _pendingRoom = ("", false, 0, 0);
    private (string Name, bool Private, string Password, int Max)? _pendingOptions;

    private void HandleNationChat(Packet p)
    {
        if (p.RemainingBytes < 2 || p.ReadByte() != NationChatReplies.NationRooms) return;
        byte sub = p.ReadByte();
        switch (sub)
        {
            case NationChatReplies.PublicList:
                NationChannelsEvent?.Invoke(NationChatReplies.ReadChannels(p));
                return;
            case NationChatReplies.PrivateList:
                NationRoomsEvent?.Invoke(NationChatReplies.ReadRooms(p));
                return;
            case NationChatReplies.Members:
            {
                byte result = ReadResult(p);
                if (result == 0 && NationChatReplies.ReadMembers(p) is { } members)
                    NationMembersEvent?.Invoke(members);
                else NationChatResultEvent?.Invoke(sub, result);
                return;
            }
        }

        byte code = ReadResult(p);
        switch (sub)
        {
            case NationChatReplies.PublicEnter:
                if (code == 0) EnterChatRoom(() => NationChat.EnteredPublic(_pendingPublicChannel));
                _pendingPublicChannel = NationChatRules.NoRoom;
                break;
            case NationChatReplies.PublicLeave:
            case NationChatReplies.PrivateLeave:
                if (code == 0) LeaveChatRoom();
                break;
            case NationChatReplies.Create:
                if (code == NationChatReplies.Created && NationChatReplies.ReadRoomInfo(p) is { } created)
                    EnterChatRoom(() => NationChat.OwnRoom(created.Id, created.Name, created.Private, created.Password, created.MaxMembers));
                break;
            case NationChatReplies.PrivateEnter:
                if (code == 0)
                {
                    int roomId = p.RemainingBytes >= 2 ? p.ReadUShort() : 0;
                    EnterChatRoom(() => NationChat.EnteredPrivate(roomId));
                }
                break;
            case NationChatReplies.Search:
                if (code == 0) NationSearchEvent?.Invoke(NationChatReplies.ReadRooms(p));
                break;
            case NationChatReplies.HandOver:
                if (code == 0) NationChat.LostOwnership();
                else if (code == NationChatReplies.YouOwnIt && NationChatReplies.ReadRoomInfo(p) is { } owned)
                    NationChat.OwnRoom(owned.Id, owned.Name, owned.Private, owned.Password, owned.MaxMembers);
                break;
            case NationChatReplies.Kick:
                if (code == NationChatRules.KickedCode) LeaveChatRoom();
                break;
            case NationChatReplies.Options:
                if (code == 0 && _pendingOptions is { } options && NationChat.InPrivate)
                    NationChat.Configured(options.Name, options.Private, options.Password, options.Max);
                _pendingOptions = null;
                break;
        }
        NationChatResultEvent?.Invoke(sub, code);
        NationChatStateEvent?.Invoke();
    }

    private static byte ReadResult(Packet p) => p.RemainingBytes >= 1 ? p.ReadByte() : (byte)0;

    private void EnterChatRoom(Action enter)
    {
        RoomTimeline.Clear();
        DropVoice();
        enter();
        if (!NationChat.Owner && _pendingRoom.Name.Length > 0)
            NationChat.Describe(_pendingRoom.Name, _pendingRoom.Locked, _pendingRoom.Members + 1, _pendingRoom.Max);
        _pendingRoom = ("", false, 0, 0);
        ChatRoomPanelShown = true;
        VoicePeers.Clear();
        SendRoomHistory(0);
        SendVoiceRoster();
    }

    private void LeaveChatRoom()
    {
        NationChat.Left();
        LeftChatRoom();
    }

    private void HandleRoomChat(Packet p)
    {
        if (p.RemainingBytes < 1) return;
        byte kind = p.ReadByte();
        switch (kind)
        {
            case RoomChatMessage:
            {
                if (p.RemainingBytes < 6) return;
                int sender = p.ReadInt();
                string name = p.ReadString();
                if (p.RemainingBytes < 2) return;
                string text = p.ReadString();
                int zone = p.RemainingBytes >= 2 ? p.ReadUShort() : 0;
                if (text.Length == 0) return;
                RoomTimeline.Add(RoomPost.Line(sender, name, text, DateTime.Now));
                RoomChatEvent?.Invoke(new RoomChatLine(sender, name, text, zone));
                return;
            }
            case RoomChatEntered:
            case RoomChatLeft:
            {
                if (p.RemainingBytes < 4) return;
                int members = p.ReadUShort();
                NationChat.Counted(members);
                RoomChatNoticeEvent?.Invoke(kind, members, p.ReadString());
                return;
            }
            case RoomChatNewOwner:
            case RoomChatKicked:
                if (p.RemainingBytes >= 2) RoomChatNoticeEvent?.Invoke(kind, 0, p.ReadString());
                return;
        }
    }

    public void SendNationChannels() => SendNationChat(NationChatReplies.PublicList);

    public void SendNationRooms() => SendNationChat(NationChatReplies.PrivateList);

    public void NameNextRoom(string name, bool locked, int members, int maxMembers) =>
        _pendingRoom = (name, locked, members, maxMembers);

    public void SendNationPublicEnter(int channelId)
    {
        _pendingPublicChannel = channelId;
        var p = NationChatPacket(NationChatReplies.PublicEnter);
        p.WriteUShort((ushort)channelId);
        _conn.Send(p);
    }

    public void SendNationPublicLeave()
    {
        var p = NationChatPacket(NationChatReplies.PublicLeave);
        p.WriteUShort((ushort)Math.Max(0, NationChat.RoomId));
        _conn.Send(p);
    }

    public void SendNationCreate(string name, bool isPrivate, string password, int maxMembers) =>
        _conn.Send(RoomSettings(NationChatPacket(NationChatReplies.Create), name, isPrivate, password, maxMembers));

    public void SendNationOptions(string name, bool isPrivate, string password, int maxMembers)
    {
        _pendingOptions = (name, isPrivate, password, maxMembers);
        var p = NationChatPacket(NationChatReplies.Options);
        p.WriteUShort((ushort)Math.Max(0, NationChat.RoomId));
        _conn.Send(RoomSettings(p, name, isPrivate, password, maxMembers));
    }

    public void SendNationPrivateEnter(int roomId, string password)
    {
        var p = NationChatPacket(NationChatReplies.PrivateEnter);
        p.WriteUShort((ushort)roomId);
        p.WriteByte((byte)(password.Length > 0 ? 1 : 0));
        if (password.Length > 0) p.WriteString(password);
        _conn.Send(p);
    }

    public void SendNationPrivateLeave()
    {
        var p = NationChatPacket(NationChatReplies.PrivateLeave);
        p.WriteUShort((ushort)Math.Max(0, NationChat.RoomId));
        _conn.Send(p);
    }

    public void SendNationSearch(string word)
    {
        var p = NationChatPacket(NationChatReplies.Search);
        p.WriteString(word);
        _conn.Send(p);
    }

    public void SendNationHandOver(int memberId) => SendNationMemberAction(NationChatReplies.HandOver, memberId);

    public void SendNationKick(int memberId) => SendNationMemberAction(NationChatReplies.Kick, memberId);

    public void SendNationMembers()
    {
        var p = NationChatPacket(NationChatReplies.Members);
        p.WriteUShort((ushort)Math.Max(0, NationChat.RoomId));
        _conn.Send(p);
    }

    public void SendRoomChat(string text)
    {
        if (string.IsNullOrEmpty(text) || !NationChat.InRoom) return;
        text = TextRunes.Truncate(text, ChatMessageMaxLength);
        var p = new Packet(GameOpcodes.GS_CHAT);
        p.WriteByte(ChatType.ChatRoom);
        p.WriteString(text);
        p.WriteUShort((ushort)NationChat.RoomId);
        _conn.Send(p);
    }

    private void SendNationMemberAction(byte sub, int memberId)
    {
        var p = NationChatPacket(sub);
        p.WriteUShort((ushort)Math.Max(0, NationChat.RoomId));
        p.WriteInt(memberId);
        _conn.Send(p);
    }

    private void SendNationChat(byte sub) => _conn.Send(NationChatPacket(sub));

    private static Packet NationChatPacket(byte sub)
    {
        var p = new Packet(GameOpcodes.GS_NATION_CHAT);
        p.WriteByte(NationChatReplies.NationRooms);
        p.WriteByte(sub);
        return p;
    }

    private static Packet RoomSettings(Packet p, string name, bool isPrivate, string password, int maxMembers)
    {
        p.WriteString(name);
        p.WriteByte((byte)(isPrivate ? 1 : 0));
        if (isPrivate) p.WriteString(password);
        p.WriteUShort((ushort)maxMembers);
        return p;
    }
}
