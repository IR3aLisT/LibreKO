using System;
using System.Collections.Generic;
using Godot;
using LibreKO.Domain;

namespace LibreKO;

public partial class World
{
    private const float RunThreshold = WalkSpeed * 2f;

    private static float RunThresholdOf(Ent e) => e.IsNpc ? RunThreshold * e.BoundRadius : RunThreshold;
    private const double AnimBlend = 0.2;
    private const float TeleportSnap = 30.0f;
    private const float MoveArriveEps = 0.02f;

    private sealed class Ent
    {
        public Node3D Body = null!;
        public bool Infiltrating;
        public bool StealthUndetected;
        public float ActionTimeScale = 1f;
        public StaticBody3D? GateBlocker;
        public Label3D? NameTag;
        public PlateStack? Plate;
        public Node3D? IndicatorFx;
        public string IndicatorName = "";
        public Node3D? RoleFx;
        public AnimationPlayer? Anim;
        public CrowdAnimator? Crowd;
        public AnimationPlayer? RigAnim;
        public Flinch? Flinch;
        public string? Clip;
        public AnimationPlayer?[]? WingAnims;
        public string?[] WingClips = new string?[WingSlotCount];
        public HoverClips? Hover;
        public double ActionUntil;
        public int ActionRank;
        public int ActionAnim = NoActionAnim;
        public int StrikeAnim = NoActionAnim;
        public double DeathCheckAt, DeathDueAt;
        public int KillStriker = -1;
        public Vector3 PushDir;
        public double PushStart = -1, DeathCryAt;
        public string? ActionClip;
        public bool Gathering;
        public bool GatherFishing;
        public double CombatStanceUntil;
        public bool CombatStance;
        public bool Dead;
        public double CorpseRemoveAt;
        public Vector3 Target;
        public bool HasTarget;
        public float Speed;
        public float Lift;
        public float KoX, KoZ, KoY;
        public int TransformSkill;
        public bool IsNpc, IsMonster;
        public bool Statue;
        public bool Attackable;
        public float TargetYaw;
        public bool Backwards;
        public bool Sitting;
        public float OriginalTagY;
        public Aabb? HitFxBox;
        public Aabb? PickBox;
        public float Radius = 0.9f;
        public float BoundRadius = 1.0f;
        public string Name = "";
        public int Id;
        public int GateOpen;
        public Node3D? BridgeVisual;
        public StaticBody3D? BridgeFloor;
        public int ObjectType;
        public int Level;
        public int Hp, MaxHp;
        public int ModelId, Size, Nation, NpcId, NpcType;
        public int Race, LookRace, Face, Hair;
        public int CapeId, CapeR, CapeG, CapeB, KnightsId, AllianceId, ClanGrade, ClanRanking, ClanMarkVersion;
        public string ClanName = "";
        public byte ClanFlag;
        public byte ClanFame;
        public bool IsGm;
        public bool HelmetHidden;
        public bool CostumeHidden;
        public int PersonalRank = NationRankAura.Unranked;
        public int KnightsRank = NationRankAura.Unranked;
        public int[] Gear = System.Array.Empty<int>();
        public Dictionary<int, (Mesh? Mesh, Skin? Skin)> DefaultParts = new();
        public float SpawnX, SpawnZ, SpawnY, SpawnDir;
        public string ModelStem = "";
        public bool AnimPaused;
        public bool AnimThrottled;
        public double AnimStep;
        public int AnimEvery = 1;
        public int AnimFrames;
        public int AnimRank;
        public float CamDist2;
        public bool Far;
        public bool OnScreen = true;
        public bool JustEntered;
        public double FarAccum;
        public Node3D? PlateRoot;
        public readonly HeadJoint Head = new();
        public readonly WingPick WingPick = new();
        public double IdleLoopAt;
        public bool NpcGesturing;
        public string? DressKey;
        public bool PlateNear = true;
        public int MoveFrames;
        public float MoveAccum;
        public bool? AnimActive;
        public float? Yaw;
        public string? StepClip;
        public double StepPos;
        public int StepPhase;
        public int StrikeTarget = -1;
        public double MoveSwingAt = -1;
        public double MoveStrikeAt = -1;
        public double AnimAccum;
    }

    private Node3D _entities = null!;
    private readonly Dictionary<int, Ent> _ents = new();

    private const float NameTagDist = 10f;
    private void OnSpawn(EntitySnapshot info)
    {
        if (info.Id == _myId)
        {
            if (_selfPersonalRank == info.PersonalRank) return;
            _selfPersonalRank = info.PersonalRank;
            RefreshRankAuraFor(_myId, _stealthIds.Contains(_myId));
            return;
        }
        if (!info.IsNpc && ZoneLooks.HidesOtherPlayers(_zone)) return;
        if (info.IsNpc && info.ObjectType != 0) NoteGateState(info.X, info.Z, info.GateOpen != 0);

        if (_ents.TryGetValue(info.Id, out var existing))
        {
            if (info.IsNpc && info.ObjectType == 0)
                ApplyPlainNpcBridgeState(existing, info.GateOpen);

            var rp = existing.Statue ? StatueSpot(info.X, info.Z, info.Y) : GroundPosFor(info.IsNpc, info.X, info.Z, info.Y, existing.Lift);
            float jump = existing.Body.Position.DistanceTo(rp);
            if (existing.Dead || info.Dead || jump > TeleportSnap || jump < MoveArriveEps)
            {
                existing.Body.Position = rp;
                existing.Speed = 0f;
            }
            else existing.Speed = Mathf.Max(jump / RelistGlideSeconds, 1f);
            existing.Target = rp;
            existing.HasTarget = true;
            existing.KoX = info.X; existing.KoZ = info.Z; existing.KoY = info.Y;
            if (existing.Attackable != info.Attackable)
            {
                existing.Attackable = info.Attackable;
                RefreshEntityCollision(existing);
            }

            if (!info.IsNpc)
            {
                if (info.Sitting) _sittingIds.Add(info.Id); else _sittingIds.Remove(info.Id);
                ApplyEntitySitVisual(existing, info.Sitting);
                ApplyEntityCombatStance(existing, _stanceIds.Contains(info.Id));
                if (info.Gathering) BeginRemoteGather(info.Id, info.GatherFishing);
                else if (existing.Gathering) EndRemoteGather(info.Id);
                existing.PersonalRank = info.PersonalRank;
                existing.KnightsRank = info.KnightsRank;
                existing.ClanFlag = info.ClanFlag;
                existing.ClanFame = info.ClanFame;
                RefreshClanLine(existing, info.Id);
                RefreshRankAuraFor(info.Id, _stealthIds.Contains(info.Id));
            }
            if (info.Dead) LayOutCorpse(existing);
            else if (existing.Dead)
            {
                DropLootBoxesOf(info.Id);
                existing.Dead = false;
                existing.DeathDueAt = 0;
                existing.CorpseRemoveAt = 0;
                existing.ActionUntil = 0; existing.ActionRank = 0;
                existing.ActionClip = null; existing.Clip = null;
                existing.Hp = existing.MaxHp;
                RefreshEntityCollision(existing);
            }
            return;
        }

        if (!_pendingSpawns.ContainsKey(info.Id)) _pendingOrder.Enqueue(info.Id);
        _pendingSpawns[info.Id] = info;
    }

    private readonly Dictionary<int, EntitySnapshot> _pendingSpawns = new();
    private readonly Queue<int> _pendingOrder = new();
    private const int SpawnBuildBudget = 1;

    private void ProcessSpawnQueue()
    {
        int built = 0;
        int examined = 0, pending = _pendingOrder.Count;
        while (built < SpawnBuildBudget && examined < pending && _pendingOrder.Count > 0)
        {
            int id = _pendingOrder.Dequeue();
            examined++;
            if (!_pendingSpawns.TryGetValue(id, out var info)) continue;
            if (_ents.ContainsKey(id)) { _pendingSpawns.Remove(id); continue; }
            if (!SceneReadyFor(info)) { _pendingOrder.Enqueue(id); continue; }
            _pendingSpawns.Remove(id);
            BuildEntity(info);
            built++;
        }
    }

    private void BuildEntity(EntitySnapshot info)
    {
        var label = info.Name.Length > 0 ? info.Name : (info.IsNpc ? "NPC" : "Player");
        bool statue = info.IsNpc && RankerStatue.Is(info.NpcType);

        var watch = Diag.Watch();
        var sceneScope = Perf.Measure(Perf.Section.BuildScene);
        bool mapObject = info.ObjectType == NpcTypes.ObjectType.MapObject;
        var loadWatch = Diag.Watch();
        PackedScene? scene = mapObject ? null
            : info.IsNpc && !statue ? ResolveMobScene(info.ModelId)
                                    : ResolvePlayerScene(PlayerRig.LookRace(info.Race, info.Class));
        Diag.Slow($"model load {label} model={info.ModelId} race={info.Race}", loadWatch);
        float lift = scene != null || mapObject ? 0f : CapsuleHalf;
        var pos = statue ? StatueSpot(info.X, info.Z, info.Y) : GroundPosFor(info.IsNpc, info.X, info.Z, info.Y, lift);

        Node3D body;
        AnimationPlayer? anim = null;
        CrowdAnimator? crowd = null;
        if (scene != null)
        {
            (body, anim) = statue
                ? MakeAnimatedEntity(scene, StatueOverheadName, RankerStatue.Scale)
                : MakeAnimatedEntity(scene, label, info.Size > 0 ? info.Size / 100f : 1f);
            crowd = CrowdAnimator.Create(anim, body, scene.ResourcePath);
            if (crowd != null) anim!.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual;
        }
        else if (mapObject)
            body = MakeMapObjectEntity(label);
        else
            body = MakeEntity(ColorFor(info), label);

        body.Position = pos;
        float spawnYaw = info.Dir != 0 ? 180f - 2f * info.Dir : (info.Id * 137) % 360;
        if (scene != null)
            body.RotationDegrees = new Vector3(0, spawnYaw, 0);
        using (Perf.Measure(Perf.Section.BuildEnter)) _entities.AddChild(body);
        sceneScope.Dispose();
        Dictionary<int, (Mesh? Mesh, Skin? Skin)> defaultParts = new();
        AnimationPlayer?[]? wingAnims = null;
        Flinch? flinch = null;
        if (scene != null)
        {
            using (Perf.Measure(Perf.Section.BuildGraft))
            {
                defaultParts = CapturePartDefaults(body);
                int lookRace = PlayerRig.LookRace(info.Race, info.Class);
                if (statue)
                    GraftEquipment(body, lookRace, info.Face, info.Gear, info.Hair, false);
                else if (!info.IsNpc)
                {
                    flinch = Flinch.Attach(body);
                    GraftEquipment(body, lookRace, info.Face, info.Gear, info.Hair, info.HelmetHidden, info.CostumeHidden);
                    DressEntityCape(body, info);
                }
            }
            using (Perf.Measure(Perf.Section.BuildGear))
            {
                AttachWeapons(body, info.Gear, info.NpcType, info.NpcId);
                if (!info.IsNpc) AttachClanGauntlet(body, info.Race, info.ClanGrade, info.ClanRanking);
                if (info.IsNpc && !statue)
                {
                    string fxStem = _mobIndex != null && _mobIndex.TryGetValue(info.ModelId, out var fs)
                        ? fs : "";
                    AttachCharacterFxPlugs(body, fxStem);
                }
                else if (!info.IsNpc)
                {
                    wingAnims = AttachWings(body, ShownCospre(info.Gear), info.Race, _zone);
                    AttachHandFx(body, ShownCospre(info.Gear), info.Race, _zone);
                }
            }
        }
        using var restScope = Perf.Measure(Perf.Section.BuildRest);
        ApplyEntityRenderCost(body);
        if (scene != null && Config.MergeCharacters) CharacterMerge.Apply(body);

        string modelStem = mapObject ? "(map object)"
            : scene == null ? "(capsule fallback)"
            : statue ? "(ranker statue)"
            : info.IsNpc ? (_mobIndex != null && _mobIndex.TryGetValue(info.ModelId, out var ms) ? ms + ".glb" : "(model)")
            : "(race rig)";
        var radii = BodyRadii(body);
        var ent = new Ent
        {
            Id = info.Id,
            Body = body, Anim = anim, Crowd = crowd, WingAnims = wingAnims, Flinch = flinch,
            Target = pos, HasTarget = true, Speed = 0f, Lift = lift,
            KoX = info.X, KoZ = info.Z, KoY = info.Y,
            IsNpc = info.IsNpc, IsMonster = info.IsMonster, Statue = statue, Attackable = info.Attackable, ObjectType = info.ObjectType,
            Name = label, Level = info.Level, Radius = radii.Footprint, BoundRadius = radii.Bound,
            ModelId = info.ModelId, Size = info.Size, Nation = ShownNation(info),
            NpcId = info.NpcId, NpcType = info.NpcType,
            Race = info.Race, LookRace = PlayerRig.LookRace(info.Race, info.Class), Face = info.Face, Hair = info.Hair,
            CapeId = info.CapeId, CapeR = info.CapeR, CapeG = info.CapeG, CapeB = info.CapeB,
            KnightsId = info.KnightsId, AllianceId = info.AllianceId, ClanGrade = info.ClanGrade, ClanRanking = info.ClanRanking,
            ClanMarkVersion = info.ClanMarkVersion, ClanName = info.ClanName, ClanFlag = info.ClanFlag, ClanFame = info.ClanFame,
            IsGm = info.IsGm, HelmetHidden = info.HelmetHidden, CostumeHidden = info.CostumeHidden, PersonalRank = info.PersonalRank, KnightsRank = info.KnightsRank,
            Gear = info.Gear.Length > 0 ? (int[])info.Gear.Clone() : System.Array.Empty<int>(),
            DefaultParts = defaultParts,
            SpawnX = info.X, SpawnZ = info.Z, SpawnY = info.Y, SpawnDir = info.Dir,
            ModelStem = modelStem, TargetYaw = spawnYaw,
        };
        _ents[info.Id] = ent;
        ApplyGmFx(info.Id, Net.I.GmFxVisible(info.Id, info.IsGm));
        if (info.GenieActive) ShowGenieAura(info.Id, true);
        if (info.TransformSkill > 0 && !info.IsNpc && !ZoneLookHolds(info.Id)) WearMonsterLook(info.Id, info.TransformSkill);
        else if (!info.IsNpc) ApplyZoneLook(info.Id);

        if (IsBreakableGate(info)) ent.GateBlocker = AttachGateBlocker(body);
        RefreshEntityCollision(ent);
        if (info.IsNpc && info.ObjectType == 0)
            ApplyPlainNpcBridgeState(ent, info.GateOpen);

        if (!info.IsNpc)
        {
            ent.TargetYaw = 180f - info.Dir;
            body.RotationDegrees = new Vector3(0, ent.TargetYaw, 0);
            if (info.Sitting) _sittingIds.Add(info.Id);
            ent.Sitting = _sittingIds.Contains(info.Id);
            if (ent.Sitting) ent.Hover = ResolveHoverClips(body, anim);
            ent.CombatStance = _stanceIds.Contains(info.Id);
            ent.Gathering = info.Gathering;
            ent.GatherFishing = info.GatherFishing;
        }
        var nameLabel = FindFirst<Label3D>(body);
        if (nameLabel != null)
        {
            NamePlate.Tint(nameLabel, NameColor(info));
            if (!info.IsNpc && ZoneNameText(info) is > 0 and var hidden)
                nameLabel.Text = ItemData.Text(hidden, nameLabel.Text);
            nameLabel.Visible = false;
            ent.OriginalTagY = nameLabel.Position.Y;
            SeekHeadJoint(body, ent.Head);
            if ((ent.Sitting || _stalls.ContainsKey(info.Id)) && !ent.Head.Follows)
            {
                nameLabel.Position = new Vector3(nameLabel.Position.X, SittingNameTagHeight, nameLabel.Position.Z);
            }
            ent.NameTag = nameLabel;
            if (IsWearingTransformLook(info.Id)) FitPlateToLook(info.Id);
            ent.PlateRoot = nameLabel.GetParent() as Node3D;
            ent.Plate = new PlateStack(nameLabel);
            if (info.IsNpc) ent.Plate.SetClan(info.ClanName);
            else RefreshClanLine(ent, info.Id);
            ent.Plate.SetMark(PlateClanMark(ClanMark(info.KnightsId, info.ClanMarkVersion)));
            ent.Plate.SetTitle(TitleTextOf(info.TitleId));
            ent.Plate.SetSeeking(SeekingParty.Line(info.NeedParty, info.Level, ItemData.Text) ?? "", SeekingPartyColour);
            ent.Plate.SetLeader(info.PartyLeader);
            if (!info.IsNpc && AtSiegeWar) ApplySiegeName(ent);
            if (!info.IsNpc) RefreshMatchNames();
        }
        ent.RoleFx = SpawnNpcRoleFx(ent);
        if (info.Invisible) StealthOnSpawn(info.Id, info.Invisibility);
        if (!info.IsNpc && info.SoccerTeam != Domain.Soccer.TeamNone) ApplySoccerTeam(info.Id, info.SoccerTeam);
        if (!info.IsNpc && info.Appearance == RespawnBlink.Blinking) StartRespawnBlink(info.Id);
        if (!info.IsNpc) SizeStateOnSpawn(info.Id, info.Appearance, info.Race);
        if (!info.IsNpc) RefreshRankAuraFor(info.Id, _stealthIds.Contains(info.Id));
        if (crowd != null) ent.AnimActive = false;
        if (info.Dead) LayOutCorpse(ent);
        else PlayClip(ent, "idle");
        if (crowd != null && !info.Dead)
        {
            crowd.TakeOver();
            crowd.Step(0);
        }
        if (ent.Gathering && !info.Dead) BeginRemoteGather(info.Id, ent.GatherFishing);
        RefreshNpcQuestMarker(ent);
        AttachPendingStall(info.Id);
        if (statue) AttachStatuePlaque(body, info.Name, info.X, info.Z, info.Y);
        Diag.Slow($"entity {label} model={info.ModelId} npc={info.IsNpc}", watch);
    }

    private float LayOutCorpse(Ent e, bool settled = true, DeathPose pose = DeathPose.KeepPosition)
    {
        e.Dead = true;
        e.DeathDueAt = 0;
        e.HasTarget = false;
        e.Speed = 0f;
        e.CorpseRemoveAt = e.IsNpc && !HoldsLootBox(e.Id) ? Now() + CorpseLinger : 0;
        e.Flinch?.Stop();
        if (settled) e.Hp = 0;
        RefreshEntityCollision(e);
        int clip = DeathPoses.ClipIndex(pose, e.IsNpc);
        double len = PlayActionOn(e.Anim, clip, DeathClips);
        e.ActionUntil = Now() + Mathf.Max((float)len, 3.0f);
        e.ActionRank = ActionRankDeath;
        e.ActionClip = "dead";
        if (settled && e.Anim != null && len > 0)
        {
            BlendHold.Release(e.Anim);
            e.Anim.Advance(len);
        }
        return e.Anim != null && AnimationMetaAt(e.Anim, clip) is { } meta ? meta.SoundTime(0) : -1f;
    }

    private const string PlateRootName = "plates";

    private static Node3D AttachNameLabel(Node3D body, string name, float y)
    {
        var root = new Node3D { Name = PlateRootName };
        body.AddChild(root);
        root.AddChild(NameLabel(name, y));
        return root;
    }

    private static void SyncEntityPlates(Ent e, bool near)
    {
        if (e.PlateRoot == null || near == e.PlateNear) return;
        e.PlateNear = near;
        e.PlateRoot.TopLevel = !near;
        if (near) e.PlateRoot.Transform = Transform3D.Identity;
    }

    public System.Collections.Generic.List<string> MapObjectReport()
    {
        var lines = new System.Collections.Generic.List<string>();
        foreach (var kv in _ents)
        {
            var e = kv.Value;
            if (!e.IsNpc || e.ModelStem != "(map object)") continue;
            lines.Add($"MAPOBJECT id={kv.Key} proto={e.NpcId} model={e.ModelId} " +
                      $"tNpc={e.NpcType} hit={e.Attackable} \"{e.Name}\"");
        }
        if (lines.Count == 0) lines.Add("MAPOBJECT none in range");
        return lines;
    }

    private bool BlockedByUnits(Vector3 from, Vector3 to)
    {
        if (Net.I.IsGm) return false;
        var before = new Vector2(from.X, from.Z);
        var after = new Vector2(to.X, to.Z);
        foreach (var e in _ents.Values)
        {
            if (e.Dead || !e.Attackable || !GodotObject.IsInstanceValid(e.Body)) continue;
            var at = new Vector2(e.Body.Position.X, e.Body.Position.Z);
            if (at.DistanceSquaredTo(after) > UnitBlocking.Reach * UnitBlocking.Reach) continue;
            float height = e.IsNpc ? EntityLocalBox(e).Size.Y * e.Body.Scale.Y : UnitBlocking.PlayerHeight;
            if (UnitBlocking.Blocks(before, after, at, UnitBlocking.PlayerHeight, height)) return true;
        }
        return false;
    }

    private static void RefreshEntityCollision(Ent e) => RefreshGateBlocker(e);

    private const float MoveFacingEpsSq = 0.04f;
    private const float MoveFacingWarpSq = 900f;
    private const float MovePlaybackSeconds = 1.5f;
    private const float MovePlaybackDamp = 0.85f;
    private const float NpcMovePlaybackSeconds = 0.3f;
    private const float RelistGlideSeconds = 0.5f;


    private static void CancelEntityPosture(Ent e, double now)
    {
        if (e.ActionRank != ActionRankPosture || now >= e.ActionUntil) return;
        e.ActionUntil = 0;
        e.ActionClip = null;
        e.Clip = null;
    }

    private static void FaceEntity(Ent e, float yawDegrees, bool immediate)
    {
        e.TargetYaw = yawDegrees;
        if (immediate) SetEntityYaw(e, yawDegrees);
    }

    private static void SetEntityYaw(Ent e, float yawDegrees)
    {
        e.Yaw = yawDegrees;
        e.Body.RotationDegrees = new Vector3(0, yawDegrees, 0);
    }

    private static void TickEntityFacing(Ent e, float dt)
    {
        float current = e.Yaw ?? e.Body.RotationDegrees.Y;
        if (current == e.TargetYaw) { e.Yaw = current; return; }
        SetEntityYaw(e, Turning.Toward(current, e.TargetYaw, dt));
    }

    private void OnMove(int id, float x, float z, float y, float velHint, bool travelling)
    {
        if (id == _myId) return;
        if (!_ents.TryGetValue(id, out var e)) return;
        if (e.Dead && e.IsNpc) return;
        if (e.Dead)
        {
            e.Dead = false; e.DeathDueAt = 0; e.CorpseRemoveAt = 0; e.ActionUntil = 0; e.ActionClip = null; e.Clip = null;
            e.PushStart = -1; e.DeathCryAt = 0; e.KillStriker = -1;
            RefreshEntityCollision(e);
        }

        var dest = e.Body.Position.DistanceSquaredTo(_self.Position) <= EntityProbeDist * EntityProbeDist
            ? GroundPosFor(e.IsNpc, x, z, y, e.Lift)
            : GroundPos(x, z, y, e.Lift);
        float dxKo = x - e.KoX, dzKo = z - e.KoZ;
        float koMove2 = dxKo * dxKo + dzKo * dzKo;
        if (travelling && koMove2 > MoveFacingEpsSq)
            CancelEntityPosture(e, Now());
        if (travelling && koMove2 > MoveFacingEpsSq && koMove2 < MoveFacingWarpSq)
        {
            bool backwards = velHint < 0f;
            FaceEntity(
                e,
                180f - (backwards ? Coord.KoHeading(-dxKo, -dzKo) : Coord.KoHeading(dxKo, dzKo)),
                immediate: false);
        }

        e.KoX = x; e.KoZ = z; e.KoY = y;

        if (travelling)
            e.Backwards = velHint < 0f;

        if (e.Body.Position.DistanceTo(dest) > TeleportSnap)
        {
            e.Body.Position = dest;
            e.Target = dest;
            e.HasTarget = true;
            e.Speed = 0f;
            return;
        }

        if (!travelling)
        {
            e.Target = dest;
            e.HasTarget = true;
            return;
        }

        var delta = dest - e.Body.Position;
        float dist = new Vector2(delta.X, delta.Z).Length();
        e.Target = dest;
        e.HasTarget = true;
        if (e.IsNpc && e.NpcId == Domain.Soccer.BallProto)
        {
            e.Speed = dist / Domain.Soccer.BallTravelSeconds;
            return;
        }
        float playback = dist / MovePlaybackSeconds * MovePlaybackDamp;
        if (e.IsNpc && velHint > 0.01f)
            playback = Mathf.Max(velHint, dist / NpcMovePlaybackSeconds);
        e.Speed = Mathf.Abs(velHint) > 0.01f ? playback : 0f;
    }

    private static bool EntityMoving(Ent e) => EntityMoving(e, e.Body.Position);

    private static bool EntityMoving(Ent e, Vector3 pos) =>
        !e.Dead && e.HasTarget && e.Speed > 0.1f
        && pos.DistanceTo(e.Target) > MoveArriveEps;

    private const float EntityRenderDist = 160f;
    private const float EntityRenderFade = 30f;
    private const float EntityProbeDist = 64f;

    private static void ApplyEntityRenderCost(Node3D body)
    {
        foreach (var mi in FindAll<MeshInstance3D>(body))
        {
            mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            mi.VisibilityRangeEnd = EntityRenderDist * Config.ViewDistance;
            mi.VisibilityRangeEndMargin = EntityRenderFade;
            mi.VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Self;
            SkinShare.FixBounds(mi);
        }
    }

    private const float AnimAlwaysDist = 22f;
    private const int OffScreenMoveEvery = 6;
    private const float OnScreenMoveFloor = 1f / 30f;
    private const float AnimViewMargin = 4f;
    private const double FarEntityInterval = 0.1;
    private int _frustumCount;
    private int _animFull, _animMid, _animFar, _animOff;
    private int _closeEntities;

    private void RefreshFrustum()
    {
        if (_camera == null) { _frustumCount = 0; return; }
        var frustum = _camera.GetFrustum();
        _frustumCount = Mathf.Min(frustum.Count, _frustumPlanes.Length);
        for (int j = 0; j < _frustumCount; j++) _frustumPlanes[j] = frustum[j];
    }

    private bool InView(Vector3 pos)
    {
        for (int j = 0; j < _frustumCount; j++)
            if (_frustumPlanes[j].DistanceTo(pos) > AnimViewMargin) return false;
        return true;
    }

    private readonly List<Ent> _animRanking = new();
    private static readonly System.Comparison<Ent> ByCamDist = (a, b) => a.CamDist2.CompareTo(b.CamDist2);

    private void RankEntityAnimation()
    {
        _animRanking.Clear();
        foreach (var e in _ents.Values)
            if (e.Anim != null && !e.Far) _animRanking.Add(e);
        _animRanking.Sort(ByCamDist);
        for (int i = 0; i < _animRanking.Count; i++) _animRanking[i].AnimRank = i;
    }

    private void UpdateEntityAnimLod(Ent e, Vector3 camPos, Vector3 pos)
    {
        float d2 = pos.DistanceSquaredTo(camPos);
        e.CamDist2 = d2;
        if (d2 <= Perf.CloseEntityDist * Perf.CloseEntityDist) _closeEntities++;
        float pause2 = EntityRenderDist * EntityRenderDist;
        float resume2 = (EntityRenderDist - 12f) * (EntityRenderDist - 12f);
        e.Far = e.Far ? d2 > resume2 : d2 > pause2;
        bool inView = InView(pos);
        e.JustEntered = inView && !e.OnScreen;
        e.OnScreen = inView;
        if (e.Anim == null) return;
        bool offScreen = d2 > AnimAlwaysDist * AnimAlwaysDist && !inView;
        e.AnimPaused = !Config.EntityAnim || Perf.SkipPoses || e.Far || offScreen;
        double step = 0;
        int every = 1;
        if (!e.AnimPaused)
        {
            double mid = 1.0 / Config.AnimMidHz, far = 1.0 / Config.AnimThrottleHz;
            float reach = Mathf.Clamp(Config.ViewDistance, Config.ViewDistanceMin, Config.ViewDistanceMax);
            float midDist = Config.AnimMidDist * reach, fullDist = Config.AnimFullDist * reach;
            if (d2 > midDist * midDist) step = far;
            else if (d2 > fullDist * fullDist) step = mid;
            if (e.AnimRank >= Config.AnimFullCount + Config.AnimMidCount) step = Mathf.Max(step, far);
            else if (e.AnimRank >= Config.AnimFullCount) step = Mathf.Max(step, mid);
            every = step == 0 ? 1 : step >= far ? Config.AnimFarEvery : Config.AnimMidEvery;
        }
        e.AnimStep = step;
        e.AnimEvery = every;
        e.AnimThrottled = !e.AnimPaused && (step > 0 || e.Crowd != null);
        if (e.AnimPaused) _animOff++;
        else if (step == 0) _animFull++;
        else if (step <= 1.0 / Config.AnimMidHz) _animMid++;
        else _animFar++;
        bool active = !e.AnimPaused && !e.AnimThrottled;
        if (e.AnimActive != active)
        {
            e.AnimActive = active;
            e.Anim.CallbackModeProcess = active
                ? AnimationMixer.AnimationCallbackModeProcess.Idle
                : AnimationMixer.AnimationCallbackModeProcess.Manual;
            if (!active) e.Crowd?.TakeOver();
            else e.Crowd?.Resume();
        }
    }

    private void OnRotate(int id, float dir)
    {
        if (id == _myId) return;
        if (!_ents.TryGetValue(id, out var e) || e.Dead) return;
        FaceEntity(e, 180f - dir, immediate: false);
    }

    private void OnLookChange(int id, int lookSlot, int itemId, short durability)
    {
        if (id == _myId) return;
        if (!_ents.TryGetValue(id, out var e) || e.IsNpc) return;
        int gearIndex = GearIndexForLookSlot(lookSlot);
        if (gearIndex < 0) return;

        if (e.Gear.Length < InventoryConstants.VisualSlotCount)
            System.Array.Resize(ref e.Gear, InventoryConstants.VisualSlotCount);
        if (e.Gear[gearIndex] == itemId) return;
        e.Gear[gearIndex] = itemId;

        RedressWhenGearLoaded(e);
    }

    private static string DressKey(int race, int face, int[]? gear, int hair, bool hideHelmet, bool hideCostume) =>
        $"{race}:{face}:{hair}:{hideHelmet}:{hideCostume}:{(gear == null ? "" : string.Join(',', gear))}";

    private void RedressEntity(Ent e, bool force = false)
    {
        if (e.Anim == null || e.DefaultParts.Count == 0)
            return;
        string key = DressKey(e.LookRace, e.Face, e.Gear, e.Hair, e.HelmetHidden, e.CostumeHidden);
        if (key == e.DressKey && !force) return;
        e.DressKey = key;
        CharacterMerge.Remove(e.Body);
        RestorePartDefaults(e.Body, e.DefaultParts);
        GraftEquipment(e.Body, e.LookRace, e.Face, e.Gear, e.Hair, e.HelmetHidden, e.CostumeHidden);
        AttachWeapons(e.Body, e.Gear);
        e.WingAnims = DressAccessories(e.Body, ShownCospre(e.Gear), e.Race);
        System.Array.Clear(e.WingClips);
        if (e.Sitting)
        {
            e.Hover = ResolveHoverClips(e.Body, e.Anim);
            e.Clip = null;
        }
        RearmWornLook(e.Body, e.Gear);
        ApplyEntityRenderCost(e.Body);
        if (!e.IsNpc && Config.MergeCharacters) CharacterMerge.Apply(e.Body);
        e.HitFxBox = null;
        e.PickBox = null;
    }

    private static int GearIndexForLookSlot(int lookSlot)
        => System.Array.IndexOf(InventoryConstants.VisualSlots, lookSlot);

    private const double PlayerCorpseFade = 10.0;

    private void OnOut(int id)
    {
        _pendingSpawns.Remove(id);
        if (_ents.TryGetValue(id, out var corpse) && corpse.Dead && !corpse.IsNpc)
        {
            if (corpse.CorpseRemoveAt <= 0) corpse.CorpseRemoveAt = Now() + PlayerCorpseFade;
            return;
        }
        RemoveEntity(id);
    }

    private void RemoveEntity(int id)
    {
        if (!_ents.TryGetValue(id, out var e)) return;
        DropLootBoxesOf(id);
        if (e.BridgeFloor != null && GodotObject.IsInstanceValid(e.BridgeFloor))
            e.BridgeFloor.QueueFree();
        e.Crowd?.Release();
        e.Body.QueueFree();
        _ents.Remove(id);
        ForgetSkillFx(id);
        StateVisualForgetEntity(id);
        SizeStateForgetEntity(id);
        SoccerForgetEntity(id);
        StealthForgetEntity(id);
        ForgetStall(id);
        _remoteCastingSkill.Remove(id);
    }

    private void ApplyEntityTitle(Ent ent, int titleId)
    {
        ent.Plate?.SetTitle(TitleTextOf(titleId));
    }

    private static string TitleTextOf(int titleId) =>
        titleId != 0 && StatePlates.TitleShownIn(Net.I.LastEnter.Zone) && AchievementData.TitleOf(titleId) is { } title ? title.Name : "";

    private void OnEntityTitle(int charId, int titleId)
    {
        if (charId == _myId)
        {
            SelfPlate()?.SetTitle(TitleTextOf(titleId));
            RefreshAchievementTitleBanner();
            if (_achShown) RebuildAchievementList(keepScroll: true);
            if (_titleShown) RebuildTitleList();
            return;
        }

        if (_ents.TryGetValue(charId, out var ent))
            ApplyEntityTitle(ent, titleId);
    }

    private PlateStack? _selfPlate;

    private PlateStack? SelfPlate()
    {
        if (_selfPlate != null) return _selfPlate;
        if (_self == null) return null;

        if (!_selfNameTagFound)
        {
            _selfNameTagFound = true;
            _selfNameTag = FindFirst<Label3D>(_self);
        }

        if (_selfNameTag == null || !GodotObject.IsInstanceValid(_selfNameTag)) return null;
        return _selfPlate = new PlateStack(_selfNameTag);
    }


    private Label3D? _selfNameTag;
    private bool _selfNameTagFound;

    private float HeadHeightOf(int charId)
    {
        bool sitting = charId == _myId
            ? (_selfSitting || _stalls.ContainsKey(charId))
            : (_ents.TryGetValue(charId, out var sitEnt) && (sitEnt.Sitting || _stalls.ContainsKey(charId)));

        if (charId != _myId)
        {
            float baseH = _ents.TryGetValue(charId, out var tagEnt) && tagEnt.NameTag is { } tag
                ? Mathf.Max(0.4f, tag.Position.Y)
                : StandingNameTagHeight;
            return sitting ? Mathf.Min(baseH, SittingNameTagHeight) : baseH;
        }
        if (!_selfNameTagFound)
        {
            _selfNameTagFound = true;
            _selfNameTag = _self != null ? FindFirst<Label3D>(_self) : null;
        }
        float selfH = Mathf.Max(0.4f, _selfNameTag?.Position.Y ?? StandingSelfNameTagHeight);
        return sitting ? Mathf.Min(selfH, SittingNameTagHeight) : selfH;
    }

    private void OnEntityHp(int id, int hp, int maxHp, int damage)
    {
        if (!_ents.TryGetValue(id, out var e)) return;
        int old = e.MaxHp > 0 ? e.Hp : hp;
        e.Hp = hp; e.MaxHp = maxHp;
        if (e.IsNpc || e.Attackable) { _lastHitId = id; _lastHitAt = Now(); }
        int shown = damage != 0 ? Mathf.Abs(damage) : Mathf.Max(0, old - hp);
        if (shown > 0)
        {
            CombatLogAdd(TextTemplate.Fill(SystemText(CombatLogLines.TargetDamageText, "%s received %d damage"), ShownName(e), shown),
                CombatLogKind.Damage);
            Floaters?.Damage(id, shown);
            if (hp > 0 && !e.Dead)
            {
                AudioStruck(id);
                HitFlash(id);
            }
        }
        else if (hp - old > 0 && old > 0)
        {
            CombatLogAdd($"{e.Name} recovered {hp - old:n0} HP.", CombatLogKind.Recovery);
            Floaters?.Cure(id, hp - old);
        }
    }

    private static (float Footprint, float Bound) BodyRadii(Node3D body)
    {
        Aabb? merged = null;

        void Walk(Node n)
        {
            if (n is Cape or BoneAttachment3D) return;
            if (n is MeshInstance3D { Mesh: not null } mi)
            {
                var a = mi.GlobalTransform * mi.GetAabb();
                if (a.Size != Vector3.Zero)
                    merged = merged.HasValue ? merged.Value.Merge(a) : a;
            }
            foreach (var child in n.GetChildren()) Walk(child);
        }

        Walk(body);
        if (!merged.HasValue) return (0.9f, 1.0f);
        var s = merged.Value.Size;
        return (Mathf.Clamp(Mathf.Max(s.X, s.Z) * 0.5f, 0.45f, 6f),
                Mathf.Clamp(s.Length() * 0.5f, 0.7f, 10f));
    }

    private int AnonymousNameText(EntitySnapshot info) =>
        PlateRules.AnonymousNameText(_zone, Net.I.Nation, MyClan.InClan ? MyClan.ClanId : 0, info.Nation, info.KnightsId);

    private void RefreshFriendNameColours()
    {
        foreach (var e in _ents.Values)
            if (AtSiegeWar) ApplySiegeName(e);
            else RefreshFriendNameColour(e);
    }

    private void RefreshFriendNameColour(Ent e)
    {
        if (e.IsNpc || e.NameTag == null || !GodotObject.IsInstanceValid(e.NameTag)) return;
        NamePlate.Tint(e.NameTag, PlateRules.FriendColour(_zone) && IsFriendName(e.Name) ? FriendNameColour
            : PlateRules.AnonymousNameText(_zone, Net.I.Nation, MyClan.InClan ? MyClan.ClanId : 0, e.Nation, e.KnightsId)
                == PlateRules.DelosHeroText ? NamePlate.Enemy
            : PlayerNameColor(e.Nation));
    }

    private Color PlayerNameColor(int nation) =>
        nation == Net.I.Nation ? NamePlate.Ally : NamePlate.Enemy;

    private static readonly Color FriendNameColour = new(0.5f, 1f, 0.5f);

    private Color NameColor(EntitySnapshot info)
    {
        if (!info.IsNpc && PlateRules.FriendColour(_zone) && IsFriendName(info.Name)) return FriendNameColour;
        if (!info.IsNpc && AnonymousNameText(info) == PlateRules.DelosHeroText) return NamePlate.Enemy;
        if (!info.IsNpc && ZoneNameText(info) == ChatSender.PathoistText) return PathoistColour;
        if (!info.IsNpc) return PlayerNameColor(ShownNation(info));
        if (!info.Attackable) return new Color(0.55f, 0.85f, 1f);
        int d = info.Level - Sheet.Level;
        if (d >= 4) return new Color(1f, 0.35f, 0.30f);
        if (d >= 0) return new Color(1f, 0.85f, 0.35f);
        return new Color(0.5f, 1f, 0.5f);
    }

    public const float BridgeLoweredPitch = -110.0f;
    private const float BridgeTiltDegreesPerSecond = 45f;

    private void TiltBridge(Node3D? visual, float fromPitch, float toPitch)
    {
        if (visual == null) return;
        visual.RotationDegrees = new Vector3(fromPitch, 0, 0);
        float span = Mathf.Abs(toPitch - fromPitch);
        if (span <= 0f) return;
        var tween = visual.CreateTween();
        tween.TweenProperty(visual, "rotation_degrees:x", toPitch, span / BridgeTiltDegreesPerSecond);
    }

    private void ApplyPlainNpcBridgeState(Ent e, int gateOpen)
    {
        e.GateOpen = gateOpen;
        if (e.BridgeVisual == null || !GodotObject.IsInstanceValid(e.BridgeVisual))
            e.BridgeVisual = e.Body.GetNodeOrNull<Node3D>(ModelNodeName) ?? e.Body.FindChild(ModelNodeName, true, false) as Node3D;

        if (gateOpen == 2)
        {
            float startPitch = e.BridgeVisual?.RotationDegrees.X ?? 0f;
            if (e.BridgeVisual != null)
                e.BridgeVisual.RotationDegrees = new Vector3(BridgeLoweredPitch, 0, 0);

            if (e.BridgeFloor == null || !GodotObject.IsInstanceValid(e.BridgeFloor))
            {
                e.Body.ForceUpdateTransform();
                e.BridgeVisual?.ForceUpdateTransform();
                var faces = new List<Vector3>();
                CollectFaces(e.Body, faces);
                if (faces.Count > 0)
                {
                    var shape = new ConcavePolygonShape3D { BackfaceCollision = true };
                    shape.SetFaces(faces.ToArray());
                    var floor = new StaticBody3D { Name = "BridgeFloor", CollisionLayer = WorldCollisionLayer, CollisionMask = 0 };
                    floor.AddChild(new CollisionShape3D { Shape = shape });
                    _entities.AddChild(floor);
                    e.BridgeFloor = floor;
                }
            }
            else
            {
                e.BridgeFloor.CollisionLayer = WorldCollisionLayer;
            }
            TiltBridge(e.BridgeVisual, startPitch, BridgeLoweredPitch);
        }
        else if (gateOpen == 0)
        {
            TiltBridge(e.BridgeVisual, e.BridgeVisual?.RotationDegrees.X ?? 0f, 0f);

            if (e.BridgeFloor != null && GodotObject.IsInstanceValid(e.BridgeFloor))
            {
                e.BridgeFloor.CollisionLayer = 0;
            }
        }
    }

    private static void CollectFaces(Node node, List<Vector3> faces)
    {
        if (node is MeshInstance3D { Mesh: { } mesh } mi)
        {
            var xform = mi.GlobalTransform;
            foreach (var v in mesh.GetFaces()) faces.Add(xform * v);
        }
        foreach (var child in node.GetChildren()) CollectFaces(child, faces);
    }
}
