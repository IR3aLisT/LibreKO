using System.Collections.Generic;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private readonly HashSet<int> _sittingIds = new();
    private readonly HashSet<int> _stanceIds = new();
    private readonly Dictionary<int, Node3D> _sleepFx = new();
    private readonly Dictionary<int, Node3D> _warPointFx = new();
    private bool _selfSitting;
    private bool _selfSitVisual;
    private bool _sitRequestPending;

    private void StateVisualForgetEntity(int id)
    {
        _sittingIds.Remove(id);
        _stanceIds.Remove(id);
        ApplySleepState(id, false);
        ApplyWarPoint(id, false);
        _devilForms.Remove(id);
    }

    private const float SitDrop = 0.42f;
    private const float SitTiltDeg = -14f;
    private const int SleepFxId = 13042;
    private const int WarPointFxId = 34001;
    private const float SleepFxLift = 0.4f;

    private void StateVisualInit()
    {
        Net.I.StateChangeEvent += OnStateChange;
    }

    private void StateVisualDispose()
    {
        Net.I.StateChangeEvent -= OnStateChange;
    }

    private void OnStateChange(int charId, int type, int value)
    {
        switch (type)
        {
            case StateChange.Pose:
                if (value is UserPose.Asleep or UserPose.Awake)
                    ApplySleepState(charId, value == UserPose.Asleep);
                else
                    ApplySitState(charId, value == UserPose.Sitting);
                break;
            case StateChange.Transformation: ApplyTransformState(charId, value); break;
            case StateChange.NeedParty: ApplySeekingParty(charId, (byte)value); break;
            case StateChange.PartyLeader: ApplyPartyLeader(charId, value != 0); break;
            case StateChange.CombatStance: ApplyCombatStanceState(charId, value != StateChange.StanceRelaxed); break;
            case StateChange.Action: ApplyGesture(charId, value); break;
            case StateChange.WarPoint: ApplyWarPoint(charId, value != 0); break;
            case StateChange.DevilForm: ApplyDevilForm(charId, value != 0); break;
            case StateChange.ExpeditionSide: ApplyExpeditionName(); break;
        }
    }

    private void ApplyWarPoint(int charId, bool on)
    {
        if (_warPointFx.Remove(charId, out var running) && IsInstanceValid(running))
            running.QueueFree();
        Node3D? body = charId == _myId ? _self : _ents.TryGetValue(charId, out var e) ? e.Body : null;
        if (!on || body == null || !IsInstanceValid(body) || Fx.NameForId(WarPointFxId) is not { } fxName) return;
        if (Fx.Spawn(fxName, body, Vector3.Zero) is { } fx) _warPointFx[charId] = fx;
    }

    private void ApplyExpeditionName()
    {
        if (Net.I.LastEnter.Zone != StatePlates.ExpeditionZone || Net.I.SelfExpeditionSide == Net.NoExpeditionSide) return;
        int text = StatePlates.ExpeditionNameText(Net.I.SelfExpeditionSide);
        SelfPlate()?.SetNameText(text > 0 ? ItemData.Text(text, "") : "");
    }

    private void ApplySelfPlateState()
    {
        SelfPlate()?.SetTitle(TitleTextOf(Net.I.DisplayTitleId));
        ApplyExpeditionName();
        ApplySelfZoneName();
    }

    private static readonly Color SeekingPartyColour = new(0f, 1f, 0f);

    private void ApplyPartyLeader(int charId, bool leader)
    {
        var plate = charId == _myId ? SelfPlate() : _ents.TryGetValue(charId, out var e) ? e.Plate : null;
        plate?.SetLeader(leader);
    }

    private void ApplySeekingParty(int charId, byte state)
    {
        bool self = charId == _myId;
        var plate = self ? SelfPlate() : _ents.TryGetValue(charId, out var e) ? e.Plate : null;
        int level = self ? Sheet.Level : _ents.TryGetValue(charId, out var who) ? who.Level : 0;
        plate?.SetSeeking(SeekingParty.Line(state, level, ItemData.Text) ?? "", SeekingPartyColour);
    }

    private void ApplySleepState(int charId, bool asleep)
    {
        if (_sleepFx.Remove(charId, out var running) && IsInstanceValid(running))
            running.QueueFree();

        if (!asleep
            || !_ents.TryGetValue(charId, out var e)
            || !IsInstanceValid(e.Body)
            || Fx.NameForId(SleepFxId) is not { } fxName)
            return;

        var fx = Fx.Spawn(fxName, e.Body, new Vector3(0, HeadHeightOf(charId) + SleepFxLift, 0));
        if (fx != null)
            _sleepFx[charId] = fx;
    }

    private void ApplyCombatStanceState(int charId, bool ready)
    {
        if (charId == _myId) return;
        if (ready ? !_stanceIds.Add(charId) : !_stanceIds.Remove(charId)) return;
        if (!_ents.TryGetValue(charId, out var e) || e.IsNpc) return;
        ApplyEntityCombatStance(e, ready);
    }

    private static void ApplyEntityCombatStance(Ent e, bool ready)
    {
        if (e.CombatStance == ready) return;
        e.CombatStance = ready;
        e.Clip = null;
    }

    private const double SitToggleInterval = 3.0;
    private double _sitToggledAt = double.NegativeInfinity;

    private void ToggleSitting()
    {
        if (_selfDead || _sitRequestPending) return;
        bool sitDown = !_selfSitting;
        double now = Now();
        if (now - _sitToggledAt < SitToggleInterval) return;
        if (sitDown && (_selfMoving || SelfCasting(now))) return;
        _sitToggledAt = now;
        _sitRequestPending = true;
        Net.I.SendSitting(sitDown);
    }

    private void ApplySitState(int charId, bool sitting)
    {
        bool self = charId == _myId;

        if (sitting ? !_sittingIds.Add(charId) : !_sittingIds.Remove(charId))
        {
            return;
        }

        if (self)
        {
            _sitRequestPending = false;
            _selfSitting = sitting;
            if (sitting)
            {
                _hasMoveTarget = false;
                _terrainMoveHeld = false;
                _autoMoveForward = false;
                StopAutoAttack();
            }
            ApplySelfSitVisual(sitting);
        }
        else if (_ents.TryGetValue(charId, out var ent) && GodotObject.IsInstanceValid(ent.Body))
        {
            ApplyEntitySitVisual(ent, sitting);
        }

        if (self)
            CombatLogAdd(
                sitting ? "You sit down." : "You stand up.",
                CombatLogKind.Status);
    }

    private void ApplyEntitySitVisual(Ent e, bool sitting)
    {
        if (e.Sitting == sitting) return;
        e.Sitting = sitting;
        e.Clip = null;
        if (sitting) e.Hover = ResolveHoverClips(e.Body, e.Anim);

        if (e.NameTag != null && GodotObject.IsInstanceValid(e.NameTag) && !e.Head.Follows)
        {
            var tagPos = e.NameTag.Position;
            tagPos.Y = sitting && e.Hover == null
                ? SittingNameTagHeight
                : (e.OriginalTagY > 0 ? e.OriginalTagY : StandingNameTagHeight);
            if (e.Plate != null) e.Plate.MoveTo(tagPos);
            else e.NameTag.Position = tagPos;
        }

        var posture = sitting ? e.Hover?.Start ?? SitDownClips : e.Hover?.End ?? StandUpClips;
        if (e.Anim != null && Pick(e.Anim, posture) != null)
        {
            PlayEntityAction(e, posture, ActionRankPosture);
            return;
        }

        var pos = e.Body.Position;
        pos.Y += sitting ? -SitDrop : SitDrop;
        e.Body.Position = pos;
    }

    private void ApplySelfSitVisual(bool sitting)
    {
        if (_selfNameTag == null || !GodotObject.IsInstanceValid(_selfNameTag))
        {
            _selfNameTagFound = true;
            _selfNameTag = _self != null ? FindFirst<Label3D>(_self) : null;
        }

        if (sitting && _selfSitVisual != sitting) _selfHover = ResolveHoverClips(_self, _selfAnim);

        if (_selfNameTag != null && GodotObject.IsInstanceValid(_selfNameTag) && !_selfHead.Follows)
        {
            var tagPos = _selfNameTag.Position;
            tagPos.Y = sitting && _selfHover == null ? SittingNameTagHeight : StandingSelfNameTagHeight;
            if (SelfPlate() is { } plate) plate.MoveTo(tagPos);
            else _selfNameTag.Position = tagPos;
        }

        if (!GodotObject.IsInstanceValid(_selfVisual)) return;
        if (_selfSitVisual == sitting) return;
        _selfSitVisual = sitting;

        _selfVisual.Transform = _selfStandingVisualTransform;
        if (!sitting)
        {
            if (_selfAnim != null)
            {
                double len = PlayActionOn(_selfAnim, _selfHover?.End ?? StandUpClips);
                if (len > 0)
                {
                    BeginSelfAction(len, ActionRankPosture, Now());
                }
            }
            return;
        }

        var sitDown = _selfHover?.Start ?? SitDownClips;
        if (_selfAnim != null && Pick(_selfAnim, sitDown) != null)
        {
            double len = PlayActionOn(_selfAnim, sitDown);
            BeginSelfAction(len, ActionRankPosture, Now());
            return;
        }

        var seated = _selfStandingVisualTransform;
        seated.Origin += Vector3.Down * SitDrop;
        seated.Basis = seated.Basis.Rotated(Vector3.Right, Mathf.DegToRad(SitTiltDeg));
        _selfVisual.Transform = seated;
    }

    private void StandUp()
    {
        _selfSitting = false;
        _selfSitVisual = false;
        _sitRequestPending = true;
        if (GodotObject.IsInstanceValid(_selfVisual))
            _selfVisual.Transform = _selfStandingVisualTransform;
        _selfActionUntil = 0;
        _selfClip = null;
        Net.I.SendSitting(false);
    }

    private void ApplyTransformState(int charId, int skillId)
    {
        bool ending = skillId <= 0;
        if (ZoneLookHolds(charId))
        {
            if (charId == _myId) HoldSelfTransform(ending ? 0 : skillId);
            return;
        }

        if (ending)
        {
            RestoreLookOrZoneLook(charId);
            return;
        }

        WearMonsterLook(charId, skillId);
    }

    private const string TransformNodeName = "TransformModel";
    private const string WornAccessoriesMeta = "worn_accessories";

    private Node3D? TransformHost(int charId) =>
        charId == _myId ? _selfBody : _ents.TryGetValue(charId, out var e) ? e.Body : null;

    private bool IsWearingTransformLook(int charId) =>
        TransformHost(charId)?.GetNodeOrNull<Node3D>(TransformNodeName) != null;

    private void RearmWornLook(Node3D host, int[]? gear)
    {
        if (host.GetNodeOrNull<Node3D>(TransformNodeName) is { } worn)
            AttachWeapons(worn, WornGear(worn, gear));
    }

    private static Node3D? WornLookOf(Node3D body)
    {
        if (body.Name == TransformNodeName) return body;
        if (body.GetNodeOrNull<Node3D>(TransformNodeName) is { } worn) return worn;
        return body.GetParent() is CharacterBody3D host ? host.GetNodeOrNull<Node3D>(TransformNodeName) : null;
    }

    private static (Node3D? Body, int Race) AccessoryHostFor(Node3D body, int race)
    {
        if (WornLookOf(body) is not { } worn) return (body, race);
        return worn.GetMeta(WornAccessoriesMeta, false).AsBool() ? (worn, PlayerRig.StandardRace(race)) : (null, race);
    }

    private bool WearsAccessories(SkillData.Skill skill, Node3D ownBody, int race, Node3D worn) =>
        TransformationUse.WearsAccessories(skill.TransformUse)
        && FindFirst<Skeleton3D>(worn) is { } wornRig
        && PlayerRig.SameBones(StandardRigBones(ownBody, race), BoneNames(wornRig));

    private string[]? _standardRigBones;

    private string[] StandardRigBones(Node3D ownBody, int race)
    {
        if (!PlayerRig.IsKurian(race))
            return FindFirst<Skeleton3D>(ownBody) is { } own ? BoneNames(own) : System.Array.Empty<string>();
        if (_standardRigBones != null) return _standardRigBones;
        var names = System.Array.Empty<string>();
        if (ResolvePlayerScene(PlayerRig.StandardRace(race)) is { } scene)
        {
            var rig = scene.Instantiate<Node3D>();
            if (FindFirst<Skeleton3D>(rig) is { } skel) names = BoneNames(skel);
            rig.Free();
        }
        return _standardRigBones = names;
    }

    private static string[] BoneNames(Skeleton3D skel)
    {
        var names = new string[skel.GetBoneCount()];
        for (int i = 0; i < names.Length; i++) names[i] = skel.GetBoneName(i);
        return names;
    }

    private AnimationPlayer?[] DressAccessories(Node3D body, int[]? gear, int race, bool shineShadow = false)
    {
        var (look, lookRace) = AccessoryHostFor(body, race);
        var anims = AttachWings(body, look == body ? gear : null, race, _zone, shineShadow: shineShadow);
        AttachHandFx(body, look == body ? gear : null, race, _zone);
        if (look == null || look == body) return anims;
        AttachHandFx(look, gear, lookRace, _zone);
        return AttachWings(look, gear, lookRace, _zone, shineShadow: shineShadow);
    }

    private static void DressClanGauntlet(Node3D body, int race, int clanGrade, int clanRanking)
    {
        var (look, lookRace) = AccessoryHostFor(body, race);
        if (look != body) AttachClanGauntlet(body, race, 0, 0);
        if (look != null) AttachClanGauntlet(look, lookRace, clanGrade, clanRanking);
    }

    private void DressSelfLook()
    {
        RerenderSelfEquipment(force: true);
        DressSelfCape();
        DressClanGauntlet(_self, _selfRace, MyClan.InClan ? MyClan.Grade : 0, MyClan.InClan ? MyClan.Ranking : 0);
    }

    private void DressEntityLook(Ent e)
    {
        RedressEntity(e, force: true);
        DressCape(e.Body, e.CapeId, e.CapeR, e.CapeG, e.CapeB, e.IsGm, e.Race);
        DressClanGauntlet(e.Body, e.Race, e.ClanGrade, e.ClanRanking);
    }

    private void WearMonsterLook(int charId, int skillId, float? scale = null, int fixedWeapon = 0)
    {
        var skill = SkillData.Get(skillId);
        if (skill == null || skill.TransformModelId <= 0) return;
        WearLook(charId, skill.TransformModelId, scale ?? skill.TransformScale, skillId, fixedWeapon, skill);
    }

    private Node3D? WearLook(int charId, int modelId, float scale, int skillId, int fixedWeapon, SkillData.Skill? skill)
    {
        var host = TransformHost(charId);
        if (host == null) return null;

        var scene = ResolveMobScene(modelId);
        if (scene == null)
        {
            GD.Print($"[transform] no baked model for {modelId} (skill {skillId})");
            return null;
        }

        RestoreOwnLook(charId, endingTransform: false);

        int race = charId == _myId ? _selfRace : _ents.TryGetValue(charId, out var wearer) ? wearer.Race : 0;
        var (monster, anim) = MakeAnimatedEntity(scene, "", scale);
        monster.Name = TransformNodeName;
        AdoptWornPlate(monster);
        monster.SetMeta(WornAccessoriesMeta, skill != null && WearsAccessories(skill, host, race, monster));
        if (fixedWeapon > 0) monster.SetMeta(FixedWeaponMeta, fixedWeapon);
        host.AddChild(monster);

        if (charId == _myId)
        {
            _selfRigAnim ??= _selfAnim;
            _selfAnim = anim;
            _selfClip = null;
            _selfTransformSkill = skillId;
            DressSelfLook();
        }
        else if (_ents.TryGetValue(charId, out var e))
        {
            e.TransformSkill = skillId;
            e.RigAnim ??= e.Anim;
            e.Anim = anim;
            e.Clip = null;
            DressEntityLook(e);
        }

        SetOwnLookVisible(host, false);
        ReapplySizeState(charId);
        FitPlateToLook(charId);
        return monster;
    }

    private void RestoreOwnLook(int charId, bool endingTransform = true)
    {
        var host = TransformHost(charId);
        if (host == null) return;

        if (host.GetNodeOrNull<Node3D>(TransformNodeName) is not { } worn)
            return;

        host.RemoveChild(worn);
        worn.QueueFree();
        SetOwnLookVisible(host, true);
        ReapplySizeState(charId);
        FitPlateToLook(charId);

        if (charId == _myId)
        {
            if (_selfRigAnim != null) { _selfAnim = _selfRigAnim; _selfRigAnim = null; }
            _selfClip = null;
            if (endingTransform) DropBuffChip(_selfTransformSkill);
            _selfTransformSkill = 0;
            DressSelfLook();
        }
        else if (_ents.TryGetValue(charId, out var e))
        {
            e.TransformSkill = 0;
            if (e.RigAnim != null) { e.Anim = e.RigAnim; e.RigAnim = null; }
            e.Clip = null;
            DressEntityLook(e);
        }
    }

    private AnimationPlayer? _selfRigAnim;
    private int _selfTransformSkill;

    private static void SetOwnLookVisible(Node3D host, bool visible)
    {
        foreach (var child in host.GetChildren())
        {
            if (child is not Node3D node || node.Name == TransformNodeName) continue;
            if (node.FindChild(ModelNodeName, true, false) is Node3D model)
                model.Visible = visible;
            else if (node.Name == ModelNodeName)
                node.Visible = visible;
        }
    }
}
