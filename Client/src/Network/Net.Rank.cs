using System;

namespace LibreKO.Network;

public partial class Net
{
    public const byte RankTypePkZone = 1;
    public const byte RankTypeBorderWar = 2;
    public const byte RankTypeChaosDungeon = 3;

    public event Action<PkLadderReply>? RankEvent;
    public event Action<BorderWarRanking>? BorderWarRankEvent;

    private void HandleRank(Packet p)
    {
        if (p.RemainingBytes < 1) return;
        int rankType = p.ReadByte();
        if (rankType == RankTypeBorderWar)
        {
            BorderWarRankEvent?.Invoke(BorderWarRanking.Read(p));
            return;
        }
        if (rankType == RankTypeChaosDungeon)
        {
            ChaosRankEvent?.Invoke(ChaosRanking.Read(p));
            return;
        }
        if (rankType == RankTypePkZone) RankEvent?.Invoke(PkLadderReply.Read(p));
    }

    public void SendRankRequest(byte rankType)
    {
        var p = new Packet(GameOpcodes.GS_RANK);
        p.WriteByte(rankType);
        _conn.Send(p);
    }
}
