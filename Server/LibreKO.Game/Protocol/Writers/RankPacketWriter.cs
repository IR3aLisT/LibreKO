using LibreKO.Common.Infrastructure.Network;

namespace LibreKO.Game.Protocol.Writers;

public sealed class RankPacketWriter
{
    public const ushort NoPremiumBonus = 0;
    public const int MaxPremiumBonus = 999;
    public const byte NoSymbolRank = 0xFF;

    public readonly record struct RankEntry(
        string Name,
        byte Nation,
        ushort ClanId,
        ushort MarkVersion,
        string ClanName,
        int DailyLoyalty,
        ushort PremiumBonus = NoPremiumBonus,
        byte SymbolRank = NoSymbolRank);

    public static ushort PremiumShare(int premiumLoyalty) => (ushort)Math.Clamp(premiumLoyalty, 0, MaxPremiumBonus);

    public static Packet PkZone(
        byte rankType,
        IReadOnlyList<RankEntry> karus,
        IReadOnlyList<RankEntry> elmorad,
        ushort myRank,
        int myDailyLoyalty,
        ushort myPremiumBonus)
    {
        var packet = Sub(rankType);
        WriteNation(packet, karus);
        WriteNation(packet, elmorad);
        packet.WriteUShort(myRank);
        packet.WriteInt(myDailyLoyalty);
        packet.WriteUShort(myPremiumBonus);
        return packet;
    }

    public static Packet BorderDefenseWar(
        byte rankType,
        IReadOnlyList<RankEntry> karus,
        IReadOnlyList<RankEntry> elmorad,
        uint victoryExp,
        uint bonusExp)
    {
        var packet = Sub(rankType);
        WriteRoomNation(packet, karus);
        WriteRoomNation(packet, elmorad);
        packet.WriteUInt(victoryExp);
        packet.WriteUInt(bonusExp);
        return packet;
    }

    private static void WriteRoomNation(Packet packet, IReadOnlyList<RankEntry> entries)
    {
        packet.WriteUShort((ushort)entries.Count);
        foreach (var entry in entries)
        {
            packet.WriteString(entry.Name);
            packet.WriteByte(entry.Nation);
            packet.WriteUShort(entry.ClanId);
            packet.WriteUShort(entry.MarkVersion);
            packet.WriteString(entry.ClanName);
            packet.WriteInt(entry.DailyLoyalty);
        }
    }

    public readonly record struct ChaosRow(string Name, short Kills, short Deaths);

    public static Packet ChaosDungeon(byte rankType, IReadOnlyList<ChaosRow> rows, uint exp, uint premiumExp)
    {
        var packet = Sub(rankType);
        packet.WriteByte((byte)rows.Count);
        foreach (var row in rows)
        {
            packet.WriteString(row.Name);
            packet.WriteShort(row.Kills);
            packet.WriteShort(row.Deaths);
        }
        packet.WriteUInt(exp);
        packet.WriteUInt(premiumExp);
        return packet;
    }

    public static Packet Unsupported(byte rankType)
    {
        var packet = Sub(rankType);
        packet.WriteUShort(0);
        return packet;
    }

    private static void WriteNation(Packet packet, IReadOnlyList<RankEntry> entries)
    {
        packet.WriteUShort((ushort)entries.Count);
        foreach (var entry in entries)
        {
            packet.WriteString(entry.Name);
            packet.WriteByte(entry.Nation);
            packet.WriteUShort(entry.ClanId);
            packet.WriteUShort(entry.MarkVersion);
            packet.WriteString(entry.ClanName);
            packet.WriteInt(entry.DailyLoyalty);
            packet.WriteUShort(entry.PremiumBonus);
            packet.WriteByte(entry.SymbolRank);
        }
    }

    private static Packet Sub(byte rankType)
    {
        var packet = new Packet(GameOpcodes.GS_RANK);
        packet.WriteByte(rankType);
        return packet;
    }
}
