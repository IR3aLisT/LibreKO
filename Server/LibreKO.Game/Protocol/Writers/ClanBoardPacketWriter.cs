using LibreKO.Common.Infrastructure.Network;

namespace LibreKO.Game.Protocol.Writers;

public static class ClanBoardPacketWriter
{
    public const byte BoardStatus = 1;
    public const byte BoardRows = 2;
    public const byte StateStaff = 0;
    public const byte StateMember = 1;
    public const byte StateClanless = 2;
    public const byte KindRegister = 0;
    public const byte KindDelete = 1;
    public const byte PromoteBroadcast = 1;
    public const byte WishPage = 0;
    public const byte WishRequests = 1;
    public const byte WishIntroResult = 2;
    public const byte WishRequestResult = 3;
    public const byte WishPromote = 6;
    public const byte WishPageRows = 0;
    public const byte WishPageStatus = 1;
    public const byte WishPageTooSoon = 4;
    public const byte RoleRecruiter = 0;
    public const byte RoleMember = 1;
    public const byte RoleSeeker = 2;
    public const int NoTarget = -1;

    public readonly record struct Applicant(byte Level, byte Class, string Name, string Message);
    public readonly record struct AppliedClan(short ClanId, short MarkVersion, string ClanName);
    public readonly record struct AdvertRow(short ClanId, string ClanName, byte Grade, byte Type, string Chief, short MarkVersion, string Text);
    public readonly record struct WishRow(string Name, string Introduction, byte Level, byte Class, byte Age, bool Online, int TargetId, string Registered);
    public readonly record struct WishRequest(short ClanId, string Leader, string ClanName, byte Grade, byte Type, short MarkVersion, string Message);

    public static Packet StaffStatus(IReadOnlyList<Applicant> applicants)
    {
        var packet = Sub(KnightsSubOpcode.ClanAdvance);
        packet.WriteByte(BoardStatus);
        packet.WriteByte(StateStaff);
        packet.WriteShort((short)applicants.Count);
        foreach (var applicant in applicants)
        {
            packet.WriteByte(applicant.Level);
            packet.WriteByte(applicant.Class);
            packet.WriteString(applicant.Name);
            packet.WriteString(applicant.Message);
        }
        return packet;
    }

    public static Packet ApplicantStatus(byte state, AppliedClan? applied)
    {
        var packet = Sub(KnightsSubOpcode.ClanAdvance);
        packet.WriteByte(BoardStatus);
        packet.WriteByte(state);
        packet.WriteShort((short)(applied == null ? 0 : 1));
        if (applied is { } clan)
        {
            packet.WriteShort(clan.ClanId);
            packet.WriteShort(clan.MarkVersion);
            packet.WriteString(clan.ClanName);
        }
        return packet;
    }

    public static Packet AdvertRows(IReadOnlyList<AdvertRow> rows)
    {
        var packet = Sub(KnightsSubOpcode.ClanAdvance);
        packet.WriteByte(BoardRows);
        packet.WriteShort((short)rows.Count);
        foreach (var row in rows)
        {
            packet.WriteShort(row.ClanId);
            packet.WriteString(row.ClanName);
            packet.WriteByte(row.Grade);
            packet.WriteByte(row.Type);
            packet.WriteString(row.Chief);
            packet.WriteShort(row.MarkVersion);
            packet.WriteString(row.Text);
        }
        return packet;
    }

    public static Packet KindResult(KnightsSubOpcode sub, byte kind, byte code)
    {
        var packet = Sub(sub);
        packet.WriteByte(kind);
        packet.WriteByte(code);
        return packet;
    }

    public static Packet PromoteRefused(byte code)
    {
        var packet = Sub(KnightsSubOpcode.ClanAdvancePromote);
        packet.WriteByte(code);
        return packet;
    }

    public static Packet Promoted(string clanName, string chief)
    {
        var packet = Sub(KnightsSubOpcode.ClanAdvancePromote);
        packet.WriteByte(PromoteBroadcast);
        packet.WriteSByteString(clanName);
        packet.WriteSByteString(chief);
        packet.WriteSByteString(clanName);
        return packet;
    }

    public static Packet WishRows(IReadOnlyList<WishRow> rows)
    {
        var packet = Wish(WishPage);
        packet.WriteByte(WishPageRows);
        packet.WriteShort((short)rows.Count);
        foreach (var row in rows)
        {
            packet.WriteString(row.Name);
            packet.WriteString(row.Introduction);
            packet.WriteByte(row.Level);
            packet.WriteByte(row.Class);
            packet.WriteByte(row.Age);
            packet.WriteByte((byte)(row.Online ? 1 : 0));
            packet.WriteInt(row.TargetId);
            packet.WriteString(row.Registered);
        }
        return packet;
    }

    public static Packet WishRecruiterStatus(string requestedName)
    {
        var packet = Wish(WishPage);
        packet.WriteByte(WishPageStatus);
        packet.WriteByte(RoleRecruiter);
        packet.WriteString(requestedName);
        return packet;
    }

    public static Packet WishMemberStatus()
    {
        var packet = Wish(WishPage);
        packet.WriteByte(WishPageStatus);
        packet.WriteByte(RoleMember);
        return packet;
    }

    public static Packet WishSeekerStatus(int received)
    {
        var packet = Wish(WishPage);
        packet.WriteByte(WishPageStatus);
        packet.WriteByte(RoleSeeker);
        packet.WriteByte((byte)received);
        return packet;
    }

    public static Packet WishTooSoon()
    {
        var packet = Wish(WishPage);
        packet.WriteByte(WishPageTooSoon);
        return packet;
    }

    public static Packet WishRequestList(IReadOnlyList<WishRequest> requests)
    {
        var packet = Wish(WishRequests);
        packet.WriteByte((byte)requests.Count);
        foreach (var request in requests)
        {
            packet.WriteShort(request.ClanId);
            packet.WriteString(request.Leader);
            packet.WriteString(request.ClanName);
            packet.WriteByte(request.Grade);
            packet.WriteByte(request.Type);
            packet.WriteShort(request.MarkVersion);
            packet.WriteString(request.Message);
        }
        return packet;
    }

    public static Packet WishResult(byte type, byte action, byte code)
    {
        var packet = Wish(type);
        packet.WriteByte(action);
        packet.WriteByte(code);
        return packet;
    }

    public static Packet WishPromoteRefused(byte code)
    {
        var packet = Wish(WishPromote);
        packet.WriteByte(code);
        return packet;
    }

    public static Packet WishPromoted(string name)
    {
        var packet = Wish(WishPromote);
        packet.WriteByte(PromoteBroadcast);
        packet.WriteSByteString(name);
        return packet;
    }

    private static Packet Wish(byte type)
    {
        var packet = Sub(KnightsSubOpcode.WishBoard);
        packet.WriteByte(type);
        return packet;
    }

    private static Packet Sub(KnightsSubOpcode sub)
    {
        var packet = new Packet(GameOpcodes.GS_KNIGHTS_PROCESS);
        packet.WriteByte((byte)sub);
        return packet;
    }
}
