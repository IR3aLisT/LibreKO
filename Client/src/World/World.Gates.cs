using System.Collections.Generic;
using Godot;

namespace LibreKO;

public partial class World
{
    private const int ObjectEventGate = 1;
    private const int ObjectEventGate2 = 2;
    private const int ObjectEventLever = 3;
    private const int ObjectEventLeverFlip = 14;
    private const float LeverTiltAngle = 0.7853982f;
    private const float GateMatchRadius = 4f;
    private const string GateWingPrefix = "gate_wing";
    private const float GateSwingRadiansPerSecond = 1f;
    private const float GateOpenAngle = 1.3962634f;
    private const float Gate2OpenAngle = 1.5707964f;

    private sealed class GateCollider
    {
        public Vector2 KoXZ;
        public StaticBody3D? Body;
        public int EventType;
        public readonly List<Node3D> Wings = new();
        public Tween? Swing;
    }

    private readonly List<GateCollider> _gateColliders = new();
    private readonly List<(Vector2 KoXZ, bool Open)> _gateStates = new();

    private void AddGateCollider(ObjInfo o, Node3D placed)
    {
        if (_objRoot == null) return;
        if (o.EventType == Net.ObjectEventKrowasGate)
        {
            RegisterKeyGate(o, placed);
            return;
        }
        if (o.EventType == Net.ObjectEventPoisonGas)
        {
            RegisterGasShape(o, placed);
            return;
        }
        if (o.EventType is ObjectEventLever or ObjectEventLeverFlip)
        {
            RegisterGate(new GateCollider { KoXZ = new Vector2(o.KoPos.X, o.KoPos.Z), EventType = o.EventType }, placed);
            return;
        }
        if (o.EventType is not (ObjectEventGate or ObjectEventGate2)) return;
        var faces = new List<Vector3>();
        CollectGateFaces(placed, faces);
        if (faces.Count == 0) return;

        var shape = new ConcavePolygonShape3D { BackfaceCollision = true };
        shape.SetFaces(faces.ToArray());
        var body = new StaticBody3D { Name = $"Gate{o.EventId}", CollisionLayer = 0, CollisionMask = 0 };
        body.AddChild(new CollisionShape3D { Shape = shape });
        _objRoot.AddChild(body);

        RegisterGate(new GateCollider { KoXZ = new Vector2(o.KoPos.X, o.KoPos.Z), Body = body, EventType = o.EventType }, placed);
    }

    private void RegisterGate(GateCollider gate, Node3D placed)
    {
        CollectGateWings(placed, gate.Wings);
        if (gate.Body == null && gate.Wings.Count == 0) return;
        gate.Wings.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        _gateColliders.Add(gate);
        foreach (var (at, open) in _gateStates)
            if (at.DistanceTo(gate.KoXZ) <= GateMatchRadius) SetGateClosed(gate, !open);
    }

    private static void CollectGateFaces(Node node, List<Vector3> faces)
    {
        if (node is MeshInstance3D { Mesh: { } mesh } mi)
        {
            var xform = mi.GlobalTransform;
            foreach (var v in mesh.GetFaces()) faces.Add(xform * v);
        }
        foreach (var child in node.GetChildren()) CollectGateFaces(child, faces);
    }

    private void NoteGateState(float koX, float koZ, bool open, bool flip = false, bool animate = false)
    {
        var at = new Vector2(koX, koZ);
        foreach (var gate in _gateColliders)
        {
            if (gate.KoXZ.DistanceTo(at) > GateMatchRadius) continue;
            if (flip && gate.Wings.Count > 0 && GodotObject.IsInstanceValid(gate.Wings[0])) open = gate.Wings[0].Rotation.X > 0f;
            SetGateClosed(gate, !open);
        }
        NoteMapObjectState(at, open, animate);
        _gateStates.RemoveAll(s => s.KoXZ.DistanceTo(at) <= GateMatchRadius);
        _gateStates.Add((at, open));
    }

    private static void CollectGateWings(Node node, List<Node3D> wings)
    {
        if (node is Node3D wing && wing.Name.ToString().StartsWith(GateWingPrefix, System.StringComparison.Ordinal))
            wings.Add(wing);
        foreach (var child in node.GetChildren()) CollectGateWings(child, wings);
    }

    private static void SetGateClosed(GateCollider gate, bool closed)
    {
        if (gate.Body != null && GodotObject.IsInstanceValid(gate.Body))
            gate.Body.CollisionLayer = closed ? WorldCollisionLayer : 0u;
        SwingGateWings(gate, closed);
    }

    private static void SwingGateWings(GateCollider gate, bool closed)
    {
        if (gate.Wings.Count == 0 || !GodotObject.IsInstanceValid(gate.Wings[0])) return;
        if (gate.Swing != null && gate.Swing.IsValid()) gate.Swing.Kill();
        gate.Swing = gate.Wings[0].CreateTween().SetParallel();
        for (int i = 0; i < gate.Wings.Count; i++)
        {
            var wing = gate.Wings[i];
            var target = GateWingRotation(wing.Rotation, gate.EventType, i, closed);
            float turn = (target - wing.Rotation).Length();
            if (turn <= 0f) continue;
            gate.Swing.TweenProperty(wing, "rotation", target, turn / GateSwingRadiansPerSecond);
        }
    }

    internal static Vector3 GateWingRotation(Vector3 current, int eventType, int wingIndex, bool closed)
    {
        if (eventType is ObjectEventLever or ObjectEventLeverFlip)
        {
            float tilt = closed ? LeverTiltAngle : -LeverTiltAngle;
            return new Vector3(wingIndex % 2 == 0 ? tilt : -tilt, current.Y, current.Z);
        }
        float open = eventType == ObjectEventGate2 ? Gate2OpenAngle : GateOpenAngle;
        float angle = closed ? 0f : wingIndex % 2 == 0 ? -open : open;
        return eventType == ObjectEventGate2
            ? new Vector3(current.X, current.Y, angle)
            : new Vector3(current.X, angle, current.Z);
    }

    internal static void PoseGateWings(Node root, int eventType, bool closed)
    {
        var wings = new List<Node3D>();
        CollectGateWings(root, wings);
        wings.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        for (int i = 0; i < wings.Count; i++)
            wings[i].Rotation = GateWingRotation(wings[i].Rotation, eventType, i, closed);
    }
}
