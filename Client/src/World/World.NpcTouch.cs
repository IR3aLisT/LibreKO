using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private double _touchCooldown;
    private double _tremorClock;
    private double _touchLockUntil;

    private bool TouchLocked() => Now() < _touchLockUntil;

    private void NpcTouchTick(double delta)
    {
        if (_touchCooldown > 0)
        {
            _touchCooldown -= delta;
            return;
        }
        if (_self == null) return;

        var self = _self.GlobalPosition;
        float selfRadius = _ents.TryGetValue(_myId, out var me) ? me.Radius : DefaultBodyRadius;
        Tremor strongest = default;
        Node3D? source = null;
        bool touched = false;
        foreach (var (id, e) in _ents)
        {
            if (!e.IsNpc || e.Dead || !IsInstanceValid(e.Body) || NpcTouch.Ignored(e.ModelId) || !Restriction.Touch.Contains(e.NpcId))
                continue;
            float distance = e.Body.GlobalPosition.DistanceTo(self);
            var tremor = NpcTouch.TremorFrom(e.ModelId, distance, EntityMoving(e));
            if (tremor.Power > strongest.Power)
            {
                strongest = tremor;
                source = e.Body;
            }
            float radius = e.Radius > 0f ? e.Radius : NpcTouch.UnknownRadius;
            if (_selfDead || distance >= NpcTouch.ContactDistance(e.ModelId, selfRadius, radius)) continue;
            if (!NpcTouch.HurtsOnContact(e.ModelId, e.Nation == Net.I.Nation)) continue;

            Net.I.SendNpcTouch(id);
            _touchCooldown = NpcTouch.Cooldown;
            touched = true;
            if (NpcTouch.PicksUpCube(e.ModelId, _zone))
            {
                StopSelfMoving();
                Net.I.SendNpcEvent(id);
            }
            if (e.ModelId == NpcTouch.SawBladeModel)
            {
                StopSelfMoving();
                _touchLockUntil = Now() + NpcTouch.SawBladeLockSeconds;
            }
        }

        _tremorClock += delta;
        if (strongest.Power <= 0f || (_tremorClock <= NpcTouch.TremorInterval && !touched)) return;
        _tremorClock = 0;
        ShakeCamera(NpcTouch.TremorSeconds, NpcTouch.Amplitude(strongest, touched), NpcTouch.TremorRate);
        Audio.PlayAt(NpcTouch.TremorSound, source);
    }

    private void StopSelfMoving()
    {
        _hasMoveTarget = false;
        _terrainMoveHeld = false;
        _autoMoveForward = false;
    }
}
