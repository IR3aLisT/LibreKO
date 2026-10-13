namespace LibreKO.Network;

public readonly record struct ClanPremiumReply(bool Active, uint SecondsLeft)
{
    private const byte ActiveResult = 1;

    public static ClanPremiumReply? Read(Packet p)
    {
        if (p.RemainingBytes < 1) return null;
        p.ReadByte();
        bool active = p.RemainingBytes >= 1 && p.ReadByte() == ActiveResult;
        return new ClanPremiumReply(active, p.RemainingBytes >= sizeof(uint) ? p.ReadUInt() : 0u);
    }
}
