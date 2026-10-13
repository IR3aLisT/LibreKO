using LibreKO.Common.Infrastructure.Network;

namespace LibreKO.Game.Protocol.Writers;

public static class SiegeTowerPacketWriter
{
    public const byte SubBoard = 1;
    public const byte SubLeave = 2;
    public const byte SubEnded = 3;
    public const byte SubUse = 0x10;
    public const byte SubExit = 0x11;

    public const byte Failed = 0;
    public const byte Succeeded = 1;

    public const byte BoardAlreadyOnboard = 2;
    public const byte BoardNoUser = 3;
    public const byte BoardNotTower = 4;
    public const byte BoardTooFar = 5;

    public const byte LeaveNotOnboard = 2;
    public const byte LeaveTooFar = 3;

    public const byte EndedDismissed = 0;
    public const byte EndedCancelled = 1;

    public const byte UseNotAvailable = 2;
    public const byte UseInUse = 8;

    public static Packet Board(ushort x, ushort z, ushort y)
    {
        var packet = Result(SubBoard, Succeeded);
        packet.WriteUShort(x);
        packet.WriteUShort(z);
        packet.WriteUShort(y);
        return packet;
    }

    public static Packet Use(int userId, int npcId, ushort x, ushort z, ushort y)
    {
        var packet = Result(SubUse, Succeeded);
        packet.WriteInt(userId);
        packet.WriteInt(npcId);
        packet.WriteUShort(x);
        packet.WriteUShort(z);
        packet.WriteUShort(y);
        return packet;
    }

    public static Packet Exit(int userId, int npcId)
    {
        var packet = Result(SubExit, Succeeded);
        packet.WriteInt(userId);
        packet.WriteInt(npcId);
        return packet;
    }

    public static Packet Result(byte sub, byte result)
    {
        var packet = new Packet(GameOpcodes.GS_SIEGE_TOWER);
        packet.WriteByte(sub);
        packet.WriteByte(result);
        return packet;
    }
}
