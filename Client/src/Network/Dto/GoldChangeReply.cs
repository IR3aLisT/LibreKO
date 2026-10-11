namespace LibreKO.Network;

public readonly record struct GoldChangeReply(byte Sub, int Amount, int Total)
{
    public const byte Earned = 1;
    public const byte Lost = 2;
    public const byte Used = 3;
    public const byte Received = 4;
    public const byte Multiplied = 5;

    public static GoldChangeReply? Read(Packet p) =>
        p.RemainingBytes < 9 ? null : new GoldChangeReply(p.ReadByte(), p.ReadInt(), p.ReadInt());
}
