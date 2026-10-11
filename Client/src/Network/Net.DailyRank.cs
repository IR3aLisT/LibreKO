using System;
using System.Collections.Generic;
using LibreKO.Domain;

namespace LibreKO.Network;

public partial class Net
{
    public event Action<DailyRankBoardData>? DailyRankBoardEvent;
    public event Action? DailyRankResetEvent;

    private void HandleDailyRank(Packet p)
    {
        if (p.RemainingBytes < 1) return;
        switch (p.ReadByte())
        {
            case DailyRank.SubBoard when ReadDailyRankBoard(p) is { } board:
                DailyRankBoardEvent?.Invoke(board);
                break;
            case DailyRank.SubReset:
                DailyRankResetEvent?.Invoke();
                break;
        }
    }

    internal static DailyRankBoardData? ReadDailyRankBoard(Packet p)
    {
        if (p.RemainingBytes < 5) return null;
        int board = p.ReadByte();
        if (board >= DailyRank.BoardSlots) return null;
        int myPlace = p.ReadInt();
        if (board == DailyRank.AdonisBoard && myPlace == DailyRank.AdonisUnranked) myPlace = 0;
        int change = 0;
        if (DailyRank.HasChange(board))
        {
            if (p.RemainingBytes < 4) return null;
            change = p.ReadInt();
        }
        if (p.RemainingBytes < 2) return null;
        int count = Math.Min((int)p.ReadShort(), DailyRank.MaxNames);
        var entries = new List<DailyRankEntry>();
        for (int i = 0; i < count && p.RemainingBytes >= 1; i++)
        {
            string name = p.ReadSByteString();
            if (name.Length > 0) entries.Add(new DailyRankEntry(i + 1, name));
        }
        return new DailyRankBoardData(board, myPlace, change, entries);
    }

    public void SendDailyRankRequest(int board)
    {
        var p = new Packet(GameOpcodes.GS_DAILY_RANK);
        p.WriteByte(DailyRank.SubBoard);
        p.WriteByte((byte)board);
        _conn.Send(p);
    }
}
