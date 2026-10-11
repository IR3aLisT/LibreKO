using System.Collections.Generic;
using LibreKO.Domain;

namespace LibreKO.Network;

public sealed record ChaosRanking(IReadOnlyList<ChaosRankRow> Rows, uint Exp, uint PremiumExp)
{
    public static ChaosRanking Read(Packet p)
    {
        int count = p.RemainingBytes >= 1 ? p.ReadByte() : 0;
        var rows = new List<ChaosRankRow>(count);
        for (int i = 0; i < count && p.RemainingBytes >= 2; i++)
        {
            string name = p.ReadString();
            int kills = p.RemainingBytes >= 2 ? p.ReadShort() : 0;
            int deaths = p.RemainingBytes >= 2 ? p.ReadShort() : 0;
            rows.Add(new ChaosRankRow(name, kills, deaths));
        }
        uint exp = p.RemainingBytes >= 4 ? p.ReadUInt() : 0;
        uint premium = p.RemainingBytes >= 4 ? p.ReadUInt() : 0;
        return new ChaosRanking(rows, exp, premium);
    }
}
