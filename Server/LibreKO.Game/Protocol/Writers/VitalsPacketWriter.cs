using LibreKO.Common.Infrastructure.Network;

namespace LibreKO.Game.Protocol.Writers;

public sealed class VitalsPacketWriter
{
    public const int NoAttacker = -1;

    public static Packet HpChange(short maxHp, short hp, int attackerId)
    {
        var packet = new Packet(GameOpcodes.GS_HP_CHANGE);
        packet.WriteShort(maxHp);
        packet.WriteShort(hp);
        packet.WriteInt(attackerId);
        return packet;
    }

    public const byte StaminaBar = 1;
    public const byte StaminaBarShown = 1;

    public static Packet StaminaChange(short maxSp, short sp)
    {
        var packet = new Packet(GameOpcodes.GS_SP_CHANGE);
        packet.WriteByte(StaminaBar);
        packet.WriteByte(StaminaBarShown);
        packet.WriteByte((byte)Math.Clamp((int)maxSp, 0, byte.MaxValue));
        packet.WriteByte((byte)Math.Clamp((int)sp, 0, byte.MaxValue));
        return packet;
    }

    public static Packet MpChange(short maxMp, short mp)
    {
        var packet = new Packet(GameOpcodes.GS_MSP_CHANGE);
        packet.WriteShort(maxMp);
        packet.WriteShort(mp);
        return packet;
    }
}
