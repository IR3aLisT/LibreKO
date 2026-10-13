using Godot;
using LibreKO.Domain;

namespace LibreKO;

public partial class World
{
    private const double KillingBlowGrace = 0.15;
    private const double KillingBlowCap = 1.5;

    private void BeginDying(Ent e)
    {
        e.Dead = true;
        double now = Now();
        e.DeathCheckAt = now + KillingBlowGrace;
        e.DeathDueAt = now + KillingBlowCap;
    }

    private void TickDying(Ent e, double now)
    {
        if (now < e.DeathCheckAt) return;
        if (now < e.DeathDueAt && KillingBlowPending(e.Id)) return;
        FinishDying(e);
    }

    private void FinishDying(Ent e)
    {
        if (e.DeathDueAt <= 0) return;
        e.DeathDueAt = 0;
        var pose = e.KillStriker >= 0 ? DeathPoses.ForKillerKind(HeldItemClass(GearOf(e.KillStriker), InventoryConstants.VisRightHand)) : DeathPose.KeepPosition;
        var killer = e.KillStriker == _myId ? _self : _ents.TryGetValue(e.KillStriker, out var k) ? k.Body : null;
        e.KillStriker = -1;
        float cry = LayOutCorpse(e, settled: false, pose);
        double now = Now();
        if (cry > 0f) e.DeathCryAt = now + cry;
        else AudioDeath(e.Id);
        if (killer == null || !IsInstanceValid(killer) || !DeathPoses.Pushes(pose, e.ModelId)) return;
        var away = e.Body.GlobalPosition - killer.GlobalPosition;
        away.Y = 0f;
        if (away.LengthSquared() <= 0f) return;
        e.PushDir = away.Normalized();
        e.PushStart = now;
    }

    private void TickCorpse(Ent e, double now, double delta)
    {
        if (e.DeathCryAt > 0 && now >= e.DeathCryAt)
        {
            e.DeathCryAt = 0;
            AudioDeath(e.Id);
        }
        if (e.PushStart < 0) return;
        float seconds = (float)(now - e.PushStart);
        float step = DeathPoses.PushStep(seconds, e.Radius * e.Body.Scale.Y, (float)delta);
        if (step <= 0f)
        {
            if (seconds >= DeathPoses.PushEndSeconds) e.PushStart = -1;
            return;
        }
        var moved = e.Body.Position + e.PushDir * step;
        var (koX, koZ) = WorldToKo(moved);
        e.Body.Position = GroundPosFor(e.IsNpc, koX, koZ, e.KoY, e.Lift);
    }

    private static bool Dying(Ent e) => e.DeathDueAt > 0;

    private bool KillingBlowPending(int victimId)
    {
        if (_strikeTargetSelf == victimId && StrikeStillAhead(_selfAnim)) return true;
        foreach (var attacker in _ents.Values)
            if (attacker.StrikeTarget == victimId && !attacker.Dead && StrikeStillAhead(attacker.Anim)) return true;
        return false;
    }

    private static bool StrikeStillAhead(AnimationPlayer? anim)
    {
        if (anim == null) return false;
        if (!CrowdAnimator.Current(anim, out string name, out double pos))
        {
            name = anim.CurrentAnimation.ToString();
            pos = name.Length == 0 ? 0 : anim.CurrentAnimationPosition;
        }
        if (name.Length == 0 || AnimationMetaFor(anim, name) is not { } meta) return false;
        for (int slot = 0; slot < SoundSlots; slot++)
        {
            float at = meta.StrikeTime(slot);
            if (at >= 0f && at > pos) return true;
        }
        return false;
    }
}
