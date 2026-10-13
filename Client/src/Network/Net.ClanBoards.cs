using System;
using System.Collections.Generic;
using LibreKO.Domain;

namespace LibreKO.Network;

public partial class Net
{
    public const byte KnClanAdvance = 0x52, KnClanApply = 0x54, KnClanAdvert = 0x55, KnClanPromote = 0x56, KnWishBoard = 0x61;
    private const byte BoardStatusType = 1, BoardRowsType = 2;
    private const byte BoardPageRequest = 0, BoardOpenRequest = 1;
    private const byte WishPageType = 0, WishRequestsType = 1, WishIntroType = 2, WishRequestType = 3, WishOnlineType = 4,
        WishAcceptType = 5, WishPromoteType = 6;
    private const byte WishRowsSub = 0, WishStatusSub = 1, WishTooSoonSub = 4;
    private const byte PromoteBroadcastCode = 1;

    public event Action<ClanBoardStatus>? ClanBoardStatusEvent;
    public event Action<List<ClanAdvert>>? ClanAdvertsEvent;
    public event Action<int, int>? ClanApplyResultEvent;
    public event Action<int, int>? ClanAdvertResultEvent;
    public event Action<int, string, string>? ClanPromoteEvent;
    public event Action<List<WishPost>>? WishPostsEvent;
    public event Action<WishStatus>? WishStatusEvent;
    public event Action? WishTooSoonEvent;
    public event Action<List<WishRequest>>? WishRequestsEvent;
    public event Action<int, int>? WishIntroResultEvent;
    public event Action<int, int>? WishRequestResultEvent;
    public event Action<int, string>? WishPromoteEvent;

    private void HandleClanAdvance(Packet p)
    {
        if (p.RemainingBytes < 1) return;
        byte type = p.ReadByte();
        if (type == BoardStatusType && ReadBoardStatus(p) is { } status) ClanBoardStatusEvent?.Invoke(status);
        else if (type == BoardRowsType) ClanAdvertsEvent?.Invoke(ReadAdverts(p));
    }

    internal static ClanBoardStatus? ReadBoardStatus(Packet p)
    {
        if (p.RemainingBytes < 3) return null;
        int state = p.ReadByte();
        int count = p.ReadShort();
        var applicants = new List<ClanApplicant>();
        if (state == ClanBoards.StateStaff)
        {
            for (int i = 0; i < count && p.RemainingBytes >= 6; i++)
                applicants.Add(new ClanApplicant(p.ReadByte(), p.ReadByte(), p.ReadString(), p.ReadString()));
            return new ClanBoardStatus(state, applicants, 0, 0, "");
        }
        if (count == 0 || p.RemainingBytes < 6) return new ClanBoardStatus(state, applicants, 0, 0, "");
        int clanId = p.ReadShort();
        int mark = p.ReadShort();
        return new ClanBoardStatus(state, applicants, clanId, mark, p.ReadString());
    }

    internal static List<ClanAdvert> ReadAdverts(Packet p)
    {
        var rows = new List<ClanAdvert>();
        if (p.RemainingBytes < 2) return rows;
        int count = p.ReadShort();
        for (int i = 0; i < count && i < ClanBoards.RowsPerPage && p.RemainingBytes >= 12; i++)
            rows.Add(new ClanAdvert(p.ReadShort(), p.ReadString(), p.ReadByte(), p.ReadByte(), p.ReadString(), p.ReadShort(), p.ReadString()));
        return rows;
    }

    private void HandleClanKindResult(Packet p, Action<int, int>? handler)
    {
        int kind = p.RemainingBytes >= 1 ? p.ReadByte() : ClanBoards.KindRegister;
        int code = p.RemainingBytes >= 1 ? p.ReadByte() : 0;
        handler?.Invoke(kind, code);
    }

    private void HandleClanPromote(Packet p)
    {
        if (p.RemainingBytes < 1) return;
        int code = p.ReadByte();
        if (code != PromoteBroadcastCode) { ClanPromoteEvent?.Invoke(code, "", ""); return; }
        if (p.RemainingBytes < 3) return;
        p.ReadSByteString();
        string chief = p.ReadSByteString();
        string clan = p.ReadSByteString();
        ClanPromoteEvent?.Invoke(code, chief, clan);
    }

    private void HandleWishBoard(Packet p)
    {
        if (p.RemainingBytes < 1) return;
        switch (p.ReadByte())
        {
            case WishPageType when p.RemainingBytes >= 1:
                switch (p.ReadByte())
                {
                    case WishRowsSub: WishPostsEvent?.Invoke(ReadWishPosts(p)); break;
                    case WishStatusSub: WishStatusEvent?.Invoke(ReadWishStatus(p)); break;
                    case WishTooSoonSub: WishTooSoonEvent?.Invoke(); break;
                }
                break;
            case WishRequestsType: WishRequestsEvent?.Invoke(ReadWishRequests(p)); break;
            case WishIntroType: HandleClanKindResult(p, WishIntroResultEvent); break;
            case WishRequestType: HandleClanKindResult(p, WishRequestResultEvent); break;
            case WishPromoteType when p.RemainingBytes >= 1:
            {
                int code = p.ReadByte();
                WishPromoteEvent?.Invoke(code, code == PromoteBroadcastCode && p.RemainingBytes >= 1 ? p.ReadSByteString() : "");
                break;
            }
        }
    }

    internal static List<WishPost> ReadWishPosts(Packet p)
    {
        var rows = new List<WishPost>();
        if (p.RemainingBytes < 2) return rows;
        int count = p.ReadShort();
        if (count > ClanBoards.RowsPerPage) return rows;
        for (int i = 0; i < count && p.RemainingBytes >= 14; i++)
            rows.Add(new WishPost(p.ReadString(), p.ReadString(), p.ReadByte(), p.ReadByte(), p.ReadByte(), p.ReadByte() != 0,
                p.ReadInt(), p.ReadString()));
        return rows;
    }

    internal static WishStatus ReadWishStatus(Packet p)
    {
        int role = p.RemainingBytes >= 1 ? p.ReadByte() : ClanBoards.RoleRecruiter;
        if (role == ClanBoards.RoleRecruiter)
            return new WishStatus(role, p.RemainingBytes >= 2 ? p.ReadString() : "", 0);
        if (role == ClanBoards.RoleMember) return new WishStatus(role, "", 0);
        return new WishStatus(role, "", p.RemainingBytes >= 1 ? p.ReadByte() : 0);
    }

    internal static List<WishRequest> ReadWishRequests(Packet p)
    {
        var rows = new List<WishRequest>();
        if (p.RemainingBytes < 1) return rows;
        int count = p.ReadByte();
        for (int i = 0; i < count && i < ClanBoards.MaxRequests && p.RemainingBytes >= 12; i++)
            rows.Add(new WishRequest(p.ReadShort(), p.ReadString(), p.ReadString(), p.ReadByte(), p.ReadByte(), p.ReadShort(), p.ReadString()));
        return rows;
    }

    public void SendClanBoardOpen(int page) => SendClanBoard(BoardOpenRequest, page);

    public void SendClanBoardPage(int page) => SendClanBoard(BoardPageRequest, page);

    private void SendClanBoard(byte mode, int page)
    {
        var p = new Packet(GameOpcodes.GS_KNIGHTS_PROCESS);
        p.WriteByte(KnClanAdvance);
        p.WriteByte(mode);
        p.WriteByte((byte)page);
        _conn.Send(p);
    }

    public void SendClanApply(int clanId, string message)
    {
        var p = new Packet(GameOpcodes.GS_KNIGHTS_PROCESS);
        p.WriteByte(KnClanApply);
        p.WriteByte(ClanBoards.KindRegister);
        p.WriteShort((short)clanId);
        p.WriteString(message);
        _conn.Send(p);
    }

    public void SendClanApplyCancel(string name)
    {
        var p = new Packet(GameOpcodes.GS_KNIGHTS_PROCESS);
        p.WriteByte(KnClanApply);
        p.WriteByte(ClanBoards.KindDelete);
        p.WriteString(name);
        _conn.Send(p);
    }

    public void SendClanAdvertRegister(string text)
    {
        var p = new Packet(GameOpcodes.GS_KNIGHTS_PROCESS);
        p.WriteByte(KnClanAdvert);
        p.WriteByte(ClanBoards.KindRegister);
        p.WriteString(text);
        _conn.Send(p);
    }

    public void SendClanAdvertDelete()
    {
        var p = new Packet(GameOpcodes.GS_KNIGHTS_PROCESS);
        p.WriteByte(KnClanAdvert);
        p.WriteByte(ClanBoards.KindDelete);
        _conn.Send(p);
    }

    public void SendClanPromote() => SendKnightsByte(KnClanPromote);

    public void SendWishPage(int page, bool refresh)
    {
        var p = Wish(WishPageType);
        p.WriteByte((byte)page);
        p.WriteByte((byte)(refresh ? 1 : 0));
        _conn.Send(p);
    }

    public void SendWishRequestList() => _conn.Send(Wish(WishRequestsType));

    public void SendWishIntroRegister(int age, string text)
    {
        var p = Wish(WishIntroType);
        p.WriteByte(ClanBoards.KindRegister);
        p.WriteByte((byte)age);
        p.WriteSByteString(text);
        _conn.Send(p);
    }

    public void SendWishIntroDelete()
    {
        var p = Wish(WishIntroType);
        p.WriteByte(ClanBoards.KindDelete);
        _conn.Send(p);
    }

    public void SendWishOfflineRequest(int clanId, string poster, string message)
    {
        var p = Wish(WishRequestType);
        p.WriteByte(ClanBoards.KindRegister);
        p.WriteShort((short)clanId);
        p.WriteSByteString(poster);
        p.WriteSByteString(message);
        _conn.Send(p);
    }

    public void SendWishRequestCancel(int clanId)
    {
        var p = Wish(WishRequestType);
        p.WriteByte(ClanBoards.KindDelete);
        p.WriteShort((short)clanId);
        _conn.Send(p);
    }

    public void SendWishOnlineRequest(int targetId, string name)
    {
        var p = Wish(WishOnlineType);
        p.WriteInt(targetId);
        p.WriteSByteString(name);
        _conn.Send(p);
    }

    public void SendWishAccept(int clanId)
    {
        var p = Wish(WishAcceptType);
        p.WriteShort((short)clanId);
        _conn.Send(p);
    }

    public void SendWishPromote() => _conn.Send(Wish(WishPromoteType));

    private static Packet Wish(byte type)
    {
        var p = new Packet(GameOpcodes.GS_KNIGHTS_PROCESS);
        p.WriteByte(KnWishBoard);
        p.WriteByte(type);
        return p;
    }
}
