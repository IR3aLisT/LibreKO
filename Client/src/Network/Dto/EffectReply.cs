namespace LibreKO.Network;

public readonly record struct EffectReply(int EntityId, int Value, string Name)
{
    public const int Announcement = -1;
    public const int SparkleValue = 1;
    public const int GlowValue = 2;
    public const int WindowValueFirst = 1000000;

    public static EffectReply? Read(Packet p)
    {
        if (p.RemainingBytes < 8) return null;
        int entity = p.ReadInt();
        int value = p.ReadInt();
        string name = entity == Announcement && p.RemainingBytes >= 1 ? p.ReadSByteString() : "";
        return new EffectReply(entity, value, name);
    }
}
