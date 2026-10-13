using LibreKO.Common.Infrastructure.Network;

namespace LibreKO.Game.Protocol.Writers;

public sealed class ExperiencePacketWriter
{

    public const byte JackpotSub = 2;

    public static Packet Current(long currentExperience) => Sub(ExperienceSubOpcode.CurrentExperience, currentExperience);

    public static Packet Jackpot(int unitId, int times)
    {
        var packet = new Packet(GameOpcodes.GS_EXP_CHANGE);
        packet.WriteByte(JackpotSub);
        packet.WriteInt(unitId);
        packet.WriteUShort((ushort)times);
        return packet;
    }

    private static Packet Sub(ExperienceSubOpcode sub, long value)
    {
        var packet = new Packet(GameOpcodes.GS_EXP_CHANGE);
        packet.WriteByte((byte)sub);
        packet.WriteLong(value);
        return packet;
    }
}
