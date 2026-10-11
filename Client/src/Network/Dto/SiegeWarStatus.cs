namespace LibreKO.Network;

public readonly record struct SiegeWarStatus(
    byte State, int OwnerClan, int OwnerMark, int OwnerAlliance, int HoldSeconds, int WarSeconds, string OwnerName)
{
    public const byte NoWar = 0;
    public const byte Unheld = 1;
    public const byte LastRunning = 3;
    public const byte Ended = 4;
    private const int FixedBytes = 1 + 3 * sizeof(short) + sizeof(int) + sizeof(short);

    public static readonly SiegeWarStatus None = new(NoWar, 0, 0, 0, 0, 0, "");

    public bool Running => State is >= Unheld and <= LastRunning;
    public bool Held => State is > Unheld and <= LastRunning;

    public static SiegeWarStatus? Read(Packet p)
    {
        if (p.RemainingBytes < FixedBytes) return null;
        byte state = p.ReadByte();
        int clan = p.ReadShort();
        int mark = p.ReadShort();
        int alliance = p.ReadShort();
        int hold = p.ReadInt();
        int war = p.ReadShort();
        string name = p.RemainingBytes >= 1 ? p.ReadSByteString() : "";
        return new SiegeWarStatus(state, clan, mark, alliance, hold, war, name);
    }
}

public readonly record struct SiegePoints(int Kind, int Gained, int Total)
{
    public const byte Obtained = 1;
    public const byte CreditedAtEnd = 2;
    public const byte Renewed = 3;
    private const byte Valid = 1;

    public static SiegePoints? Read(Packet p, int lastTotal)
    {
        if (p.RemainingBytes < 2 || p.ReadByte() != Valid) return null;
        int kind = p.ReadByte();
        int baseline = kind == CreditedAtEnd && p.RemainingBytes >= sizeof(int) ? p.ReadInt() : lastTotal;
        if (p.RemainingBytes < sizeof(int)) return null;
        int total = p.ReadInt();
        return new SiegePoints(kind, total - baseline, total);
    }
}
