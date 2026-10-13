using LibreKO.Common.Infrastructure.Network;

namespace LibreKO.Game.Protocol.Writers;

public enum RankerMessageSub : byte
{
    View = 1,
    Write = 2,
    Redress = 3,
}

public static class RankerMessageResult
{
    public const short Done = 1;
    public const short ViewUnderRevision = -1;
    public const short ViewInvalidType = -2;
    public const short WriteSettling = -1;
    public const short WriteNoData = -2;
    public const short WriteLengthError = -3;
    public const short WriteNotRanker = -4;
    public const short RedressSettling = -1;
    public const short RedressInvalidType = -3;
    public const short RedressNotRanker = -5;
    public const short RedressOncePerDay = -6;
}

public static class RankerMessagePacketWriter
{
    public static Packet Paper(string name, string message)
    {
        var packet = Result(RankerMessageSub.View, RankerMessageResult.Done);
        packet.WriteString(name);
        packet.WriteString(message);
        return packet;
    }

    public static Packet Result(RankerMessageSub sub, short result)
    {
        var packet = new Packet(GameOpcodes.GS_RANKER_MESSAGE);
        packet.WriteByte((byte)sub);
        packet.WriteShort(result);
        return packet;
    }
}
