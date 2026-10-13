using System;

namespace LibreKO.Network;

public partial class Net
{
    public const byte ObjectEventBind = 0;
    public const byte ObjectEventGate = 1;
    public const byte ObjectEventGate2 = 2;
    public const byte ObjectEventGateLever = 3;
    public const byte ObjectEventFlagLever = 4;
    public const byte ObjectEventKrowasGate = 12;
    public const byte ObjectEventPoisonGas = 13;
    public const byte ObjectEventWoo = 14;
    private const int GateStateBytes = 5;
    public const byte ObjectEventWarpGate = 5;
    public const byte ObjectEventRemoveBind = 7;
    public const byte ObjectEventAnvil = 8;
    public const byte ObjectEventEffect = 11;
    private const int ObjectEffectBytes = 8;
    public const byte ObjectEventTurn = 10;
    private const int ObjectTurnBytes = 4 + 2;

    public event Action<byte, bool, int>? ObjectEventResultEvent;

    public event Action<byte, int, bool>? ObjectEventGateStateEvent;

    public event Action<int, int>? ObjectEffectEvent;

    public event Action<int, byte, short>? ObjectTurnEvent;

    public static bool IsGateObject(byte type) =>
        type is ObjectEventGate or ObjectEventGate2 or ObjectEventGateLever or ObjectEventFlagLever
            or ObjectEventKrowasGate or ObjectEventWoo;

    private void HandleObjectEvent(Packet p)
    {
        if (p.RemainingBytes < 2) return;
        byte type = p.ReadByte();
        byte result = p.ReadByte();
        bool success = result != 0;
        if (type == ObjectEventPoisonGas)
        {
            if (ReadShapeState(p) is { } gas) ObjectEventGateStateEvent?.Invoke(type, gas.Id, gas.On);
            return;
        }
        if (type == ObjectEventEffect)
        {
            if (p.RemainingBytes >= ObjectEffectBytes) ObjectEffectEvent?.Invoke(p.ReadInt(), p.ReadInt());
            return;
        }
        if (type == ObjectEventTurn)
        {
            if (p.RemainingBytes >= ObjectTurnBytes) ObjectTurnEvent?.Invoke(p.ReadInt(), result, p.ReadShort());
            return;
        }
        if (success && IsGateObject(type) && ReadShapeState(p) is { } gate)
        {
            ObjectEventGateStateEvent?.Invoke(type, gate.Id, gate.On);
            return;
        }
        int objectId = p.RemainingBytes >= 4 ? p.ReadInt() : 0;
        ObjectEventResultEvent?.Invoke(type, success, objectId);
    }

    internal static (int Id, bool On)? ReadShapeState(Packet p) =>
        p.RemainingBytes < GateStateBytes ? null : (p.ReadInt(), p.ReadByte() != 0);

    public void SendObjectEvent(short objectIndex, int npcId)
    {
        var p = new Packet(GameOpcodes.GS_OBJECT_EVENT);
        p.WriteShort(objectIndex);
        p.WriteInt(npcId);
        _conn.Send(p);
    }
}
