namespace LibreKO.Network;

public readonly record struct GoldChangeReply(byte Sub, int Amount, int Total, int Times = 0, int UnitId = -1)
{
    public const byte Earned = 1;
    public const byte Lost = 2;
    public const byte Used = 3;
    public const byte Received = 4;
    public const byte Multiplied = 5;

    private const int JackpotTailBytes = 6;

    public bool ChangesBalance => Sub != Multiplied;

    public static GoldChangeReply? Read(Packet p)
    {
        if (p.RemainingBytes < 9) return null;
        byte sub = p.ReadByte();
        int amount = p.ReadInt();
        int total = p.ReadInt();
        if (sub != Multiplied) return new GoldChangeReply(sub, amount, total);
        if (p.RemainingBytes < JackpotTailBytes) return null;
        int times = p.ReadUShort();
        return new GoldChangeReply(sub, amount, total, times, p.ReadInt());
    }
}
