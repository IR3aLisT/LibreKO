using System;
using System.Collections.Generic;
using Godot;

namespace LibreKO;

public partial class World
{
    private const float WalkSpeed = 1.5f;
    private const float RunSpeed = 4.5f;
    private const float GmSpeedMult = 5.0f;

    private const float CapsuleHalf = 1.0f;
    private const uint WorldCollisionLayer = 1;
    private const uint BlockerCollisionLayer = 2;
    private const float PlayerCapsuleRadius = 0.4f;
    private const float PlayerCapsuleHeight = 1.3f;
    private const float PlayerCapsuleOffsetY = 1.15f;
    private const float BlockedProgressFraction = 0.25f;
    private Node3D _self = null!;
    private CharacterBody3D? _selfBody;
    private Node3D _selfVisual = null!;
    private Transform3D _selfStandingVisualTransform;
    private CapsuleShape3D? _selfCapsule;
    private float _selfCapsuleOffsetY;
    private Vector3 _lastFreePos;
    private AnimationPlayer? _selfAnim;
    private string? _selfClip;
    private double _selfActionUntil;
    private int _selfActionRank;
    private int _selfActionAnim = NoActionAnim;
    private Flinch? _selfFlinch;
    private float _selfLift = CapsuleHalf;
    private bool _selfMoving;
    private bool _selfMovingBackward;
    private bool _selfDead;

    private float _myKoX, _myKoZ, _myKoY;
    private float _lastKoX, _lastKoZ;
    private readonly MoveSendClock _moveClock = new();
    private bool _moveSent;
    private float _lastSentHeading = float.NaN;
    private bool _running = true;
    private bool _isGm;
    private bool _collisionsOff;
    private bool NoClip => _isGm && _collisionsOff;

    private bool GmSpeedHeld => (_isGm || Net.I.GmSpeedGranted) && Held(KeyAction.GmSpeed);
    private Vector3 _moveWish;
    private Vector3 _faceDir = Vector3.Forward;
    private Vector3 _attackLungeDir;
    private double _attackLungeUntil;
    private void HandleInput(double delta)
    {
        _movePressedEdge = false;
        _walkPressedEdge = false;
        _walkKeyHeld = false;

        if (_selfDead) { _selfMoving = false; _moveSent = false; return; }
        if (TickSelfDash()) return;
        if (TradeHoldsInput)
        {
            _selfMoving = false;
            _hasMoveTarget = false;
            _terrainMoveHeld = false;
            _autoMoveForward = false;
            SendMoveStop();
            return;
        }
        bool typing = GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit or SpinBox;
        if (typing && !WhisperInputHasFocus() && !Chat.IsActive) { _selfMoving = false; SendMoveStop(); return; }

        if (_selfSitting)
        {
            if (!SeatedMoveIntent())
            {
                _selfMoving = false;
                _moveWish = Vector3.Zero;
                if (_selfBody != null) _selfBody.Velocity = Vector3.Zero;
                SendMoveStop();
                return;
            }
            StandUp();
        }

        var wish = ProfileMoveWish(delta, typing);
        _moveWish = wish.LengthSquared() > 0.01f ? wish.Normalized() : Vector3.Zero;

        _selfMoving = wish != Vector3.Zero;
        if (_selfMoving) CloseInteractionDialogs();
        if (wish == Vector3.Zero && !_turning)
        {
            SettleSelfFall(delta);
            SendMoveStop();
            return;
        }

        wish = _moveWish;
        float speed = _selfMovingBackward ? WalkSpeed : _running ? RunSpeed : WalkSpeed;
        speed *= MoveSpeedMultiplier();
        if (GmSpeedHeld)
            speed *= GmSpeedMult;
        if (IsRootedByCast() || IsStunned() || TouchLocked()) speed = 0f;

        var wasAt = _self.Position;
        if (_selfBody != null)
        {
            _selfBody.Velocity = wish * speed * MoveAndSlideScale(delta);
            _selfBody.MoveAndSlide();
        }
        else
            _self.Position += wish * speed * (float)delta;

        var resolved = _self.Position;
        var grounded = SelfGroundAt(resolved);
        grounded.Y = FallToward(resolved.Y, grounded.Y, delta);
        _self.Position = BlockedByUnits(wasAt, grounded) ? wasAt : grounded;

        if (CapsuleOverlaps() && !StepOffLedge(grounded, resolved.Y))
            _self.Position = _lastFreePos;
        else
            _lastFreePos = _self.Position;

        var (koX, koZ) = WorldToKo(_self.Position);
        _myKoX = koX; _myKoZ = koZ; _myKoY = _self.Position.Y - _selfLift;

        if (!IsRootedByCast() && (_selfMoving || _turning))
            SnapSelfToFaceDir();

        if (TrackBlockedProgress(wasAt, speed, delta))
        {
            _selfMoving = false;
            speed = 0f;
        }

        bool travelling = _selfMoving && speed > 0f;
        _moveClock.Tick((float)delta);
        if (!travelling && _moveSent)
            SendMoveStop();
        else if (travelling)
        {
            float signedSpeed = _selfMovingBackward ? -speed : speed;
            var koPos = new Vector2(koX, koZ);
            var facing = KoFacing(koX, koZ);
            if (!_moveSent || _moveClock.MoveDue(koPos, _myKoY, _selfMovingBackward ? -facing : facing))
            {
                var lead = MoveSendClock.Lead(koPos, facing, signedSpeed);
                Net.I.SendMove(lead.X, lead.Y, _myKoY, signedSpeed,
                    _moveSent ? Net.MoveEchoMove : Net.MoveEchoStart, koX, koZ, _myKoY);
                _moveClock.Sent(koPos, lead, _myKoY);
                _moveSent = true;
            }
            _lastKoX = koX; _lastKoZ = koZ;
        }
        SendRotateWhenDue(koX, koZ);
    }

    private Vector2 KoFacing(float koX, float koZ)
    {
        var (faceX, faceZ) = WorldToKo(_self.Position + _faceDir);
        return new Vector2(faceX - koX, faceZ - koZ);
    }

    private void SendRotateWhenDue(float koX, float koZ)
    {
        var facing = KoFacing(koX, koZ);
        float koHeading = Coord.KoHeading(facing.X, facing.Y);
        if (!_moveClock.RotateDue(koHeading, _lastSentHeading)) return;
        Net.I.SendRotate(koHeading);
        _lastSentHeading = koHeading;
        _moveClock.Rotated();
    }

    private float MoveAndSlideScale(double step)
    {
        double frame = GetProcessDeltaTime();
        return frame > 0 ? (float)(step / frame) : 1f;
    }

    private void SendMoveStop()
    {
        if (!_moveSent) return;
        _moveSent = false;
        _moveClock.Stopped();
        Net.I.SendMove(_myKoX, _myKoZ, _myKoY, 0f, Net.MoveEchoFinish);
    }

    private float _selfFallSpeed;

    private Vector3 SelfGroundAt(Vector3 resolved)
    {
        var (koX, koZ) = WorldToKo(resolved);
        var grounded = GroundPos(koX, koZ, resolved.Y - _selfLift, _selfLift);
        float objY = ObjectFloorY(grounded, resolved.Y - _selfLift);
        if (objY > grounded.Y || (_zone == JuraidZone && objY > float.NegativeInfinity / 2f)) grounded.Y = objY + _selfLift;
        return grounded;
    }

    private void SettleSelfFall(double delta)
    {
        if (_selfFallSpeed <= 0f || _self == null) return;
        var at = _self.Position;
        at.Y = FallToward(at.Y, SelfGroundAt(at).Y, delta);
        _self.Position = at;
        _lastFreePos = at;
        _myKoY = at.Y - _selfLift;
    }

    private float FallToward(float y, float groundY, double delta) =>
        Fall.Step(ref _selfFallSpeed, y, groundY, (float)delta);

    private bool StepOffLedge(Vector3 below, float edgeY)
    {
        if (edgeY <= below.Y + StepUp) return false;
        _self.Position = new Vector3(below.X, edgeY, below.Z);
        return !CapsuleOverlaps();
    }

    private bool TrackBlockedProgress(Vector3 wasAt, float speed, double delta)
    {
        var moved = new Vector2(_self.Position.X - wasAt.X, _self.Position.Z - wasAt.Z);
        if (!_selfMoving || speed <= 0f
            || moved.Length() >= speed * (float)delta * BlockedProgressFraction)
            return false;

        _hasMoveTarget = false;
        _autoMoveForward = false;
        return true;
    }

    private const float HeadingResendRadians = 0.2f;

    private void SendHeadingIfChanged(float koX, float koZ)
    {
        var (faceX, faceZ) = WorldToKo(_self.Position + _faceDir);
        float koHeading = Coord.KoHeading(faceX - koX, faceZ - koZ);
        if (!float.IsNaN(_lastSentHeading) && Mathf.Abs(Mathf.AngleDifference(
                Mathf.DegToRad(koHeading), Mathf.DegToRad(_lastSentHeading))) <= HeadingResendRadians)
            return;
        Net.I.SendRotate(koHeading);
        _lastSentHeading = koHeading;
        _moveClock.Rotated();
    }

    private bool SeatedMoveIntent() =>
        _hasMoveTarget || _terrainMoveHeld || _autoMoveForward
        || Held(KeyAction.MoveForward) || Held(KeyAction.MoveBackward);

    private void OnWarp(float koX, float koZ)
    {
        if (_self == null) return;
        var pos = EntityGroundPos(koX, koZ, NoServerHeight, _selfLift);
        float objY = ObjectFloorY(pos, pos.Y - _selfLift);
        if (objY > pos.Y || (_zone == JuraidZone && objY > float.NegativeInfinity / 2f)) pos.Y = objY + _selfLift;
        _self.Position = pos;
        _lastFreePos = pos;
        _lastKoX = koX; _lastKoZ = koZ;
        _myKoX = koX; _myKoZ = koZ; _myKoY = pos.Y - _selfLift;
        if (Now() < _blinkWarpUntil)
        {
            _blinkWarpUntil = 0;
            return;
        }
        Fx.Spawn(Net.I.LastEnter.Nation == Nations.Karus ? "warp_ka" : "warp_el", _self, Vector3.Zero,
            oneShot: true);
        Audio.Play(Sfx.WarpZone, pos);
    }

    private static CollisionShape3D BodyCapsule(float radius, float lift) => new()
    {
        Shape = new CapsuleShape3D { Radius = radius, Height = PlayerCapsuleHeight },
        Position = new Vector3(0, PlayerCapsuleOffsetY - lift, 0),
    };

    private static CharacterBody3D MakePlayerBody(Node3D visual)
    {
        var body = new CharacterBody3D { MotionMode = CharacterBody3D.MotionModeEnum.Floating };
        body.AddChild(BodyCapsule(PlayerCapsuleRadius, 0f));
        body.AddChild(visual);
        return body;
    }

    private void ApplyCollisionPolicy()
    {
        if (_selfBody == null) return;
        _selfBody.CollisionMask = NoClip ? 0u : WorldCollisionLayer | BlockerCollisionLayer;
    }

    private void SetCollisions(bool on)
    {
        if (!_isGm) return;
        _collisionsOff = !on;
        Net.I.GmCollisionsOff = _collisionsOff;
        ApplyCollisionPolicy();
        RefreshAdminCollisionSwitch();
        CombatNotice(on ? "Collision enabled." : "Collision disabled — you now walk through everything.");
    }

    private const string VaultCommand = "vault";

    private bool RunLocalCommand(string command)
    {
        string[] parts = command.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;

        if (parts[0].Equals("leave", System.StringComparison.OrdinalIgnoreCase)
            || parts[0].Equals("exit", System.StringComparison.OrdinalIgnoreCase))
        {
            if (IsTempleEventZone(_zone))
            {
                OnInZoneLeavePressed();
                return true;
            }
        }

        if (CommanderOrders.SlashSlot(parts[0], ItemData.Text) is var commanderSlot and not CommanderOrders.NoSlot)
        {
            if (AmCommander) UseCommanderSlot(commanderSlot);
            return true;
        }

        if (parts[0].Equals(VaultCommand, System.StringComparison.OrdinalIgnoreCase))
        {
            OpenVipVaultByCommand();
            return true;
        }

        if (CommandCatalog.Find(parts[0], ItemData.Text) is { } catalogCommand
            && RunCatalogCommand(catalogCommand, command[parts[0].Length..].Trim()))
            return true;

        if (!parts[0].Equals("collision", System.StringComparison.OrdinalIgnoreCase))
            return false;
        if (!_isGm) return false;
        bool on = parts.Length < 2
            ? _collisionsOff
            : parts[1].Equals("on", System.StringComparison.OrdinalIgnoreCase);
        SetCollisions(on);
        return true;
    }
}
