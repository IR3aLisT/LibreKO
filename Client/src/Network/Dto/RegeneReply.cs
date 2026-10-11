namespace LibreKO.Network;

public readonly record struct RegeneReply
{
    public const byte Respawned = 1;
    public const byte Offer = 4;
    public const byte Refused = 10;
    public const ushort RefusedStandby = 4;
    public const ushort RefusedMissingItem = 10;
    public const float KoTenths = 10f;

    public float X { get; init; }
    public float Z { get; init; }
    public byte Type { get; init; }
    public int CasterId { get; init; }
    public int SkillId { get; init; }
    public ushort Code { get; init; }
    public int ItemId { get; init; }
    public ushort Amount { get; init; }

    public static RegeneReply? Read(Packet p)
    {
        if (p.RemainingBytes < 4) return null;
        var reply = new RegeneReply { X = p.ReadUShort() / KoTenths, Z = p.ReadUShort() / KoTenths, Type = Respawned };
        if (p.RemainingBytes >= 2) p.ReadUShort();
        if (p.RemainingBytes < 1) return reply;
        reply = reply with { Type = p.ReadByte() };
        if (reply.Type == Offer)
            return p.RemainingBytes >= 8 ? reply with { CasterId = p.ReadInt(), SkillId = p.ReadInt() } : null;
        if (reply.Type != Refused) return reply;
        if (p.RemainingBytes < 2) return null;
        reply = reply with { Code = p.ReadUShort() };
        return reply.Code == RefusedMissingItem && p.RemainingBytes >= 6
            ? reply with { ItemId = p.ReadInt(), Amount = p.ReadUShort() }
            : reply;
    }
}
