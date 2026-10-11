using LibreKO.Common.Infrastructure.Network;

namespace LibreKO.Game.Protocol.Writers;

public enum DailyRankBoard : byte
{
    GrandMerchant = 0,
    KnightOfAdonis = 1,
    HeroOfChaos = 2,
    ApprenticeOfShozin = 4,
    ConquerorOfDraki = 5,
    DiscipleOfCharon = 6,
    KnightsOfFelankor = 8,
}

public static class DailyRankPacketWriter
{
    public const byte BoardSub = 1;
    public const byte ResetSub = 3;
    public const int MaxNames = 100;
    public const int NewEntryChange = 50001;

    public static bool IsBoard(byte board) => Enum.IsDefined(typeof(DailyRankBoard), board);

    public static bool HasChange(DailyRankBoard board) => board != DailyRankBoard.ConquerorOfDraki;

    public static int Change(int place, int previousPlace) =>
        place <= 0 ? 0 : previousPlace <= 0 ? NewEntryChange : previousPlace - place;

    public static Packet Board(DailyRankBoard board, int myPlace, int change, IReadOnlyList<string> names)
    {
        var packet = new Packet(GameOpcodes.GS_DAILY_RANK);
        packet.WriteByte(BoardSub);
        packet.WriteByte((byte)board);
        packet.WriteInt(Math.Max(myPlace, 0));
        if (HasChange(board))
            packet.WriteInt(change);
        var count = Math.Min(names.Count, MaxNames);
        packet.WriteShort((short)count);
        for (var i = 0; i < count; i++)
            packet.WriteSByteString(names[i]);
        return packet;
    }

    public static Packet Reset()
    {
        var packet = new Packet(GameOpcodes.GS_DAILY_RANK);
        packet.WriteByte(ResetSub);
        return packet;
    }
}
