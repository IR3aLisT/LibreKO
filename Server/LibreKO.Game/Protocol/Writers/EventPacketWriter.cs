using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.World;

namespace LibreKO.Game.Protocol.Writers;

public sealed class EventPacketWriter
{
    private const ushort RivalPanelUnknown = 1;

    public static Packet FullMoonEntry(byte code)
    {
        var packet = new Packet(GameOpcodes.GS_EVENT);
        packet.WriteByte(World.FullMoonRules.EntrySub);
        packet.WriteByte(code);
        return packet;
    }

    public static Packet BattleZoneOpened(byte sub, byte openType, byte zone)
    {
        var packet = Battle(sub);
        packet.WriteByte(openType);
        packet.WriteByte(zone);
        return packet;
    }

    public static Packet BattleDeclare(byte sub, byte declareType, byte nation)
    {
        var packet = Battle(sub);
        packet.WriteByte(declareType);
        packet.WriteByte(nation);
        return packet;
    }

    public static Packet BattleZoneClosed(byte sub, byte closeType)
    {
        var packet = Battle(sub);
        packet.WriteByte(closeType);
        return packet;
    }

    public static Packet BattleResult(byte sub, byte resultType, byte winner, int karus, int elmorad)
    {
        var packet = Battle(sub);
        packet.WriteByte(resultType);
        packet.WriteByte(winner);
        packet.WriteInt(karus);
        packet.WriteInt(elmorad);
        return packet;
    }

    public static Packet AssignRival(
        byte sub, int rivalId, int money, int loyalty, string clanName, string rivalName)
    {
        var packet = Pvp(sub);
        packet.WriteInt(rivalId);
        packet.WriteInt(money);
        packet.WriteInt(loyalty);
        packet.WriteUShort(RivalPanelUnknown);
        packet.WriteUShort(RivalPanelUnknown);
        packet.WriteString(clanName);
        packet.WriteString(rivalName);
        return packet;
    }

    public static Packet PvpFlag(byte sub) => Pvp(sub);

    public static Packet AngerGauge(byte sub, byte gauge, byte isFull)
    {
        var packet = Pvp(sub);
        packet.WriteByte(gauge);
        packet.WriteByte(isFull);
        return packet;
    }

    public static Packet TempleSignUps(TempleEvent contest, ushort karus, ushort elMorad)
    {
        var packet = new Packet(GameOpcodes.GS_EVENT);
        packet.WriteByte((byte)TempleSubOpcode.TempleEventCounter);
        packet.WriteUShort(TempleEventRules.SignUpEventId(contest));
        packet.WriteUShort(karus);
        packet.WriteUShort(elMorad);
        return packet;
    }

    public static Packet TempleEvent(byte sub, byte result, short zone)
    {
        var packet = new Packet(GameOpcodes.GS_EVENT);
        packet.WriteByte(sub);
        packet.WriteByte(result);
        packet.WriteShort(zone);
        return packet;
    }

    public static Packet MonsterStone(MonsterStoneResult result)
    {
        var packet = new Packet(GameOpcodes.GS_EVENT);
        packet.WriteByte((byte)TempleSubOpcode.MonsterStone);
        packet.WriteByte((byte)result);
        return packet;
    }

    public static Packet MonsterStoneEntered(int itemId)
    {
        var packet = MonsterStone(MonsterStoneResult.Entered);
        packet.WriteInt(itemId);
        return packet;
    }

    public const ushort NestFinishEvent = 17;
    public const byte NestFinishResult = 101;

    public static Packet NestCompleted(uint closesInSeconds)
    {
        var packet = new Packet(GameOpcodes.GS_EVENT);
        packet.WriteByte((byte)TempleSubOpcode.TempleEventFinish);
        packet.WriteUShort(NestFinishEvent);
        packet.WriteByte(NestFinishResult);
        packet.WriteUInt(closesInSeconds);
        return packet;
    }

    public static Packet TempleScreenScores(int karusScore, int elmoradScore)
    {
        var packet = new Packet(GameOpcodes.GS_EVENT);
        packet.WriteByte((byte)TempleSubOpcode.TempleScreen);
        packet.WriteInt(karusScore);
        packet.WriteInt(elmoradScore);
        return packet;
    }

    public static Packet AltarTimer(ushort secondsRemaining)
    {
        var packet = new Packet(GameOpcodes.GS_EVENT);
        packet.WriteByte((byte)TempleSubOpcode.AltarTimer);
        packet.WriteUShort(secondsRemaining);
        return packet;
    }

    public static Packet AltarFlag(string playerName, byte nation)
    {
        var packet = new Packet(GameOpcodes.GS_EVENT);
        packet.WriteByte((byte)TempleSubOpcode.AltarKilledMessage);
        packet.WriteSByteString(playerName);
        packet.WriteByte(nation);
        return packet;
    }

    public static Packet TempleEventFinish(ushort eventId, byte winnerNation, uint countdownSeconds)
    {
        var packet = new Packet(GameOpcodes.GS_EVENT);
        packet.WriteByte((byte)TempleSubOpcode.TempleEventFinish);
        packet.WriteUShort(eventId);
        packet.WriteByte(winnerNation);
        packet.WriteUInt(countdownSeconds);
        return packet;
    }

    public const byte DrakiTimerHeaderFirst = 233;
    public const byte DrakiTimerHeaderSecond = 3;

    public static Packet DrakiTimer(
        ushort stage, ushort subStage, int timeLimitSeconds, int elapsedSeconds)
    {
        var packet = new Packet(GameOpcodes.GS_EVENT);
        packet.WriteByte((byte)TempleSubOpcode.DrakiTimer);
        packet.WriteByte(DrakiTimerHeaderFirst);
        packet.WriteByte(DrakiTimerHeaderSecond);
        packet.WriteUShort(stage);
        packet.WriteUShort(subStage);
        packet.WriteInt(timeLimitSeconds);
        packet.WriteInt(elapsedSeconds);
        return packet;
    }

    public static Packet BattleZoneState(byte sub, byte open, byte zone, int secondsRemaining)
    {
        var packet = Battle(sub);
        packet.WriteByte(open);
        packet.WriteByte(zone);
        packet.WriteInt(secondsRemaining);
        return packet;
    }

    private static Packet Battle(byte sub)
    {
        var packet = new Packet(GameOpcodes.GS_BATTLE_EVENT);
        packet.WriteByte(sub);
        return packet;
    }

    private static Packet Pvp(byte sub)
    {
        var packet = new Packet(GameOpcodes.GS_PVP);
        packet.WriteByte(sub);
        return packet;
    }
}
