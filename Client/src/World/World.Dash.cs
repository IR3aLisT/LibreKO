using Godot;

namespace LibreKO;

public partial class World
{
    private const double DashSeconds = 0.3;
    private const double PullReachSeconds = 0.2;
    private const float DashStopDefault = 1.124f;
    private const float DashStopMin = 1.1f;
    private const float DashStopMax = 1.2f;
    private const float DashRayLift = 1.0f;
    private const float KoTenths = 10f;
    private const int DashXSlot = 0;
    private const int DashResultSlot = 1;
    private const int DashZSlot = 2;
    private const int MagicDataSlots = 7;
    private const short DashSucceeded = 1;
    private const int PullFailedFx = 30731;
    private const int TextSkillFailed = 4404;

    private Vector3 _selfDashFrom, _selfDashTo;
    private double _selfDashStart = double.NegativeInfinity;

    private static int DashWarpType(SkillData.Skill s)
    {
        int warp = s.WarpKind;
        if (warp is WarpType.Rush or WarpType.Pull) return warp;
        return SkillReach.SpotKind(s.Id) switch
        {
            SkillReach.SpotRush => WarpType.Rush,
            SkillReach.SpotPull => WarpType.Pull,
            _ => 0,
        };
    }

    private short[]? DashRequestData(int skillId, int targetId)
    {
        if (SkillData.Get(skillId) is not { } s) return null;
        int warp = DashWarpType(s);
        if (warp == 0 || !_ents.TryGetValue(targetId, out var target)) return null;
        float radius = _ents.TryGetValue(_myId, out var me) ? me.Radius : DefaultBodyRadius;
        var mine = _self.GlobalPosition;
        var theirs = target.Body.GlobalPosition;
        var dest = warp == WarpType.Rush ? DashDestination(mine, theirs, radius) : DashDestination(theirs, mine, radius);
        var (koX, koZ) = WorldToKo(dest);
        var data = new short[MagicDataSlots];
        data[DashXSlot] = Tenths(koX);
        data[DashZSlot] = Tenths(koZ);
        return data;
    }

    private static short Tenths(float ko) =>
        unchecked((short)(ushort)Mathf.Clamp(Mathf.RoundToInt(ko * KoTenths), 0, ushort.MaxValue));

    private static float FromTenths(short wire) => unchecked((ushort)wire) / KoTenths;

    private Vector3 DashDestination(Vector3 from, Vector3 to, float radius)
    {
        float stop = radius is >= DashStopMin and <= DashStopMax ? radius : DashStopDefault;
        var span = new Vector3(to.X - from.X, 0f, to.Z - from.Z);
        float length = span.Length();
        if (length <= stop) return from;
        var dir = span / length;
        var goal = to - dir * stop;

        var query = PhysicsRayQueryParameters3D.Create(
            from + Vector3.Up * DashRayLift, goal + Vector3.Up * DashRayLift, WorldCollisionLayer);
        query.Exclude = SightExclusions();
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count > 0)
        {
            var at = (Vector3)hit["position"];
            float clear = new Vector2(at.X - from.X, at.Z - from.Z).Length();
            goal = from + dir * Mathf.Max(0f, clear - Mathf.Min(stop, clear));
        }
        goal.Y = HeightNear(goal);
        return goal;
    }

    private bool OnDashEffecting(SkillData.Skill s, int casterId, int targetId, short[] data)
    {
        int warp = DashWarpType(s);
        if (warp == 0) return false;

        PlaySkillAction(casterId, SkillAnim(casterId, s, false), ClipsForCast(s), ActionRankSkill, restart: true);
        if (targetId >= 0) FaceToward(casterId, targetId, instant: true);

        bool succeeded = data.Length > DashZSlot && data[DashResultSlot] == DashSucceeded;
        if (!succeeded)
        {
            if (warp == WarpType.Pull && Fx.NameForId(PullFailedFx) is { } failed)
                SpawnFxOn(casterId, failed, 0f);
            if (casterId == _myId) CombatNotice(SystemText(TextSkillFailed, "Failed %s.", s.Name));
            return true;
        }

        float koX = FromTenths(data[DashXSlot]), koZ = FromTenths(data[DashZSlot]);
        if (warp == WarpType.Rush)
        {
            if (s.SelfFx1 != null) SpawnOwnedFx(casterId, s.Id, 1, s.SelfFx1, s.SelfPart1, oneShot: true);
            DashEntity(casterId, koX, koZ);
            return true;
        }

        if (s.SelfFx1 != null) SpawnOwnedFx(casterId, s.Id, 1, s.SelfFx1, s.SelfPart1, oneShot: true);
        GetTree().CreateTimer(PullReachSeconds).Timeout += () =>
        {
            if (s.TargetFx != null) SpawnOwnedFx(targetId, s.Id, 3, s.TargetFx, s.TargetPart, oneShot: true);
            DashEntity(targetId, koX, koZ);
        };
        return true;
    }

    private void DashEntity(int id, float koX, float koZ)
    {
        if (id == _myId)
        {
            if (_self == null || !IsInstanceValid(_self)) return;
            _selfDashFrom = _self.Position;
            _selfDashTo = EntityGroundPos(koX, koZ, NoServerHeight, _selfLift);
            _selfDashStart = Now();
            return;
        }
        if (!_ents.TryGetValue(id, out var e)) return;
        var dest = EntityGroundPos(koX, koZ, NoServerHeight, e.Lift);
        float distance = new Vector2(dest.X - e.Body.Position.X, dest.Z - e.Body.Position.Z).Length();
        e.KoX = koX;
        e.KoZ = koZ;
        e.Target = dest;
        e.HasTarget = true;
        e.Speed = distance / (float)DashSeconds;
    }

    private bool TickSelfDash()
    {
        if (double.IsNegativeInfinity(_selfDashStart) || _self == null) return false;
        double t = (Now() - _selfDashStart) / DashSeconds;
        var pos = _selfDashFrom.Lerp(_selfDashTo, (float)Mathf.Min(t, 1.0));
        var (koX, koZ) = WorldToKo(pos);
        pos = GroundPos(koX, koZ, pos.Y - _selfLift, _selfLift);
        _self.Position = pos;
        _lastFreePos = pos;
        _myKoX = koX;
        _myKoZ = koZ;
        _myKoY = pos.Y - _selfLift;
        if (_selfBody != null) _selfBody.Velocity = Vector3.Zero;
        if (t >= 1.0) _selfDashStart = double.NegativeInfinity;
        return true;
    }
}
