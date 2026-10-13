using System.Collections.Generic;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const int KeyGateEffect = 20029;
    private const int KeyGateOpenSound = 30002;
    private const int KeyGateMoveSound = 30003;
    private const int KeyGateLandSound = 6151;
    private const int TextObjectTooFar = 1914;
    private const float KeyGateReachFactor = 2f;
    private const float RadiusFromDiagonal = 0.5f;
    private static readonly Color KeyGateTextColour = new(0f, 1f, 0f);
    private static readonly Color ObjectTooFarColour = new(1f, 0.5f, 0.5f);

    private sealed class KeyGate
    {
        public Node3D Node = null!;
        public Vector3 Base;
        public Vector2 KoXZ;
        public int EventId;
        public float Radius;
        public readonly GateSlide Slide = new();
    }

    private sealed class GasShape
    {
        public Node3D Node = null!;
        public Vector2 KoXZ;
    }

    private readonly List<KeyGate> _keyGates = new();
    private readonly List<GasShape> _gasShapes = new();
    private readonly CameraShake _cameraShake = new();

    private void RegisterKeyGate(ObjInfo o, Node3D placed)
    {
        var gate = new KeyGate
        {
            Node = placed,
            Base = placed.Position,
            KoXZ = new Vector2(o.KoPos.X, o.KoPos.Z),
            EventId = o.EventId,
            Radius = ShapeRadius(placed),
        };
        AttachKeyGateCollider(placed);
        _keyGates.Add(gate);
        foreach (var (at, open) in _gateStates)
            if (at.DistanceTo(gate.KoXZ) <= GateMatchRadius) gate.Slide.Place(open);
        PlaceKeyGate(gate);
    }

    private void RegisterGasShape(ObjInfo o, Node3D placed)
    {
        var gas = new GasShape { Node = placed, KoXZ = new Vector2(o.KoPos.X, o.KoPos.Z) };
        _gasShapes.Add(gas);
        foreach (var (at, on) in _gateStates)
            if (at.DistanceTo(gas.KoXZ) <= GateMatchRadius) placed.Visible = on;
    }

    private static void AttachKeyGateCollider(Node3D placed)
    {
        var faces = new List<Vector3>();
        CollectGateFaces(placed, faces);
        if (faces.Count == 0) return;
        var toLocal = placed.GlobalTransform.AffineInverse();
        for (int i = 0; i < faces.Count; i++) faces[i] = toLocal * faces[i];
        var shape = new ConcavePolygonShape3D { BackfaceCollision = true };
        shape.SetFaces(faces.ToArray());
        var body = new StaticBody3D { Name = "KeyGateCollider", CollisionLayer = WorldCollisionLayer, CollisionMask = 0 };
        body.AddChild(new CollisionShape3D { Shape = shape });
        placed.AddChild(body);
    }

    private static float ShapeRadius(Node3D placed)
    {
        if (LocalMeshBounds(placed, placed) is not { } bounds) return 0f;
        var scale = placed.Basis.Scale;
        float grow = Mathf.Max(Mathf.Abs(scale.X), Mathf.Max(Mathf.Abs(scale.Y), Mathf.Abs(scale.Z)));
        return bounds.Size.Length() * RadiusFromDiagonal * grow;
    }

    private void NoteMapObjectState(Vector2 at, bool open, bool animate)
    {
        foreach (var gate in _keyGates)
        {
            if (gate.KoXZ.DistanceTo(at) > GateMatchRadius) continue;
            if (animate) gate.Slide.SetOpen(open);
            else gate.Slide.Place(open);
            PlaceKeyGate(gate);
        }
        foreach (var gas in _gasShapes)
            if (gas.KoXZ.DistanceTo(at) <= GateMatchRadius && IsInstanceValid(gas.Node))
                gas.Node.Visible = open;
    }

    private static void PlaceKeyGate(KeyGate gate)
    {
        if (!IsInstanceValid(gate.Node)) return;
        gate.Node.Position = gate.Base + Vector3.Up * gate.Slide.Current;
    }

    private void KeyGateTick(double delta)
    {
        foreach (var gate in _keyGates)
        {
            if (!gate.Slide.Moving) continue;
            if (gate.Slide.Step((float)delta)) Audio.PlayAt(KeyGateLandSound, gate.Node);
            PlaceKeyGate(gate);
        }
    }

    private void OnKeyGateState(int uniqueId, bool open)
    {
        if (!_ents.TryGetValue(uniqueId, out var ent)) return;
        ShakeCamera(CameraShake.GateSeconds, CameraShake.GateAmplitude, CameraShake.GateRate);
        if (Fx.NameForId(KeyGateEffect) is { } fx) Fx.Spawn(fx, this, ent.Body.GlobalPosition, oneShot: true);
        CombatNotice(open
            ? SystemText(TextGateOpened, "The Castle Gate has opened")
            : SystemText(TextGateClosed, "The Castle Gate has been closed"), KeyGateTextColour);
        if (open) Audio.PlayAt(KeyGateOpenSound, ent.Body);
        NoteGateState(ent.KoX, ent.KoZ, open, animate: true);
        Audio.PlayAt(KeyGateMoveSound, ent.Body);
    }

    private bool TryOperateKeyGate(Ent e, int npcUniqueId)
    {
        if (e.NpcType != NpcTypes.KeyGate) return false;
        var at = new Vector2(e.KoX, e.KoZ);
        KeyGate? gate = null;
        foreach (var candidate in _keyGates)
            if (candidate.KoXZ.DistanceTo(at) <= GateMatchRadius) { gate = candidate; break; }
        if (gate == null) return true;

        float self = _ents.TryGetValue(_myId, out var me) ? me.Radius : DefaultBodyRadius;
        if (FlatDistance(_self.Position, gate.Node.GlobalPosition) > KeyGateReachFactor * (self + gate.Radius))
        {
            CombatNotice(SystemText(TextObjectTooFar, "You are too far away from the object."), ObjectTooFarColour);
            return true;
        }
        TryOperateObject((short)gate.EventId, npcUniqueId);
        return true;
    }

    private float CameraShakeTick(double delta) => _cameraShake.Step((float)delta);

    private void ShakeCamera(float seconds, float amplitude, float rate)
    {
        if (Config.CameraShake) _cameraShake.Start(seconds, amplitude, rate);
    }
}
