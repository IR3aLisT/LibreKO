namespace LibreKO;

public partial class World
{
    public const byte FreezeBuffType = 22;
    public const byte StunBuffType = 47;

    private double _stunnedUntil;

    public static bool IsStunBuffType(int buffType) => buffType is FreezeBuffType or StunBuffType;

    private bool IsStunned() => Now() < _stunnedUntil;

    private void Stun(double seconds)
    {
        if (seconds <= 0) return;
        double until = Now() + seconds;
        if (until > _stunnedUntil) _stunnedUntil = until;
        _hasMoveTarget = false;
        _terrainMoveHeld = false;
        CancelSwing();
    }

    private void ClearStun() => _stunnedUntil = 0;

    private const byte SilenceBuffType = 152;
    private const byte DisarmBuffType = 32;
    private const byte DrakeBlessingBuffType = 181;
    private const byte DrakeBlessingRenewBuffType = 182;
    private const double SilenceSeconds = 3.0;
    private const int DisarmedHandSlot = 7;
    private const int TextFrozen = 3035;
    private const int TextDrakeBlessing = 42340;
    private const int TextCastingFailed = 4002;

    private double _skillLockUntil;
    private int _disarmToken;

    private bool SkillsLocked() => Now() < _skillLockUntil;

    private void ApplySelfStatus(int buffType, int speedPercent, double seconds)
    {
        if (IsStunBuffType(buffType)) Stun(seconds);
        else if (IsSpeedBuffType(buffType)) ApplyMoveSpeedBuff(speedPercent, (int)seconds);
        StartBlind(buffType, seconds);

        switch (buffType)
        {
            case SilenceBuffType:
                _skillLockUntil = Now() + SilenceSeconds;
                break;
            case FreezeBuffType:
                CombatNotice(SystemText(TextFrozen, "You are Frozen"));
                break;
            case DrakeBlessingBuffType or DrakeBlessingRenewBuffType:
                CombatNotice(SystemText(TextDrakeBlessing, "Drake's Blessing has been used"));
                break;
            case DisarmBuffType:
                Disarm(seconds);
                break;
        }
    }

    private void Disarm(double seconds)
    {
        if (seconds <= 0) return;
        SetSelfWeaponVisible(DisarmedHandSlot, false);
        int token = ++_disarmToken;
        GetTree().CreateTimer(seconds).Timeout += () =>
        {
            if (token == _disarmToken) SetSelfWeaponVisible(DisarmedHandSlot, true);
        };
    }

    private void SetSelfWeaponVisible(int slot, bool visible)
    {
        if (_self == null || !IsInstanceValid(_self) || FindFirst<Godot.Skeleton3D>(_self) is not { } skeleton) return;
        if (skeleton.GetNodeOrNull<Godot.Node3D>($"{WeaponNodePrefix}{slot}") is { } weapon) weapon.Visible = visible;
    }
}
