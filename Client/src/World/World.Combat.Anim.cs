using System.Collections.Generic;
using Godot;

namespace LibreKO;

public partial class World
{
    private static int AttackSlot(int[]? gear)
    {
        var (right, left, weight) = HandLoadout(gear);
        return WeaponAnimation.AttackAnim(right, left, weight, n => (int)(GD.Randi() % (uint)n));
    }

    private static int StanceIdleSlot(int[]? gear)
    {
        var (right, left, weight) = HandLoadout(gear);
        return WeaponAnimation.BreathBase(right, left, weight);
    }

    private static string[] AttackClips(int[]? gear)
    {
        var (kind, leftKind, _) = HandLoadout(gear);
        if (WeaponAnimation.IsOneHandBlade(kind) && WeaponAnimation.IsOneHandBlade(leftKind))
            return DualAttackClips;
        if (kind == WeaponAnimation.Crossbow) return CrossbowAttackClips;
        if (leftKind is WeaponAnimation.Bow or WeaponAnimation.LongBow
            || kind is WeaponAnimation.Bow or WeaponAnimation.LongBow) return BowAttackClips;
        return kind switch
        {
            WeaponAnimation.NoItem or 0 => UnarmedAttackClips,
            11 => DaggerAttackClips,
            21 => BasicAttackClips,
            22 => TwoHandAttackClips,
            31 or 32 => AxeAttackClips,
            41 or 181 => BluntAttackClips,
            42 => TwoBluntAttackClips,
            51 => SpearAttackClips,
            52 => PolearmAttackClips,
            110 => StaffAttackClips,
            140 => JamadarAttackClips,
            _ => BasicAttackClips,
        };
    }

    private static string[] ClipsForCast(SkillData.Skill? s) => s == null ? MeleeSkillClips : s.Type1 switch
    {
        1 or 2 => MeleeSkillClips,
        3 => MagicCastClips,
        _ => BuffClips,
    };

    private const int TransformedSkillAnim = 4;

    private int SkillAnim(int entityId, SkillData.Skill s, bool release)
    {
        int[]? gear = GearOf(entityId);
        int anim = SkillAnimation.Resolve(s, HeldItemClass(gear, 6), HeldItemClass(gear, 7), release);
        return anim != SkillAnimation.None && WearsMonsterLook(entityId) ? TransformedSkillAnim : anim;
    }

    private bool WearsMonsterLook(int entityId) =>
        TransformHost(entityId)?.GetNodeOrNull<Node3D>(TransformNodeName) is { } worn
        && !worn.GetMeta(WornAccessoriesMeta, false).AsBool();

    private const float StrikeTailPad = 0.08f;

    private static float LastStrikeTime(AnimationPlayer? anim)
    {
        if (anim == null) return 0f;
        string name = anim.CurrentAnimation.ToString();
        if (name.Length == 0 || AnimationMetaFor(anim, name) is not { } meta) return 0f;
        float last = 0f;
        for (int slot = 0; slot < 2; slot++)
            last = Mathf.Max(last, meta.StrikeTime(slot));
        return last + (float)BlendHold.Remaining(anim);
    }

    private static float FirstStrikeTime(AnimationPlayer? anim)
    {
        if (anim == null) return 0f;
        string name = anim.CurrentAnimation.ToString();
        if (name.Length == 0 || AnimationMetaFor(anim, name) is not { } meta) return 0f;
        float first = 0f;
        for (int slot = 0; slot < 2; slot++)
        {
            float at = meta.StrikeTime(slot);
            if (at > 0f && (first <= 0f || at < first)) first = at;
        }
        return first > 0f ? first + (float)BlendHold.Remaining(anim) : first;
    }

    private static double ActionLength(Animation? clip, double blend) =>
        clip == null ? ActionClip.MissingClip : ActionClip.Duration(clip.Length, blend);

    private static double PlayActionOn(AnimationPlayer? anim, string[] candidates)
    {
        if (anim == null) return 0;
        string? name = Pick(anim, candidates);
        if (name == null) return 0;
        return StartAction(anim, name, AnimationMetaFor(anim, name)?.Blend ?? ActionClip.DefaultBlend);
    }

    internal static double StartAction(AnimationPlayer anim, string name, double blend)
    {
        var res = anim.GetAnimation(name);
        if (res != null)
        {
            res.LoopMode = Animation.LoopModeEnum.None;
            if (ActionClip.IsStaticPose(res.Length)) res.Length = (float)ActionClip.Hold(res.Length, blend);
        }
        BlendHold.Play(anim, name, blend);
        CrowdAnimator.NotifyPlay(anim, name, blend);
        return ActionLength(res, blend);
    }

    private const int ActionRankStruck = 0;
    private const int ActionRankBasic = 1;
    private const int ActionRankPosture = 2;
    private const int ActionRankSkill = 3;
    private const int ActionRankDeath = 4;

    private static bool CanPlayAction(int rank, int current, double until, double now)
    {
        if (now >= until) return true;
        if (current == ActionRankDeath) return false;
        return rank >= ActionRankBasic;
    }

    private bool CanPlaySelfAction(int rank, double now) =>
        CanPlayAction(rank, _selfActionRank, _selfActionUntil, now);

    private static bool CanPlayEntityAction(Ent e, int rank, double now) =>
        CanPlayAction(rank, e.ActionRank, e.ActionUntil, now);

    private const int NoActionAnim = int.MinValue;

    private int _selfStrikeAnim = NoActionAnim;

    private void BeginSelfAction(double len, int rank, double now, int anim = NoActionAnim)
    {
        _selfActionUntil = now + Mathf.Min((float)len, ActionClipCap);
        _selfActionRank = rank;
        _selfActionAnim = anim;
        _selfClip = null;
    }

    private static void BeginEntityAction(Ent e, double len, int rank, double now, int anim = NoActionAnim)
    {
        ResetEntityActionScale(e);
        e.ActionUntil = now + Mathf.Min((float)len, ActionClipCap);
        e.ActionRank = rank;
        e.ActionAnim = anim;
        e.ActionClip = "action";
        e.Clip = null;
    }

    private readonly System.Collections.Generic.Dictionary<int, int> _remoteCastingSkill = new();

    private static void ResetEntityActionScale(Ent e)
    {
        if (e.ActionTimeScale == 1f) return;
        e.ActionTimeScale = 1f;
        if (e.Anim != null && e.Anim.SpeedScale != 1f) e.Anim.SpeedScale = 1f;
    }

    private void HoldEntityCastPose(int casterId, SkillData.Skill s)
    {
        if (!s.RootsCaster || s.CastSeconds <= CastStretchMinSeconds) return;
        if (!_ents.TryGetValue(casterId, out var e) || e.Anim == null || e.Dead) return;
        double now = Now();
        double clipLeft = e.ActionUntil - now;
        if (clipLeft <= 0) return;
        e.ActionUntil = now + s.CastSeconds;
        e.ActionTimeScale = (float)(clipLeft / s.CastSeconds);
        if (!e.AnimThrottled) e.Anim.SpeedScale = e.ActionTimeScale;
    }

    private void EndEntityCastPose(int casterId)
    {
        if (!_ents.TryGetValue(casterId, out var e) || e.ActionTimeScale == 1f) return;
        ResetEntityActionScale(e);
        e.ActionUntil = 0;
    }

    private void PlaySkillAction(int entityId, int action, string[] fallback, int rank, bool restart = false)
    {
        if (action < 0) return;
        double now = Now();
        if (entityId == _myId)
        {
            if (_selfDead || _selfAnim == null) return;
            if (now < _selfActionUntil && action == _selfActionAnim) return;
            if (!CanPlaySelfAction(rank, now)) return;
            double len = PlayActionOn(_selfAnim, action, fallback);
            if (len > 0 && rank == ActionRankSkill && Net.I.GenieRunning && GenieComboOn) len *= GenieClipScale();
            if (len > 0)
            {
                BeginSelfAction(len, rank, now, action);
                _selfStrikeAnim = action;
            }
        }
        else if (_ents.TryGetValue(entityId, out var e) && e.Anim != null && !e.Dead)
        {
            if (!restart && now < e.ActionUntil && action == e.ActionAnim) return;
            if (!CanPlayEntityAction(e, rank, now)) return;
            double len = PlayActionOn(e.Anim, action, fallback);
            if (len > 0)
            {
                BeginEntityAction(e, len, rank, now, action);
                e.StrikeAnim = action;
            }
        }
    }

    private double GenieClipScale()
    {
        var gear = SelfGear();
        int right = gear.Length > InventoryConstants.VisRightHand ? ItemData.Get(gear[InventoryConstants.VisRightHand])?.Kind ?? WeaponAnimation.None : WeaponAnimation.None;
        int left = gear.Length > InventoryConstants.VisLeftHand ? ItemData.Get(gear[InventoryConstants.VisLeftHand])?.Kind ?? WeaponAnimation.None : WeaponAnimation.None;
        return GenieClip.Scale(right, left);
    }

    private void SelfAction(string[] candidates, int rank)
    {
        if (_selfDead) return;
        if (_selfAnim == null) return;
        double now = Now();
        if (!CanPlaySelfAction(rank, now)) return;
        bool basicAttack = candidates == BasicAttackClips;
        double len;
        int strikeAnim = NoActionAnim;
        if (basicAttack)
        {
            var gear = SelfGear();
            strikeAnim = AttackSlot(gear);
            len = PlayActionOn(_selfAnim, strikeAnim, AttackVariant(AttackClips(gear)));
        }
        else len = PlayActionOn(_selfAnim, candidates);
        if (len > 0)
        {
            BeginSelfAction(len, rank, now);
            _selfStrikeAnim = strikeAnim;
        }
    }

    private void PlayEntityAction(Ent e, string[] candidates, int rank)
    {
        if (e.Anim == null || e.Dead) return;
        double now = Now();
        if (!CanPlayEntityAction(e, rank, now)) return;
        bool playerSwing = candidates == BasicAttackClips && !e.IsNpc;
        int strikeAnim = playerSwing ? AttackSlot(e.Gear) : NoActionAnim;
        double len = playerSwing
            ? PlayActionOn(e.Anim, strikeAnim, AttackVariant(AttackClips(e.Gear)))
            : PlayActionOn(e.Anim, candidates == BasicAttackClips ? AttackVariant(candidates) : candidates);
        if (len <= 0) return;
        BeginEntityAction(e, len, rank, now);
        e.StrikeAnim = strikeAnim;
        if (candidates == BasicAttackClips && !e.IsNpc)
            e.CombatStanceUntil = e.ActionUntil + 3.0;
    }

    private static int StruckAnim(bool npc) =>
        (npc ? SkillAnimation.NpcStruck0 : SkillAnimation.Struck0)
        + (int)(GD.Randi() % SkillAnimation.StruckPoses);

    private const uint NpcFlinchOneIn = 10;

    private void PlayStruck(int entityId)
    {
        double now = Now();
        bool monsterLook = WearsMonsterLook(entityId);
        if (entityId == _myId)
        {
            if (_selfFlinch != null && !monsterLook)
            {
                if (SelfFlinchReady(now)) PlayFlinch(_selfFlinch, _selfAnim);
                return;
            }
            if (monsterLook && GD.Randi() % NpcFlinchOneIn != 0) return;
            PlaySkillAction(entityId, StruckAnim(monsterLook), StruckClips, ActionRankStruck);
            return;
        }

        if (!_ents.TryGetValue(entityId, out var e)) return;
        if (e.Flinch != null && !monsterLook)
        {
            if (EntityFlinchReady(e, now)) PlayFlinch(e.Flinch, e.Anim);
            return;
        }
        bool monsterClips = e.IsNpc || monsterLook;
        if (monsterClips && GD.Randi() % NpcFlinchOneIn != 0) return;
        PlaySkillAction(entityId, StruckAnim(monsterClips), StruckClips, ActionRankStruck);
    }

    private const int MoveAttackWeaponAnim = 14;
    private const int MoveAttackBareAnim = 15;

    private bool TryMoveAttack(Ent a)
    {
        if (a.IsNpc || a.Flinch == null || a.Anim == null || !EntityMoving(a)) return false;
        int pose = HandLoadout(a.Gear).Right != WeaponAnimation.NoItem ? MoveAttackWeaponAnim : MoveAttackBareAnim;
        string? name = AnimationNameAt(a.Anim, pose);
        if (name == null || !a.Anim.HasAnimation(name) || a.Anim.GetAnimation(name) is not { } clip) return false;
        var meta = AnimationMetaAt(a.Anim, pose);
        a.Flinch.PlayThrough(pose, clip, meta?.Blend ?? ActionClip.DefaultBlend);
        double now = Now();
        float swing = meta?.SoundTime(0) ?? -1f;
        float strike = meta?.StrikeTime(0) ?? -1f;
        a.MoveSwingAt = swing >= 0f ? now + swing : -1;
        a.MoveStrikeAt = strike >= 0f ? now + strike : -1;
        return true;
    }

    private double _selfMoveSwingAt = -1;
    private double _selfMoveStrikeAt = -1;

    private bool SelfMoveAttackReady() =>
        _selfMoving && _selfFlinch != null && _selfAnim != null && !IsWearingTransformLook(_myId);

    private bool SelfMoveAttack(int target, double now, out float lastStrike)
    {
        lastStrike = 0f;
        int pose = HandLoadout(SelfGear()).Right != WeaponAnimation.NoItem ? MoveAttackWeaponAnim : MoveAttackBareAnim;
        string? name = AnimationNameAt(_selfAnim!, pose);
        if (name == null || !_selfAnim!.HasAnimation(name) || _selfAnim.GetAnimation(name) is not { } clip) return false;
        var meta = AnimationMetaAt(_selfAnim, pose);
        _selfFlinch!.PlayThrough(pose, clip, meta?.Blend ?? ActionClip.DefaultBlend);
        float swing = meta?.SoundTime(0) ?? -1f;
        float strike = meta?.StrikeTime(0) ?? -1f;
        _selfMoveSwingAt = swing >= 0f ? now + swing : -1;
        _selfMoveStrikeAt = strike >= 0f ? now + strike : -1;
        _selfStrikeAnim = pose;
        _swingTarget = target;
        if (target >= 0) _swingCommitAt = now + (strike > 0f ? strike : SwingCommitFallback);
        lastStrike = strike > 0f ? strike : (float)clip.Length;
        return true;
    }

    private void SelfMoveAttackTick(double now)
    {
        if (_selfMoveSwingAt >= 0 && now >= _selfMoveSwingAt)
        {
            _selfMoveSwingAt = -1;
            AudioSelfSwing();
        }
        if (_selfMoveStrikeAt >= 0 && now >= _selfMoveStrikeAt)
        {
            _selfMoveStrikeAt = -1;
            SelfStrike();
        }
    }

    private void MoveAttackTick(Ent e, double now)
    {
        if (e.MoveSwingAt >= 0 && now >= e.MoveSwingAt)
        {
            e.MoveSwingAt = -1;
            PlayEntitySound(e, SwingSound(e.Gear, LooksOf(e)), SoundOwnerKind.Swing);
        }
        if (e.MoveStrikeAt >= 0 && now >= e.MoveStrikeAt)
        {
            e.MoveStrikeAt = -1;
            EntityStrike(e);
        }
    }

    private static void PlayFlinch(Flinch layer, AnimationPlayer? anim)
    {
        if (anim == null) return;
        int pose = StruckAnim(npc: false);
        string? name = AnimationNameAt(anim, pose);
        if (name == null || !anim.HasAnimation(name)) return;
        if (anim.GetAnimation(name) is not { } clip) return;
        layer.Play(pose, clip, AnimationMetaAt(anim, pose)?.Blend ?? ActionClip.DefaultBlend);
    }

    private bool SelfFlinchReady(double now) =>
        !_selfDead && !_selfSitting && !SelfCasting(now)
        && !(_selfActionRank is ActionRankPosture or ActionRankDeath && now < _selfActionUntil);

    private static bool EntityFlinchReady(Ent e, double now) =>
        !e.Dead && !e.Sitting && !e.AnimPaused
        && !(e.ActionRank is ActionRankSkill or ActionRankPosture or ActionRankDeath && now < e.ActionUntil);

    private bool _selfCombatStance;

    private bool SelfCombatStanceReady() =>
        !_selfDead
        && _selectedId >= 0
        && _ents.TryGetValue(_selectedId, out var target)
        && target.Attackable
        && !target.Dead;

    private void CombatStanceTick()
    {
        bool ready = SelfCombatStanceReady();
        if (ready == _selfCombatStance) return;
        _selfCombatStance = ready;
        Net.I.SendCombatStance(ready);
    }

    private static string[] AttackVariant(string[] clips)
    {
        if (clips.Length < 2 || (GD.Randi() & 1) == 0) return clips;
        var varied = (string[])clips.Clone();
        (varied[0], varied[1]) = (varied[1], varied[0]);
        return varied;
    }

    private void FaceToward(int attackerId, int targetId, bool instant = false)
    {
        Vector3? tgt = WorldPosOf(targetId);
        if (tgt == null) return;
        if (attackerId == _myId && _self != null)
        {
            if (instant) FaceSelfToward(tgt.Value);
            else TurnSelfToward(tgt.Value);
        }
        else if (_ents.TryGetValue(attackerId, out var a)) FaceEntityToward(a, tgt.Value, instant);
    }

    private void FaceTowardImpact(int casterId, short[] data)
    {
        if (data.Length <= 2 || (data[0] == 0 && data[2] == 0)) return;
        Vector3 impact = GroundPos(data[0], data[2], 0f, 0f);
        if (casterId == _myId && _self != null) TurnSelfToward(impact);
        else if (_ents.TryGetValue(casterId, out var a)) FaceEntityToward(a, impact);
    }

    private float? _selfYawGoal;

    private void FaceSelfToward(Vector3 target)
    {
        if (FlatDirectionTo(target) is not { } dir) return;
        _faceDir = dir;
        SnapSelfToFaceDir();
    }

    private void TurnSelfToward(Vector3 target)
    {
        if (FlatDirectionTo(target) is not { } dir) return;
        _faceDir = dir;
        _selfYawGoal = SelfYawOf(dir);
    }

    private void SnapSelfToFaceDir()
    {
        _selfYawGoal = null;
        _self.RotationDegrees = new Vector3(0, SelfYawOf(_faceDir), 0);
    }

    private void TickSelfFacing(float dt)
    {
        if (_selfYawGoal is not { } goal || _self == null) return;
        float yaw = Turning.Toward(_self.RotationDegrees.Y, goal, dt);
        _self.RotationDegrees = new Vector3(0, yaw, 0);
        if (yaw == goal) _selfYawGoal = null;
    }

    private Vector3? FlatDirectionTo(Vector3 target)
    {
        var d = target - _self.Position;
        d.Y = 0;
        return d.LengthSquared() < 0.01f ? null : d.Normalized();
    }

    private static float SelfYawOf(Vector3 dir) => 180f - Coord.KoHeading(-dir.X, dir.Z);

    private void FaceEntityToward(Ent e, Vector3 to, bool instant = false)
    {
        if (YawToward(e.Body.Position, to) is { } yaw) FaceEntity(e, yaw, instant);
    }

    private void FaceNodeToward(Node3D node, Vector3 from, Vector3 to)
    {
        if (YawToward(from, to) is { } yaw) node.RotationDegrees = new Vector3(0, yaw, 0);
    }

    private float? YawToward(Vector3 from, Vector3 to)
    {
        var (fx, fz) = WorldToKo(from);
        var (tx, tz) = WorldToKo(to);
        float dx = tx - fx, dz = tz - fz;
        if (dx * dx + dz * dz < 0.01f) return null;
        return 180f - Coord.KoHeading(dx, dz);
    }

    private Vector3? WorldPosOf(int id) =>
        id == _myId ? _self?.Position :
        _ents.TryGetValue(id, out var e) ? e.Body.Position : null;
}
