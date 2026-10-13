using LibreKO.Domain;
using LibreKO.Network;
using Godot;

namespace LibreKO;

public partial class World
{
    private void ObjectEventInit()
    {
        Net.I.ObjectEventResultEvent += OnObjectEventResult;
        Net.I.ObjectEventGateStateEvent += OnObjectGateState;
        Net.I.ObjectEffectEvent += OnObjectEffect;
        Net.I.ObjectTurnEvent += OnObjectTurn;
    }

    private void ObjectEventDispose()
    {
        Net.I.ObjectEventResultEvent -= OnObjectEventResult;
        Net.I.ObjectEventGateStateEvent -= OnObjectGateState;
        Net.I.ObjectEffectEvent -= OnObjectEffect;
        Net.I.ObjectTurnEvent -= OnObjectTurn;
    }

    private static readonly StringName ObjectTurnAngleMeta = "object_turn_angle";
    private static readonly StringName ObjectTurnBaseMeta = "object_turn_base";

    private void OnObjectTurn(int objectId, byte direction, short degrees)
    {
        if (!_ents.TryGetValue(objectId, out var ent) || !IsInstanceValid(ent.Body)) return;
        var part = ent.Body.GetNodeOrNull<Node3D>(ModelNodeName) ?? ent.Body;
        if (!part.HasMeta(ObjectTurnBaseMeta)) part.SetMeta(ObjectTurnBaseMeta, part.Rotation.Y);
        float baseYaw = part.GetMeta(ObjectTurnBaseMeta).AsSingle();
        float current = part.HasMeta(ObjectTurnAngleMeta) ? part.GetMeta(ObjectTurnAngleMeta).AsSingle() : 0f;
        float target = ObjectTurn.TargetRadians(degrees);
        float from = ObjectTurn.StartFrom(current, target, direction);
        part.SetMeta(ObjectTurnAngleMeta, target);
        part.CreateTween().TweenMethod(Callable.From<float>(angle =>
        {
            if (IsInstanceValid(part)) part.Rotation = new Vector3(part.Rotation.X, baseYaw - angle, part.Rotation.Z);
        }), from, target, ObjectTurn.Seconds(from, target));
        Audio.PlayAt(ObjectTurn.SoundId, ent.Body);
    }

    private const float ObjectEffectHeight = 1.0f;

    private void OnObjectEffect(int entityId, int effectId)
    {
        string? fx = Fx.NameForId(effectId) ?? SkillData.Get(effectId)?.TargetFx;
        if (string.IsNullOrEmpty(fx)) return;
        SpawnFxOn(entityId, fx, ObjectEffectHeight);
    }

    private const string AnvilSuccessFx = "item_success";
    private const string AnvilFailFx = "item_fail";

    public bool TryOperateObject(short objectIndex, int npcId)
    {
        if (!_worldReady || _selfDead) return false;
        Net.I.SendObjectEvent(objectIndex, npcId);
        return true;
    }

    private const int TextGateClosed = 1801;
    private const int TextGateOpened = 1802;
    private const int TextLeverActivated = 2801;
    private const int TextLeverDeactivated = 2802;
    private const int TextLeverFailed = 1005;
    private const int LeverSound = 340101;
    private const int TextBindFailed = 1001;
    private const int TextBindDone = 1003;
    private const int TextWrongWarpGate = 1004;

    private void OnObjectEventResult(byte type, bool success, int objectId)
    {
        if (!success && Net.IsGateObject(type))
        {
            CombatNotice(SystemText(TextLeverFailed, "Failed turning the lever"));
            return;
        }
        switch (type)
        {
            case Net.ObjectEventAnvil:
                SpawnAnvilFx(objectId, success ? AnvilSuccessFx : AnvilFailFx);
                break;
            case Net.ObjectEventBind:
            case Net.ObjectEventRemoveBind:
                CombatNotice(success
                    ? SystemText(TextBindDone, "Successfully designated a re-spawn point.")
                    : SystemText(TextBindFailed, "Failed designating re-spawn point."));
                break;
            case Net.ObjectEventWarpGate when !success:
                CombatNotice(SystemText(TextWrongWarpGate, "You've selected the wrong Warp Gate."));
                break;
        }
    }

    private static string? GateStateText(byte type, bool open) => type switch
    {
        Net.ObjectEventGate or Net.ObjectEventGate2 => open
            ? SystemText(TextGateOpened, "The Castle Gate has opened")
            : SystemText(TextGateClosed, "The Castle Gate has been closed"),
        Net.ObjectEventGateLever => open
            ? SystemText(TextLeverActivated, "The lever has been activated.")
            : SystemText(TextLeverDeactivated, "The lever has been deactivated."),
        Net.ObjectEventWoo => SystemText(TextLeverActivated, "The lever has been activated."),
        _ => null,
    };

    private void SpawnAnvilFx(int anvilId, string fx)
    {
        var anchor = AnvilAnchor(anvilId);
        if (anchor != null) Fx.Spawn(fx, anchor, Vector3.Zero, oneShot: true);
    }

    private void OnObjectGateState(byte type, int uniqueId, bool gateOpen)
    {
        if (type == Net.ObjectEventKrowasGate)
        {
            OnKeyGateState(uniqueId, gateOpen);
            return;
        }
        if (!_ents.TryGetValue(uniqueId, out var ent)) return;
        NoteGateState(ent.KoX, ent.KoZ, gateOpen, flip: type == Net.ObjectEventWoo);
        if (type == Net.ObjectEventGateLever) Audio.PlayAt(LeverSound, ent.Body);
        if (GateStateText(type, gateOpen) is { } text) CombatNotice(text);
    }
}
