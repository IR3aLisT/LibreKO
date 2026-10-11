using System.Collections.Generic;
using Godot;

namespace LibreKO;

public partial class World
{
    private const float SwingCommitFallback = 0.3f;
    private const float ActionClipCap = 2.5f;
    private const float ProjectileFxSpeed = 18f;
    private const double CorpseLinger = 5.0;

    private const int PendingAwaitServer = 0;
    private const int PendingFlying = 2;
    private const int PendingEffecting = 3;
    private const double PendingReplyTimeout = 5.0;
    private const short ArrowMissedMarker = MagicFail.ProjectileKilled;
    private const int MissMarkerSlot = 3;

    private static short[] ArrowMissedData()
    {
        var data = new short[7];
        data[MissMarkerSlot] = ArrowMissedMarker;
        return data;
    }

    internal Vitals Vitals => Net.I.Vitals;
    private bool _autoAttack;
    private int _autoTargetId = -1;
    private readonly HashSet<int> _castLogged = new();
    private double _nextSwing;
    private int _swingTarget = -1;
    private double _swingCommitAt;
    private double _nextSwingIfCancelled;

    private const double VolleyHitGapSeconds = 0.12;
    private const double ComboLen = 1.0;
    private const double ComboGrace = 1.0;
    private double _comboStepLen = ComboLen;
    private int _comboStep;
    private double _comboPlayingUntil;
    private double _comboExpireAt;
    private bool _comboBuffered;

    private Dictionary<int, double> _skillReady => Net.I.SkillCooldowns;

    private struct PendingCast
    {
        public double EffectTime, ReplyDeadline;
        public int SkillId, TargetId, Stage;
        public short[]? Data;
        public bool Straight;
        public Vector3 From, To;
    }
    private readonly List<PendingCast> _pendingCasts = new();
    private readonly List<PendingCast> _launches = new();
    private readonly Dictionary<(int Caster, int Skill, int Phase), List<Node3D>> _skillFx = new();

    private static double Now() => Time.GetTicksMsec() / 1000.0;

    private void CombatInit()
    {
        SkillData.EnsureLoaded();
        Net.I.ItemStatsEvent += OnItemStats;
        _selfCombatStance = false;
        Net.I.SendCombatStance(false);
        RestoreBuffs();
    }

    private readonly List<(double At, int Caster, int Skill, int Phase)> _skillFxStops = new();
    private const double CastFxGraceSeconds = 3.0;

    private void ScheduleSkillFxStop(int casterId, int skillId, int phase, double at) =>
        _skillFxStops.Add((at, casterId, skillId, phase));

    private void TickSkillFxStops(double now)
    {
        for (int i = _skillFxStops.Count - 1; i >= 0; i--)
        {
            var stop = _skillFxStops[i];
            if (now < stop.At) continue;
            _skillFxStops.RemoveAt(i);
            StopSkillFx(stop.Caster, stop.Skill, stop.Phase);
        }
    }

    private void CombatTick(double now)
    {
        if (!_selfDead && Vitals.Known && Vitals.Hp <= 0) EnterSelfDeath();
        TickSkillFxStops(now);

        AdvancedGenieTick(now);
        _launches.Clear();
        for (int i = _pendingCasts.Count - 1; i >= 0; i--)
        {
            var pc = _pendingCasts[i];
            if (pc.Stage == PendingAwaitServer)
            {
                if (now < pc.ReplyDeadline) continue;
                _pendingCasts.RemoveAt(i);
                EndCast(pc.SkillId);
                continue;
            }
            if (now < pc.EffectTime) continue;
            if (pc.Stage == PendingFlying)
            {
                Net.I.SendMagic(2, pc.SkillId, pc.TargetId, pc.Data);
                _launches.Add(pc);
            }
            else
            {
                if (pc.Straight)
                {
                    pc.TargetId = FirstHostileAlong(pc.From, pc.To);
                    if (pc.TargetId < 0)
                    {
                        Net.I.SendMagic(4, pc.SkillId, AreaImpactTarget, ArrowMissedData());
                        _pendingCasts.RemoveAt(i);
                        if (!HasPendingCast(pc.SkillId)) EndCast(pc.SkillId);
                        continue;
                    }
                }
                Net.I.SendMagic(3, pc.SkillId, pc.TargetId, DashRequestData(pc.SkillId, pc.TargetId) ?? KickRequestData(pc.SkillId, pc.TargetId) ?? pc.Data);
            }
            pc.Stage = PendingAwaitServer;
            pc.EffectTime = double.PositiveInfinity;
            pc.ReplyDeadline = now + PendingReplyTimeout;
            _pendingCasts[i] = pc;
        }
        foreach (var launch in _launches)
            if (SkillData.Get(launch.SkillId) is { } launched)
                LaunchFlight(_myId, launch.TargetId, launched, launch.Data ?? new short[7], own: true);

        if (_comboBuffered && !_selfDead && now >= _comboPlayingUntil) ComboHit(now);
        TickSwingCommit(now);
        TickAutoAttack(now);

        ReapCorpses(now);
        CueReadySkills(now);
        UpdateHotbarReady(now);
        PetBarTick(now);
        BuffBarTick(now);
        BlindTick(now);
        PotionBarTick(now);
        _touchActions?.SetAutoAttack(CanAutoAttack(), _autoAttack);
        ExpBarStatsTick(now);
    }

    private readonly List<int> _corpseScratch = new();

    private void ReapCorpses(double now)
    {
        _corpseScratch.Clear();
        foreach (var (id, e) in _ents)
            if (e.Dead && e.CorpseRemoveAt > 0 && now >= e.CorpseRemoveAt)
                _corpseScratch.Add(id);
        foreach (var id in _corpseScratch)
        {
            if (_selectedId == id) Deselect();
            RemoveEntity(id);
        }
    }

    private bool CanAutoAttack() =>
        _selectedId >= 0
        && _ents.TryGetValue(_selectedId, out var target)
        && target.Attackable
        && !target.Dead;

    private void ToggleAutoAttack()
    {
        if (SelfOnSoccerTeam) return;
        if (_autoAttack)
        {
            StopAutoAttack();
            return;
        }
        if (!CanAutoAttack())
        {
            if (_selectedId >= 0 && _ents.ContainsKey(_selectedId))
            {
                FaceSelectedWhenStill();
                return;
            }
            CombatNotice("Select a target first.");
            return;
        }

        FaceSelectedWhenStill();
        StartAutoAttack(_selectedId);
    }

    private readonly AttackChain _attackChain = new();

    private void StartAutoAttack(int targetId)
    {
        if (SelfOnSoccerTeam && OnSoccerPitch()) return;
        _attackChain.Record(AttackChain.BasicAttack, Now());
        _autoAttack = true;
        _autoTargetId = targetId;
        _hasMoveTarget = false;
        _terrainMoveHeld = false;
        CombatLogAdd(SystemText(TextBeginAttack, "Beginning attack on %s", CombatEntityName(targetId)), CombatLogKind.Status);
    }

    private void StopAutoAttack()
    {
        if (_autoAttack) CombatLogAdd(SystemText(TextStopAttack, "Stop Attack"), CombatLogKind.Status);
        _autoAttack = false;
        _autoTargetId = -1;
        CancelSwing();
    }

    private void ArmSwing(int targetId, double now)
    {
        _swingTarget = targetId;
        if (targetId < 0) return;
        float windUp = FirstStrikeTime(_selfAnim);
        _swingCommitAt = now + (windUp > 0f ? windUp : SwingCommitFallback);
    }

    private void TickSwingCommit(double now)
    {
        if (_swingTarget < 0 || now < _swingCommitAt) return;
        int target = _swingTarget;
        _swingTarget = -1;
        if (_selfDead || IsStunned()) return;
        if (!_ents.TryGetValue(target, out var t) || t.Dead || !t.Attackable) return;
        Net.I.SendAttack(target, SwingDelay(), chained: _attackChain.Chained);
    }

    private short SwingDelay()
    {
        var gear = SelfGear();
        int right = gear.Length > 6 ? gear[6] : 0;
        int left = gear.Length > 7 ? gear[7] : 0;
        int hand = right != 0 ? right : left;
        int delay = hand == 0 ? 0 : ItemData.Get(hand)?.Delay ?? 0;
        return (short)Mathf.Max(BareHandedSwingDelay, delay);
    }

    private const int BareHandedSwingDelay = 100;

    private float MeleeSwingInterval()
    {
        var gear = SelfGear();
        int right = gear.Length > InventoryConstants.VisRightHand ? gear[InventoryConstants.VisRightHand] : 0;
        int delay = right == 0 ? 0 : ItemData.Get(right)?.Delay ?? 0;
        int percent = right == 0 ? 0 : ItemData.ExtFor(right)?.AttackIntervalPercent ?? 0;
        return SwingTiming.IntervalSeconds(delay, percent, AttackSpeedMultiplier());
    }

    private void CancelSwing()
    {
        _comboBuffered = false;
        if (_selfActionRank == ActionRankBasic && Now() < _selfActionUntil)
        {
            _selfActionUntil = 0;
            _selfClip = null;
            _strikeTargetSelf = -1;
        }
        bool uncommitted = CancelBasicRangedShot();
        if (_swingTarget >= 0)
        {
            uncommitted = true;
            _swingTarget = -1;
        }
        if (!uncommitted) return;
        _nextSwing = _nextSwingIfCancelled;
        _comboPlayingUntil = 0;
        _comboExpireAt = 0;
        _comboStep = 0;
    }

    private bool CancelBasicRangedShot()
    {
        if (_selfDead || _pendingCasts.Count == 0) return false;
        if (BasicRangedAttackSkill() is not { } ranged) return false;
        if (!PendingCastPreEffect(ranged.Id)) return false;
        CancelSelfCast(ranged.Id);
        return true;
    }

    private void TickAutoAttack(double now)
    {
        if (!_autoAttack)
            return;
        if (_selfDead
            || _selectedId != _autoTargetId
            || !_ents.TryGetValue(_autoTargetId, out var target)
            || !target.Attackable
            || target.Dead)
        {
            StopAutoAttack();
            return;
        }

        if (!InBasicAttackRange(_autoTargetId) || now < _nextSwing)
            return;

        var rangedSkill = BasicRangedAttackSkill();
        _nextSwingIfCancelled = _nextSwing;
        _nextSwing = now + (rangedSkill != null ? rangedSkill.CastSeconds + RangedSwingPad : MeleeSwingInterval());
        BasicAttack();
    }

    private float BasicAttackRange(Ent? target)
    {
        int ranged = RangedWeaponItem();
        if (ranged != 0)
            return (ItemData.Get(ranged)?.Range ?? 0) / WeaponRangeScale + (target?.Radius ?? 0f);
        float self = _ents.TryGetValue(_myId, out var me) ? me.Radius : DefaultBodyRadius;
        return SkillReach.BasicMelee(self, target?.Radius ?? 0f, MeleeWeaponReach(), _zone);
    }

    private float MeleeWeaponReach() => HandWeaponReach(rightFirst: true);

    private float HandWeaponReach(bool rightFirst)
    {
        var gear = SelfGear();
        int right = gear.Length > 6 ? gear[6] : 0;
        int left = gear.Length > 7 ? gear[7] : 0;
        int hand = rightFirst ? (right != 0 ? right : left) : (left != 0 ? left : right);
        return hand == 0 ? 0f : (ItemData.Get(hand)?.Range ?? 0) / WeaponRangeScale;
    }

    private const float WeaponRangeScale = 10f;
    private const float ApproachFraction = 0.8f;
    private const float RangedSwingPad = 0.15f;

    private void AimAttack(double now)
    {
        if (_selectedId >= 0 && _ents.TryGetValue(_selectedId, out var e))
            FaceSelfToward(e.Body.Position);
        else if (_faceDir.LengthSquared() >= 0.01f)
            SnapSelfToFaceDir();
        _attackLungeDir = Vector3.Zero;
        _attackLungeUntil = 0;
    }

    private void BasicAttack()
    {
        if (_selfDead) return;
        if (BasicRangedAttackSkill() is { } ranged)
        {
            if (!HasPendingCast(ranged.Id)) CastSkill(ranged.Id);
            return;
        }
        double now = Now();
        if (now < _comboPlayingUntil) { _comboBuffered = true; return; }
        ComboHit(now);
    }

    private void ComboHit(double now)
    {
        if (now > _comboExpireAt) _comboStep = 0;
        _comboStep = (_comboStep + 1) % 3;
        _comboStepLen = ComboLen;

        AimAttack(now);

        int target =
            _selectedId >= 0
            && _ents.TryGetValue(_selectedId, out var t)
            && t.Attackable
            && !t.Dead
            && InBasicAttackRange(_selectedId)
                ? _selectedId
                : -1;
        _strikeTargetSelf = target;
        SelfAction(BasicAttackClips, ActionRankBasic);
        ArmSwing(target, now);
        _nextSwing = Mathf.Max((float)_nextSwing, (float)now + LastStrikeTime(_selfAnim) + StrikeTailPad);
        _comboPlayingUntil = now + _comboStepLen;
        _comboExpireAt = _comboPlayingUntil + ComboGrace;
        _comboBuffered = false;
    }

    private bool InRange(int id, float radius) =>
        _ents.TryGetValue(id, out var e) && e.Body.GlobalPosition.DistanceTo(_self.GlobalPosition) <= radius;

    private bool InBasicAttackRange(int id) =>
        _ents.TryGetValue(id, out var e)
        && FlatDistance(e.Body.GlobalPosition, _self.GlobalPosition) <= BasicAttackRange(e);

    private const int AttackResultHit = 1;
    private const int AttackResultKill = 2;

    private const int AttackTypeBasic = 1;

    private void OnAttack(int attackType, int result, int attackerId, int targetId, bool chained)
    {
        if (targetId == _myId && attackerId != _myId) BreakCastByChance();
        if (attackerId == _myId || targetId == _myId) _battleUntil = Now() + NestDungeon.BattleSeconds;
        if (attackType == AttackTypeBasic && result == AttackResultHit
            && (attackerId == _myId ? _attackChain.Consume() : chained)
            && Fx.NameForId(AttackChain.Effect(GD.Randi())) is { } chainFx)
            SpawnFxOn(attackerId, chainFx, 0f);
        if (result == AttackResultHit) PlayStruck(targetId);
        if (result == AttackResultKill && _ents.TryGetValue(targetId, out var victim)) victim.KillStriker = attackerId;
        if (attackerId != _myId && _ents.TryGetValue(attackerId, out var a))
        {
            a.StrikeTarget = result is AttackResultHit or AttackResultKill ? targetId : -1;
            if (!TryMoveAttack(a)) PlayEntityAction(a, BasicAttackClips, ActionRankBasic);
        }
        FaceToward(attackerId, targetId);
    }

    private void OnMagic(int sub, int skillId, int casterId, int targetId, short[] data)
    {
        var s = SkillData.Get(skillId);
        switch (sub)
        {
            case 1:
                if (Diag.SlowLog) GD.Print($"[fx] casting skill={skillId} caster={casterId} selfFx1={s?.SelfFx1} part={s?.SelfPart1}");
                ShowSkillMsgOut(SkillMsgOut.Cast(skillId));
                if (targetId < 0) FaceTowardImpact(casterId, data);
                else FaceToward(casterId, targetId);
                if (casterId != _myId) StopSkillFx(casterId, skillId);
                if (casterId == _myId && s != null)
                {
                    if (!_instantCasts.Remove(s.Id)) EnsureSkillCooldown(s);
                    BeginCast(s);
                    LogSkillUse(s);
                }
                if (s != null)
                {
                    bool remoteUse = casterId != _myId;
                    if (remoteUse) _remoteCastingSkill[casterId] = skillId;
                    PlaySkillAction(casterId, SkillAnim(casterId, s, false), ClipsForCast(s), ActionRankSkill, restart: remoteUse);
                    if (remoteUse) HoldEntityCastPose(casterId, s);
                    LatchStrikeTarget(casterId, s.IsMelee ? targetId : -1);
                }
                if (casterId != _myId && s != null) StartCastFx(casterId, s);
                break;

            case 2:
                if (casterId != _myId && s != null) LaunchFlight(casterId, targetId, s, data, own: false);
                break;

            case 3:
                ShowSkillMsgOut(SkillMsgOut.Effect(skillId));
                if (s != null && SecondaryBuff.IsEcho(s, data))
                {
                    OnSecondaryBuffLanded(s, targetId == 0 ? casterId : targetId, SecondaryBuff.Seconds(data),
                        SecondaryBuff.SpeedPercent(data));
                    break;
                }
                bool miss = data.Length > 3 && data[3] <= -100;
                bool dash = s != null && OnDashEffecting(s, casterId, targetId, data);
                int affected = targetId == 0 ? casterId : targetId;
                if (Diag.SlowLog) GD.Print($"[fx] effecting skill={skillId} caster={casterId} target={targetId} miss={miss} targetFx={s?.TargetFx} part={s?.TargetPart} data3={(data.Length > 3 ? data[3] : 0)}");
                StopSkillFx(casterId, skillId, 1);
                bool castFollowUp = _remoteCastingSkill.Remove(casterId, out int castingSkill) && castingSkill == skillId;
                if (s != null && !s.HasFlyingStage && (s.IsMelee || s.SelfAnim2 != 0))
                {
                    PlaySkillAction(casterId, SkillAnim(casterId, s, true), ClipsForCast(s), ActionRankSkill,
                                    restart: casterId != _myId && !castFollowUp);
                    LatchStrikeTarget(casterId, !miss && s.IsMelee ? targetId : -1);
                }
                if (s?.SelfFx2 != null)
                {
                    double buff = BuffSeconds(s, data);
                    bool loops = Fx.LoopsForever(s.SelfFx2) && buff > 0;
                    SpawnOwnedFx(casterId, skillId, 2, s.SelfFx2, s.SelfPart2, oneShot: !loops);
                    if (loops) ScheduleSkillFxStop(casterId, skillId, 2, Now() + buff);
                    AudioFxAt(s.SelfFx2Id, casterId);
                }
                if (!miss && !dash && s?.TargetFx != null)
                {
                    if (SpawnFxAtImpact(casterId, targetId, s.TargetFx, s.TargetPart, data, s.IsAreaCast, s.IsMelee)
                        && !(casterId == _myId && s.IsPotion))
                        AudioFxAt(s.TargetFxId, targetId > 0 ? targetId : casterId);
                }
                if (!miss && s != null && s.TargetAnim != 0 && targetId >= 0)
                    PlaySkillAction(affected, s.TargetAnim, StruckClips, ActionRankStruck);
                if (!miss && targetId >= 0 && Gestures.IsCorpseSkill(skillId)) PlayCorpseJiggle(targetId);
                if (casterId == _myId)
                {
                    bool requested = ResolvePendingReply(skillId);
                    if (!HasPendingCast(skillId)) EndCast(skillId);
                    if (requested && s != null && !HasPendingCast(skillId) && !dash)
                        LogSkillOutcome(s, miss);
                    if (s != null)
                    {
                        if (requested) EnsureSkillCooldown(s);
                        if (!miss && affected == _myId) ApplyMoveSpeedBuff(s, BuffSeconds(s, data));
                    }
                }
                if (!miss && s != null && affected == _myId && casterId != _myId && CastBreak.LightningHurts(CastBreakSkill.Of(s)))
                    BreakCastByChance();
                if (!miss && s != null)
                {
                    RegisterBuff(s, affected, BuffSeconds(s, data));
                    if (affected == _myId && s.Type1 == MagicType.Buff)
                        ApplySelfStatus(s.BuffType, SecondaryBuff.SpeedPercent(data), BuffSeconds(s, data));
                }
                if (!miss && s?.Revives == true && !ResurrectionOnlyOffered(s, data)) Revive(affected);
                break;

            case 4:
            case 6:
                if (sub == MagicSub.Fail && targetId == _myId && casterId != _myId && s != null && CastBreak.InFlightFailBreaks(CastBreakSkill.Of(s), data))
                    BreakCastByChance();
                StopSkillFx(casterId, skillId);
                if (casterId != _myId) EndEntityCastPose(casterId);
                if (casterId == _myId)
                {
                    bool awaited = HasPendingCast(skillId);
                    ClearPendingCast(skillId);
                    _instantCasts.Remove(skillId);
                    EndCast(skillId);
                    if (s != null && (sub != MagicSub.Fail || MagicFail.RefundsCooldown(MagicFail.Code(data), s.FlyingFxId > 0)))
                        CancelSkillCooldown(s);
                    if (sub == MagicSub.Fail) NoteGenieRefusal(skillId);
                    if (s != null && sub == MagicSub.Fail && MagicFail.Code(data) != 0) LogSkillFail(s, data);
                    else if (awaited && s != null) LogSkillRefused(s, sub);
                    _castLogged.Remove(skillId);
                }
                break;
        }
    }

    private void LogSkillUse(SkillData.Skill s)
    {
        if (BasicRangedAttackSkill()?.Id == s.Id || !_castLogged.Add(s.Id)) return;
        CombatLogAdd(s.IsPotion
            ? $"You used {ItemData.DisplayName(s.UseItem)}."
            : $"You used {s.Name}.", CombatLogKind.Status);
    }

    private void LogSkillOutcome(SkillData.Skill s, bool miss)
    {
        LogSkillUse(s);
        _castLogged.Remove(s.Id);
        if (BasicRangedAttackSkill()?.Id == s.Id || s.IsPotion) return;
        if (miss) CombatLogAdd(SystemText(TextMissed, "%s Missed.", s.Name), CombatLogKind.Incoming);
        else CombatLogAdd($"{s.Name} succeeded.", CombatLogKind.Outgoing);
    }

    private void LogSkillFail(SkillData.Skill s, short[] data)
    {
        string text = MagicFail.TextFor(data) switch
        {
            MagicFailText.CastingFailed => SystemText(TextCastingFailed, "Casting failed"),
            MagicFailText.SkillFailed => $"{s.Name} failed.",
            MagicFailText.Missed => SystemText(TextMissed, "%s Missed.", s.Name),
            MagicFailText.NotEnoughMp => SystemText(TextNotEnoughMp, "Skill Failed - Not enough MP"),
            _ => "",
        };
        CombatNotice(text);
    }

    private void LogSkillRefused(SkillData.Skill s, int sub)
    {
        if (sub == MagicSub.Cancel)
        {
            if (_castLogged.Contains(s.Id)) CombatLogAdd($"{s.Name} was cancelled.", CombatLogKind.Status);
            return;
        }
        if (s is { UseItem: not 0, IsRanged: false })
            CombatNotice($"You couldn't use {ItemData.DisplayName(s.ConsumedItem)} right now.");
        else if (ConflictingBuff(s) is { } blocker)
            CombatNotice($"{blocker.Name} is already active.");
        else
            CombatLogAdd($"{s.Name} failed.", CombatLogKind.Incoming);
    }

    private static int BuffSeconds(SkillData.Skill s, short[] data) =>
        s.Type1 is MagicType.Buff or MagicType.Transform or MagicType.Stealth
        && data.Length > 3 && data[3] > 0
            ? data[3]
            : s.Duration;

    private SkillData.Skill? ConflictingBuff(SkillData.Skill s)
    {
        if (HasActiveFragmentOfManes() && (IsSpeedBuff(s) || s.IsBlink))
            return SkillData.Get(FragmentOfManesSkillId);

        if (s.Type1 != MagicType.Buff || s.BuffType == 0) return null;
        double now = Now();
        foreach (var (skillId, end) in Net.I.BuffEnds)
        {
            if (skillId == s.Id || end <= now) continue;
            var other = SkillData.Get(skillId);
            if (other != null && other.BuffType == s.BuffType) return other;
        }
        return null;
    }

    private void OnDead(int victimId, int killerId)
    {
        if (victimId == _myId)
        {
            AudioDeath(victimId);
            if (_ents.TryGetValue(killerId, out var killer) && !killer.IsNpc && killer.Name.Length > 0)
                CombatLogAdd(TextTemplate.Fill(SystemText(CombatLogLines.KilledByText, "You've been killed by %s"), killer.Name),
                    CombatLogKind.Incoming, KilledByColour);
            EnterSelfDeath();
            return;
        }
        if (_ents.TryGetValue(victimId, out var e))
        {
            BeginDying(e);
            if (_autoAttack && _autoTargetId == victimId) StopAutoAttack();
        }
    }

    private void EnterSelfDeath()
    {
        if (_selfDead) return;
        _selfDead = true;
        _tradeInFlight = false;
        ClearStun();
        StopAutoAttack();
        Deselect();
        _pendingCasts.Clear();
        _selfFlinch?.Stop();
        double len = PlayActionOn(_selfAnim, DeathPoses.PlayerKeepClip, DeathClips);
        _selfActionUntil = Now() + Mathf.Max((float)len, 2.5f);
        _selfActionRank = ActionRankDeath;
        if (!RoyaleDeath()) ShowDeathDialog();
    }

    private void OnSelfHp(int hp, int maxHp, int attackerId)
    {
        var (dmg, recovered) = Vitals.ApplyHp(hp, maxHp);
        _hpBar?.Set(hp, maxHp);
        if (_selfDead && hp > 0) OnSelfResurrected();
        if (dmg > 0)
        {
            int shown = CombatLogLines.LoggedHp(_zone, dmg);
            CombatLogAdd(_ents.TryGetValue(attackerId, out var hitter) && !hitter.IsNpc && hitter.Name.Length > 0
                ? TextTemplate.Fill(SystemText(CombatLogLines.PlayerDamageText, "%s is inflicted with %d HP damage."), ShownName(hitter), shown)
                : TextTemplate.Fill(SystemText(CombatLogLines.HpDamageText, "%d HP Damage"), shown), CombatLogKind.Incoming);
        }
        else if (recovered > 0)
            CombatLogAdd(TextTemplate.Fill(SystemText(CombatLogLines.HpRecoveredText, "%d HP Recovered"),
                CombatLogLines.LoggedHp(_zone, recovered)), CombatLogKind.Recovery);
        if (dmg > 0 && _self != null)
        {
            Floaters?.Wound(dmg);
            if (!_selfDead)
            {
                if (_selfSitting) StandUp();
                AudioStruck(_myId);
            }
        }
        else if (recovered > 0 && _self != null)
        {
            if (attackerId >= 0) Floaters?.Cure(_myId, recovered);
            else Floaters?.RegenHp(recovered);
        }
    }

    private void OnSelfStamina(int sp, int maxSp)
    {
        int delta = Vitals.ApplySp(sp, maxSp);
        ShowVpBar(sp, maxSp);
        if (delta < 0)
            CombatLogAdd(TextTemplate.Fill(SystemText(VpUsedText, "%d SP Used"), -delta), CombatLogKind.Spent);
    }

    private const int VpUsedText = 3014;
    private static readonly Color KilledByColour = new("ff8080");

    private void OnSelfMp(int mp, int maxMp)
    {
        int delta = Vitals.ApplyMp(mp, maxMp);
        _mpBar?.Set(mp, maxMp);
        if (delta > 0)
        {
            CombatLogAdd(TextTemplate.Fill(SystemText(CombatLogLines.ManaText(_selfClass, recovered: true), "%d MP Recovered"), delta),
                CombatLogKind.Resource);
            Floaters?.RegenMp(delta);
        }
        else if (delta < 0)
            CombatLogAdd(TextTemplate.Fill(SystemText(CombatLogLines.ManaText(_selfClass, recovered: false), "%d MP Used"), -delta),
                CombatLogKind.Spent);
    }

    private void OnItemStats(DerivedStats stats)
    {
        Vitals.ApplyMaxima(stats.MaxHp, stats.MaxMp);
        _hpBar?.Set(Vitals.Hp, Vitals.MaxHp);
        _mpBar?.Set(Vitals.Mp, Vitals.MaxMp);
    }

    private void Revive(int id)
    {
        if (id == _myId) { OnSelfResurrected(); return; }
        if (!_ents.TryGetValue(id, out var e) || !e.Dead) return;
        e.Dead = false;
        e.DeathDueAt = 0;
        e.CorpseRemoveAt = 0;
        e.ActionUntil = 0;
        e.ActionRank = 0;
        e.ActionClip = null;
        e.Clip = null;
        RefreshEntityCollision(e);
    }

    private void OnSelfResurrected()
    {
        if (!_selfDead) return;
        _selfDead = false;
        _selfActionUntil = 0;
        _selfClip = null;
        CloseResurrectPrompt();
        HideDeathDialog();
        CombatLogAdd("You have been resurrected.", CombatLogKind.Recovery);
    }

    private void OnRegene(float koX, float koZ)
    {
        if (_self == null) return;
        float koY = _myKoY;
        var pos = GroundPos(koX, koZ, koY, _selfLift);
        _self.Position = pos;
        _lastFreePos = pos;
        _myKoX = koX; _myKoZ = koZ; _myKoY = koY;
        _lastKoX = koX; _lastKoZ = koZ;
        _selfDead = false;
        _selfActionUntil = 0;
        EndCast(0);
        ClearAllBuffs();
        _selfClip = null;
        CloseResurrectPrompt();
        HideDeathDialog();
        SpawnRespawnEffect();
        CombatLogAdd("You returned to the battlefield.", CombatLogKind.Status);
    }

    private static readonly string[] BasicAttackClips =
        { "attack_sword0_A", "attack_sword1_A", "attack0", "attack1", "attack",
          "attack_blunt0_A", "attack_Axe0_A", "attack_dual0_A", "attack_TwoHand0_A", "skill_sword01_A" };
    private static readonly string[] UnarmedAttackClips =
        { "attack_Punch0_A", "attack_Kick0_A", "attack_Punch0_B", "attack_Kick0_B", "attack0", "attack" };
    private static readonly string[] DaggerAttackClips =
        { "attack_Dagger0_A", "attack_Dagger1_A", "attack0", "attack" };
    private static readonly string[] DualAttackClips =
        { "attack_dual0_A", "attack_dual1_A", "attack0", "attack" };
    private static readonly string[] TwoHandAttackClips =
        { "attack_TwoHand0_A", "attack_TwoHand1_A", "attack0", "attack" };
    private static readonly string[] BluntAttackClips =
        { "attack_blunt0_A", "attack_blunt1_A", "attack0", "attack" };
    private static readonly string[] TwoBluntAttackClips =
        { "attack_Twoblunt0_A", "attack_Twoblunt1_A", "attack0", "attack" };
    private static readonly string[] AxeAttackClips =
        { "attack_Axe0_A", "attack_Axe1_A", "attack0", "attack" };
    private static readonly string[] SpearAttackClips =
        { "attack_Spear0_A", "attack0", "attack" };
    private static readonly string[] PolearmAttackClips =
        { "attack_Polearm0_A", "attack0", "attack" };
    private static readonly string[] StaffAttackClips =
        { "attack_Bash0_A", "magic_attack", "attack0", "attack" };
    private static readonly string[] JamadarAttackClips =
        { "attack_jamadar_A", "attack_Kick0_A", "attack0", "attack" };

    private static (int Right, int Left, int Weight) HandLoadout(int[]? gear)
    {
        int rightId = gear != null && gear.Length > 6 ? gear[6] : 0;
        int leftId = gear != null && gear.Length > 7 ? gear[7] : 0;
        var right = rightId > 0 ? ItemData.Get(rightId) : null;
        var left = leftId > 0 ? ItemData.Get(leftId) : null;
        return (right?.Kind ?? WeaponAnimation.NoItem,
                left?.Kind ?? WeaponAnimation.NoItem,
                right?.Weight ?? 0);
    }

    private static readonly string[] BowAttackClips =
        { "Shoot_Arrow_A", "Shoot_Arrow_B",
          "shoot_arrow_a", "shoot_arrow_b", "attack_Bow0_A", "attack_bow0_A", "attack0", "attack" };
    private static readonly string[] CrossbowAttackClips =
        { "Shoot_Quarrel_A", "Shoot_Quarrel_B", "attack_Bow0_A", "Shoot_Arrow_A", "attack0", "attack" };
    private static readonly string[] MeleeSkillClips =
        { "skill_sword01_A", "skill_sword02_A", "attack_sword0_A", "attack_sword1_A", "attack0", "attack",
          "attack_TwoHand0_A", "attack_blunt0_A", "attack_Axe0_A", "attack_dual0_A" };
    private static readonly string[] BuffClips =
        { "warcry0", "warcry1", "magic_ability", "magic_abil", "magic0", "greeting1", "breath", "basic" };
    private static readonly string[] MagicCastClips =
        { "magic_attack", "magic0", "magic1", "magic_heal", "breath", "basic" };
    private static readonly string[] StruckClips = { "struck0", "struck", "struck1", "struck2" };
    private static readonly string[] DeathClips = { "dead2", "dead0", "dead3", "dead1", "dead4", "dead" };

    private static int HeldItemClass(int[]? gear, int slot)
    {
        if (gear == null || slot >= gear.Length || gear[slot] <= 0) return SkillAnimation.NoItem;
        return ItemData.Get(gear[slot])?.Kind ?? SkillAnimation.NoItem;
    }

    private int[]? GearOf(int entityId) =>
        entityId == _myId ? SelfGear() : _ents.TryGetValue(entityId, out var e) ? e.Gear : null;

    private static double PlayActionOn(AnimationPlayer? anim, int action, string[] fallback)
    {
        if (anim == null) return 0;
        string? indexed = AnimationNameAt(anim, action);
        if (indexed != null && anim.HasAnimation(indexed))
            return StartAction(anim, indexed, AnimationMetaAt(anim, action)?.Blend ?? ActionClip.DefaultBlend);
        return PlayActionOn(anim, fallback);
    }

    private const float ProjectileFxHeight = 1.2f;
}

