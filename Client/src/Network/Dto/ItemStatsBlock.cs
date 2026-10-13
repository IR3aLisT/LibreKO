using LibreKO.Domain;

namespace LibreKO.Network;

public static class ItemStatsBlock
{
    public const int Bytes = 34;
    private const byte FlagSet = 1;

    public static bool TryRead(Packet p, out DerivedStats stats)
    {
        stats = new DerivedStats();
        if (p.RemainingBytes < Bytes)
            return false;

        stats.TotalHit = p.ReadShort();
        stats.TotalAc = p.ReadShort();
        stats.MaxWeight = p.ReadInt();
        stats.HealStrikesEnemies = p.ReadByte() == FlagSet;
        p.ReadByte();
        stats.MaxHp = p.ReadShort();
        stats.MaxMp = p.ReadShort();
        stats.StrBonus = p.ReadShort();
        stats.StaBonus = p.ReadShort();
        stats.DexBonus = p.ReadShort();
        stats.IntBonus = p.ReadShort();
        stats.ChaBonus = p.ReadShort();
        stats.FireR = p.ReadShort();
        stats.ColdR = p.ReadShort();
        stats.LightningR = p.ReadShort();
        stats.MagicR = p.ReadShort();
        stats.DiseaseR = p.ReadShort();
        stats.PoisonR = p.ReadShort();
        return true;
    }
}
