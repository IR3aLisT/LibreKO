using System.Collections.Generic;
using Godot;
using LibreKO.Domain;

namespace LibreKO;

public partial class World
{
    private const string RestrictionPath = "res://assets/npcs/restriction.json";

    private static NpcRestriction? _npcRestriction;

    private static NpcRestriction Restriction => _npcRestriction ??= LoadRestriction();

    private static NpcRestriction LoadRestriction()
    {
        var restriction = new NpcRestriction();
        using var f = Godot.FileAccess.Open(RestrictionPath, Godot.FileAccess.ModeFlags.Read);
        if (f == null) return restriction;
        var root = Json.ParseString(f.GetAsText()).AsGodotDictionary();
        Fill(root, "touch", restriction.Touch);
        Fill(root, "unpickable", restriction.Unpickable);
        Fill(root, "pickTransformed", restriction.PickTransformed);
        return restriction;

        static void Fill(Godot.Collections.Dictionary root, string key, HashSet<int> into)
        {
            if (root.TryGetValue(key, out var ids) && ids.VariantType == Variant.Type.Array)
                foreach (var id in ids.AsGodotArray()) into.Add(id.AsInt32());
        }
    }

    private Ent? RayPickEntity(Vector2 mouse, out int id)
    {
        id = -1;
        if (_camera == null) return null;
        var from = _camera.ProjectRayOrigin(mouse);
        var dir = _camera.ProjectRayNormal(mouse);
        bool transformed = _selfTransformSkill != 0;
        Ent? best = null;
        float bestDistance = float.MaxValue;
        foreach (var (eid, e) in _ents)
        {
            if (!Selectable(e) || !IsInstanceValid(e.Body)) continue;
            if (e.IsNpc && (PickBox.SkippedType(e.NpcType) || !Restriction.Pickable(e.NpcId, transformed))) continue;
            float distance = from.DistanceSquaredTo(e.Body.GlobalPosition);
            if (distance >= bestDistance) continue;
            var inverse = e.Body.GlobalTransform.AffineInverse();
            if (PickBox.Hit(inverse * from, inverse.Basis * dir, EntityPickBox(e)) == null) continue;
            best = e;
            id = eid;
            bestDistance = distance;
        }
        return best;
    }

    private const float ShapeMatchDistance = 4f;

    private Dictionary<int, List<ObjInfo>>? _objectsByNpc;

    private Aabb EntityPickBox(Ent e)
    {
        if (e.ObjectType == NpcTypes.ObjectType.MapObject && ShapeBox(e) is { } shape) return shape;
        if (e.PickBox is { } cached) return cached;
        var box = JointBox(e) ?? EntityLocalBox(e);
        e.PickBox = box;
        return box;
    }

    private Aabb? ShapeBox(Ent e)
    {
        if (_objectsByNpc == null)
        {
            _objectsByNpc = new Dictionary<int, List<ObjInfo>>();
            foreach (var o in _objects)
            {
                if (o.NpcId == 0) continue;
                if (!_objectsByNpc.TryGetValue(o.NpcId, out var list)) _objectsByNpc[o.NpcId] = list = new List<ObjInfo>();
                list.Add(o);
            }
        }
        if (!_objectsByNpc.TryGetValue(e.NpcId, out var shapes)) return null;
        var at = new Vector2(e.KoX, e.KoZ);
        foreach (var o in shapes)
        {
            if (o.Mi == null || !IsInstanceValid(o.Mi)) continue;
            if (new Vector2(o.KoPos.X, o.KoPos.Z).DistanceTo(at) > ShapeMatchDistance) continue;
            return (e.Body.GlobalTransform.AffineInverse() * o.Mi.GlobalTransform) * o.Mi.GetAabb();
        }
        return null;
    }

    private static Aabb? JointBox(Ent e)
    {
        if (FindFirst<Skeleton3D>(e.Body) is not { } skeleton || skeleton.GetBoneCount() == 0) return null;
        var toBody = e.Body.GlobalTransform.AffineInverse() * skeleton.GlobalTransform;
        var joints = new List<Vector3>(skeleton.GetBoneCount());
        for (int i = 0; i < skeleton.GetBoneCount(); i++)
            joints.Add(toBody * skeleton.GetBoneGlobalRest(i).Origin);
        return PickBox.FromJoints(joints);
    }
}
