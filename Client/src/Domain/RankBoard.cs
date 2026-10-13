using System.Collections.Generic;

namespace LibreKO.Domain;

public enum RankBoardKind { King, CommanderKiller, Player, Clan, TopKiller }

public readonly record struct RankBoardRow(
    RankBoardKind Kind, int Index, string Name, int ClanId, int MarkVersion, string ClanName, int Tail)
{
    public bool HasClan => ClanId > 0;
}

public static class RankBoard
{
    public const int Rows = 10;
    public const int HallRows = 3;
    public const int EmptyBoardText = 15900;

    private const int SlotGroup = 1000;
    private const int HallLast = 33;
    private const int PlayerLast = 110;
    private const int ClanLast = 210;
    private const int TopKillerLast = 333;

    public static (RankBoardKind Kind, int Index)? Classify(int slot)
    {
        int local = slot % SlotGroup;
        int index = slot % 10 == 0 ? Rows - 1 : slot % 10 - 1;
        return local switch
        {
            0 => (RankBoardKind.King, 0),
            <= HallLast => (RankBoardKind.CommanderKiller, System.Math.Min(index, HallRows - 1)),
            <= PlayerLast => (RankBoardKind.Player, index),
            <= ClanLast => (RankBoardKind.Clan, index),
            <= TopKillerLast => (RankBoardKind.TopKiller, System.Math.Min(index, HallRows - 1)),
            _ => null,
        };
    }

    public static RankBoardRow? Row(int slot, string name, int clanId, int markVersion, string clanName, int tail) =>
        Classify(slot) is { } placed
            ? new RankBoardRow(placed.Kind, placed.Index, name, clanId, markVersion, clanName, tail)
            : null;

    public static RankBoardRow?[] Column(IEnumerable<RankBoardRow> rows, RankBoardKind kind, int count)
    {
        var column = new RankBoardRow?[count];
        foreach (var row in rows)
            if (row.Kind == kind && row.Index < count)
                column[row.Index] = row;
        return column;
    }
}
