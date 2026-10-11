using LibreKO.Common.Infrastructure.Network;

namespace LibreKO.Game.Protocol.Writers;

public sealed class PartyPacketWriter
{
    public readonly record struct WarBoardShare(int MemberId, ushort Share);

    public const short StatusMemberRecord = 1;

    public const byte MemberJoined = 1;
    public const byte MemberPromotedToLeader = 100;

    public const int NoTargetMark = -1;
    private const byte TrailingDefault = 0;

    public readonly record struct MemberState(
        int CharacterId,
        string Name,
        byte Level,
        short Class,
        short MaxHp,
        short Hp,
        short MaxMp,
        short Mp);

    private readonly PartySubOpcode _sub;
    private readonly Action<Packet> _body;

    private PartyPacketWriter(PartySubOpcode sub, Action<Packet> body)
    {
        _sub = sub;
        _body = body;
    }

    public static Packet MemberInfo(MemberState member, byte statusCode = MemberJoined, int markedTarget = NoTargetMark, byte partyType = TrailingDefault) =>
        new PartyPacketWriter(PartySubOpcode.MemberInfo, p =>
        {
            p.WriteShort(StatusMemberRecord);
            p.WriteInt(member.CharacterId);
            p.WriteByte(statusCode);
            p.WriteString(member.Name);
            p.WriteShort(member.MaxHp);
            p.WriteShort(member.Hp);
            p.WriteByte(member.Level);
            p.WriteShort(member.Class);
            p.WriteShort(member.MaxMp);
            p.WriteShort(member.Mp);
            p.WriteByte(TrailingDefault);
            p.WriteByte(TrailingDefault);
            p.WriteInt(markedTarget);
            p.WriteByte(partyType);
            p.WriteByte(TrailingDefault);
        }).Build();
    public static Packet Rejected(short errorCode) => new PartyPacketWriter(PartySubOpcode.MemberInfo, p => p.WriteShort(errorCode)).Build();
    public static Packet Invite(int inviterId, string inviterName) => new PartyPacketWriter(PartySubOpcode.Invite, p =>
        {
            p.WriteInt(inviterId);
            p.WriteString(inviterName);
        }).Build();
    public static Packet MemberLeft(int characterId) => new PartyPacketWriter(PartySubOpcode.MemberLeft, p => p.WriteInt(characterId)).Build();
    public static Packet Disband() => new PartyPacketWriter(PartySubOpcode.Disband, static _ => { }).Build();
    public static Packet VitalsChange(
        int characterId, short maxHp, short hp, short maxMp, short mp) => new PartyPacketWriter(PartySubOpcode.VitalsChange, p =>
        {
            p.WriteInt(characterId);
            p.WriteShort(maxHp);
            p.WriteShort(hp);
            p.WriteShort(maxMp);
            p.WriteShort(mp);
        }).Build();
    public static Packet LevelChange(int characterId, byte level) => new PartyPacketWriter(PartySubOpcode.LevelChange, p =>
        {
            p.WriteInt(characterId);
            p.WriteByte(level);
        }).Build();
    public static Packet ClassChange(int characterId, short characterClass) => new PartyPacketWriter(PartySubOpcode.ClassChange, p =>
        {
            p.WriteInt(characterId);
            p.WriteShort(characterClass);
        }).Build();
    public static Packet StatusEffect(int characterId, byte statusType, bool applied) => new PartyPacketWriter(PartySubOpcode.StatusEffect, p =>
        {
            p.WriteInt(characterId);
            p.WriteByte(statusType);
            p.WriteByte(applied ? (byte)1 : (byte)0);
        }).Build();
    public static Packet Commander(int characterId, string name) => new PartyPacketWriter(PartySubOpcode.Commander, p =>
        {
            p.WriteInt(characterId);
            p.WriteString(name);
        }).Build();
    public static Packet CommanderMark(int targetId, bool clear) => new PartyPacketWriter(PartySubOpcode.CommanderMark, p =>
        {
            p.WriteInt(targetId);
            p.WriteByte((byte)(clear ? 1 : 0));
        }).Build();
    public static Packet CommanderOrder(byte order) => new PartyPacketWriter(PartySubOpcode.CommanderOrder, p => p.WriteByte(order)).Build();
    public static Packet WarBoard(byte target, byte mode, IReadOnlyList<WarBoardShare> shares) => new PartyPacketWriter(PartySubOpcode.WarBoard, p =>
        {
            p.WriteByte(target);
            p.WriteByte(mode);
            p.WriteByte((byte)shares.Count);
            foreach (var share in shares)
            {
                p.WriteInt(share.MemberId);
                p.WriteShort((short)share.Share);
            }
        }).Build();
    private Packet Build()
    {
        var packet = new Packet(GameOpcodes.GS_PARTY);
        packet.WriteByte((byte)_sub);
        _body(packet);
        return packet;
    }
}
