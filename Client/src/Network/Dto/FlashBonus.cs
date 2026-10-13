namespace LibreKO.Network;

public readonly record struct FlashBonus(bool Ended, int Kind, int Value)
{
    public const byte Sub = 4;
    private const byte Started = 1;
    private const byte Finished = 2;

    public static FlashBonus? Read(Packet p)
    {
        if (p.RemainingBytes < 1) return null;
        byte action = p.ReadByte();
        if (action is not (Started or Finished)) return null;
        if (p.RemainingBytes < 2) return null;
        int kind = p.ReadByte();
        p.ReadByte();
        int value = action == Started && p.RemainingBytes >= sizeof(int) ? p.ReadInt() : 0;
        return new FlashBonus(action == Finished, kind, value);
    }
}
