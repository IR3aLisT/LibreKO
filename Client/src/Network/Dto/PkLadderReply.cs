using System.Collections.Generic;

namespace LibreKO.Network;

public sealed record PkLadderReply(
    IReadOnlyList<RankEntry> Karus,
    IReadOnlyList<RankEntry> ElMorad,
    int MyRank,
    int MyPoints,
    int MyBonus)
{
    public static PkLadderReply Read(Packet p)
    {
        var karus = ReadNation(p);
        var elmorad = ReadNation(p);
        int myRank = p.RemainingBytes >= sizeof(ushort) ? p.ReadUShort() : 0;
        int myPoints = p.RemainingBytes >= sizeof(int) ? p.ReadInt() : 0;
        int myBonus = p.RemainingBytes >= sizeof(ushort) ? p.ReadUShort() : 0;
        return new PkLadderReply(karus, elmorad, myRank, myPoints, myBonus);
    }

    private static List<RankEntry> ReadNation(Packet p)
    {
        var list = new List<RankEntry>();
        if (p.RemainingBytes < sizeof(ushort)) return list;
        int count = p.ReadUShort();
        for (int i = 0; i < count; i++)
        {
            if (p.RemainingBytes < sizeof(ushort)) break;
            string name = p.ReadString();
            if (p.RemainingBytes < 1 + 2 * sizeof(ushort)) break;
            int nation = p.ReadByte();
            int knightsId = p.ReadUShort();
            p.ReadUShort();
            if (p.RemainingBytes < sizeof(ushort)) break;
            string clan = p.ReadString();
            if (p.RemainingBytes < sizeof(int)) break;
            int loyalty = p.ReadInt();
            int bonus = p.RemainingBytes >= sizeof(ushort) ? p.ReadUShort() : 0;
            int symbol = p.RemainingBytes >= 1 ? p.ReadByte() : byte.MaxValue;
            list.Add(new RankEntry
            {
                Name = name,
                Nation = nation,
                KnightsId = knightsId,
                ClanName = clan,
                Loyalty = loyalty,
                Bonus = bonus,
                Symbol = symbol,
                Place = i + 1,
            });
        }
        return list;
    }
}
