using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.World;

namespace LibreKO.Game.Protocol.Writers;

public sealed class MiscPacketWriter
{
    public readonly record struct ZonePopulation(ushort ZoneId, ushort Count);

    public static Packet ObjectEventResult(byte objectType, byte result)
    {
        var packet = new Packet(GameOpcodes.GS_OBJECT_EVENT);
        packet.WriteByte(objectType);
        packet.WriteByte(result);
        return packet;
    }

    public static Packet ObjectGateFlag(byte objectType, int uniqueId, bool open)
    {
        var packet = new Packet(GameOpcodes.GS_OBJECT_EVENT);
        packet.WriteByte(objectType);
        packet.WriteByte(GateFlagChanged);
        packet.WriteInt(uniqueId);
        packet.WriteByte((byte)(open ? 1 : 0));
        return packet;
    }

    private const byte GateFlagChanged = 1;

    public const byte ObjectTurnType = 10;

    public static Packet ObjectTurn(byte direction, int objectId, short degrees)
    {
        var packet = new Packet(GameOpcodes.GS_OBJECT_EVENT);
        packet.WriteByte(ObjectTurnType);
        packet.WriteByte(direction);
        packet.WriteInt(objectId);
        packet.WriteShort(degrees);
        return packet;
    }

    public static Packet ObjectEvent(byte objectType, byte result, int objectId)
    {
        var packet = new Packet(GameOpcodes.GS_OBJECT_EVENT);
        packet.WriteByte(objectType);
        packet.WriteByte(result);
        packet.WriteInt(objectId);
        return packet;
    }

    public const byte PremiumList = 1;

    public static Packet Premium(byte premiumType, int premiumHours, IReadOnlyList<PremiumBonus> bonuses)
    {
        var packet = new Packet(GameOpcodes.GS_PREMIUM);
        packet.WriteByte(PremiumList);
        bool active = premiumType != 0 && premiumHours > 0;
        packet.WriteByte(active ? (byte)1 : (byte)0);
        if (active)
        {
            packet.WriteByte(premiumType);
            packet.WriteUShort((ushort)Math.Min(premiumHours, ushort.MaxValue));
        }
        packet.WriteByte(active ? premiumType : (byte)0);
        packet.WriteByte((byte)bonuses.Count);
        foreach (var bonus in bonuses)
        {
            packet.WriteByte((byte)bonus.Kind);
            packet.WriteShort(bonus.Value);
        }
        return packet;
    }

    public static Packet AuthorityChange(byte sub, int characterId, byte fame)
    {
        var packet = new Packet(GameOpcodes.GS_AUTHORITY_CHANGE);
        packet.WriteByte(sub);
        packet.WriteInt(characterId);
        packet.WriteByte(fame);
        return packet;
    }

    public static Packet CorpseLocation(short targetId, short x, short z, short y)
    {
        var packet = new Packet(GameOpcodes.GS_CORPSE);
        packet.WriteShort(targetId);
        packet.WriteShort(x);
        packet.WriteShort(z);
        packet.WriteShort(y);
        return packet;
    }

    public static Packet SantaState(byte state)
    {
        var packet = new Packet(GameOpcodes.GS_SANTA);
        packet.WriteByte(state);
        return packet;
    }

    public static Packet ConcurrentUsers(short players, short summonedFamiliars)
    {
        var packet = new Packet(GameOpcodes.GS_CONCURRENTUSER);
        packet.WriteShort(players);
        packet.WriteShort(summonedFamiliars);
        return packet;
    }

    public static Packet ZoneConcurrentUsers(IReadOnlyList<ZonePopulation> zones)
    {
        var packet = new Packet(GameOpcodes.GS_ZONE_CONCURRENT);
        packet.WriteByte((byte)zones.Count);
        foreach (var zone in zones)
        {
            packet.WriteUShort(zone.ZoneId);
            packet.WriteUShort(zone.Count);
        }
        return packet;
    }

    public const byte ClanNameChangeMarker = 16;

    public static Packet NameChangeResult(byte resultCode)
    {
        var packet = new Packet(GameOpcodes.GS_NAME_CHANGE);
        packet.WriteByte(resultCode);
        return packet;
    }

    public static Packet ClanNameChangeResult(byte resultCode)
    {
        var packet = NameChangeResult(ClanNameChangeMarker);
        packet.WriteByte(resultCode);
        return packet;
    }

    public static Packet ClanRenamed(byte resultCode, string newName)
    {
        var packet = ClanNameChangeResult(resultCode);
        packet.WriteString(newName);
        return packet;
    }
}
