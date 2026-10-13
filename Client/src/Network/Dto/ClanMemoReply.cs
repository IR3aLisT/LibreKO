namespace LibreKO.Network;

public readonly record struct ClanMemoReply(byte Result, string Memo)
{
    public static ClanMemoReply? Read(Packet p)
    {
        if (p.RemainingBytes < 2 || p.ReadByte() != Domain.ClanMemo.WriteKind) return null;
        byte result = p.ReadByte();
        return new ClanMemoReply(result, result == Domain.ClanMemo.Written && p.RemainingBytes >= 2 ? p.ReadString() : "");
    }
}
