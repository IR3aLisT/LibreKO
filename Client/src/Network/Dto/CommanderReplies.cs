namespace LibreKO.Network;

public readonly record struct CommanderNamedReply(int CharId, string Name)
{
    public static CommanderNamedReply? Read(Packet p)
    {
        if (p.RemainingBytes < sizeof(int)) return null;
        int id = p.ReadInt();
        return new CommanderNamedReply(id, p.RemainingBytes >= sizeof(short) ? p.ReadString() : "");
    }
}

public readonly record struct CommanderMarkReply(int TargetId, bool Clear)
{
    private const int Bytes = 5;

    public static CommanderMarkReply? Read(Packet p) =>
        p.RemainingBytes >= Bytes ? new CommanderMarkReply(p.ReadInt(), p.ReadByte() != 0) : null;
}
