using System;
using System.Collections.Generic;

namespace LibreKO.Network;

public partial class Net
{
    public event Action<List<FriendEntry>>? FriendListEvent;

    public event Action<byte, string, FriendEntry>? FriendAddResultEvent;

    public event Action<byte, string>? FriendRemoveResultEvent;

    public event Action<string>? FriendLoggedInEvent;

    public event Action<string>? FriendRequestEvent;

    public event Action<byte>? FriendRequestResultEvent;

    private void HandleFriendProcess(Packet p)
    {
        if (p.RemainingBytes < 1) return;
        byte sub = p.ReadByte();
        switch (sub)
        {
            case FriendSubList:
            {
                if (p.RemainingBytes < 2) return;
                int count = p.ReadUShort();
                _friendList.Clear();
                for (int i = 0; i < count && p.RemainingBytes >= 7; i++)
                    _friendList.Add(ReadFriendStatus(p));
                FriendListEvent?.Invoke(new List<FriendEntry>(_friendList));
                break;
            }
            case FriendSubDetails:
            {
                if (p.RemainingBytes < 2) return;
                int count = p.ReadUShort();
                for (int i = 0; i < count && p.RemainingBytes >= 7; i++)
                {
                    string name = p.ReadString().Trim();
                    byte level = p.RemainingBytes >= 1 ? p.ReadByte() : (byte)0;
                    short cls = p.RemainingBytes >= 2 ? p.ReadShort() : (short)0;
                    byte nation = p.RemainingBytes >= 1 ? p.ReadByte() : (byte)0;
                    byte zone = p.RemainingBytes >= 1 ? p.ReadByte() : (byte)0;

                    for (int j = 0; j < _friendList.Count; j++)
                    {
                        if (!string.Equals(_friendList[j].Name, name, StringComparison.OrdinalIgnoreCase))
                            continue;
                        var e = _friendList[j];
                        e.Level = level; e.Class = cls; e.Nation = nation; e.ZoneId = zone;
                        _friendList[j] = e;
                        break;
                    }
                }
                FriendListEvent?.Invoke(new List<FriendEntry>(_friendList));
                break;
            }
            case FriendSubAdd:
                if (FriendReplies.ReadAdd(p) is { } added)
                    FriendAddResultEvent?.Invoke(added.Code, added.Added.Name, added.Added);
                break;
            case FriendSubRemove:
                if (FriendReplies.ReadRemove(p) is { } removed)
                    FriendRemoveResultEvent?.Invoke(removed.Code, removed.Name);
                break;
            case FriendSubLoggedIn:
                if (p.RemainingBytes >= 2) FriendLoggedInEvent?.Invoke(p.ReadString().Trim());
                break;
            case FriendSubRequest:
                if (p.RemainingBytes >= 2) FriendRequestEvent?.Invoke(p.ReadString().Trim());
                break;
            case FriendSubRequestResult:
                if (p.RemainingBytes >= 1) FriendRequestResultEvent?.Invoke(p.ReadByte());
                break;
        }
    }

    private const byte FriendSubList = 1;
    private const byte FriendSubAdd = 3;
    private const byte FriendSubRemove = 4;
    private const byte FriendSubLoggedIn = 5;
    private const byte FriendSubDetails = 6;
    private const byte FriendSubRequest = 16;
    private const byte FriendSubRequestResult = 17;
    private const int MaxFriendNameLength = 20;

    private readonly List<FriendEntry> _friendList = new();

    private static FriendEntry ReadFriendStatus(Packet p) => FriendReplies.ReadStatus(p);

    public void SendFriendListRequest()
    {
        var p = new Packet(GameOpcodes.GS_FRIEND_PROCESS);
        p.WriteByte(1);
        _conn.Send(p);
    }

    public void SendFriendAdd(string name) => SendFriendName(FriendSubAdd, name);

    public void SendFriendRemove(string name) => SendFriendName(FriendSubRemove, name);

    public void SendFriendAnswer(string requester, bool accept)
    {
        if (string.IsNullOrEmpty(requester)) return;
        var p = new Packet(GameOpcodes.GS_FRIEND_PROCESS);
        p.WriteByte(FriendSubRequest);
        p.WriteString(requester);
        p.WriteByte((byte)(accept ? 1 : 0));
        _conn.Send(p);
    }

    private void SendFriendName(byte sub, string name)
    {
        if (string.IsNullOrEmpty(name)) return;
        if (name.Length > MaxFriendNameLength) name = name.Substring(0, MaxFriendNameLength);
        var p = new Packet(GameOpcodes.GS_FRIEND_PROCESS);
        p.WriteByte(sub);
        p.WriteString(name);
        _conn.Send(p);
    }
}
