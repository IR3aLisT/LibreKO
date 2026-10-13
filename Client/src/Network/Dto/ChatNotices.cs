namespace LibreKO.Network;

public readonly record struct MonumentNotice(byte Kind, int Nation, string Name, bool Done, int Value)
{
    public const byte TopSecretCaptured = 1;
    public const byte QuestFailed = 2;
    public const byte TopSecretExpired = 3;
    public const byte MonumentCaptured = 4;
    public const byte SpyRescue = 5;
    public const byte Alert = 7;

    public static MonumentNotice? Read(Packet p)
    {
        if (p.RemainingBytes < 1) return null;
        byte kind = p.ReadByte();
        switch (kind)
        {
            case TopSecretCaptured or MonumentCaptured or SpyRescue:
            {
                if (p.RemainingBytes < 3) return null;
                int nation = p.ReadByte();
                string name = p.ReadString();
                if (name.Length == 0) return null;
                bool done = kind == SpyRescue && p.RemainingBytes >= 1 && p.ReadByte() != 0;
                return new MonumentNotice(kind, nation, name, done, 0);
            }
            case QuestFailed:
                return p.RemainingBytes >= sizeof(int) ? new MonumentNotice(kind, 0, "", false, p.ReadInt()) : null;
            case TopSecretExpired:
                return new MonumentNotice(kind, 0, "", false, 0);
            case Alert:
                return p.RemainingBytes >= 1 ? new MonumentNotice(kind, 0, "", false, p.ReadByte()) : null;
            default:
                return null;
        }
    }
}

public readonly record struct ChaosStoneNotice(string Breaker, int Nation)
{
    public static ChaosStoneNotice? Read(Packet p)
    {
        if (p.RemainingBytes < 2) return null;
        string breaker = p.ReadString();
        return p.RemainingBytes >= 1 ? new ChaosStoneNotice(breaker, p.ReadByte()) : null;
    }
}

public readonly record struct BoxItemNotice(string Name, int BoxItem, int Item)
{
    public static BoxItemNotice? Read(Packet p)
    {
        if (p.RemainingBytes < 1) return null;
        string name = p.ReadSByteString();
        if (p.RemainingBytes < 2 * sizeof(int)) return null;
        return new BoxItemNotice(name, p.ReadInt(), p.ReadInt());
    }
}
