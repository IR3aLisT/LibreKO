using Godot;

namespace LibreKO.Domain;

public sealed class MoveSendClock
{
    public const float SendSeconds = 1.5f;
    public const float LeadSeconds = 1.5f;
    public const float TurnCheckBelow = 1.4f;
    public const float TurnCheckBelowAlternate = 1.17f;
    public const float TurnDot = 0.6f;
    public const float RotateSeconds = 4f;
    public const float ClimbResend = 1f;
    public const float HeadingEpsilonDegrees = 0.05f;

    private float _left;
    private float _rotateLeft;
    private bool _alternate;
    private bool _hasSent;
    private Vector2 _sentFrom;
    private Vector2 _sentTo;
    private float _sentY;

    public void Tick(float delta)
    {
        _left -= delta;
        _rotateLeft -= delta;
    }

    public bool MoveDue(Vector2 position, float y, Vector2 heading)
    {
        if (!_hasSent) return true;
        bool moved = position != _sentFrom || Mathf.Abs(y - _sentY) >= ClimbResend;
        if (_left < 0f) return moved;
        if (_left >= (_alternate ? TurnCheckBelowAlternate : TurnCheckBelow) || !moved) return false;
        var sent = _sentTo - _sentFrom;
        var sentDir = sent.LengthSquared() > 0f ? sent.Normalized() : Vector2.Zero;
        var headingDir = heading.LengthSquared() > 0f ? heading.Normalized() : Vector2.Zero;
        return sentDir.Dot(headingDir) < TurnDot;
    }

    public void Sent(Vector2 from, Vector2 to, float y)
    {
        _left = SendSeconds;
        _alternate = !_alternate;
        _hasSent = true;
        _sentFrom = from;
        _sentTo = to;
        _sentY = y;
    }

    public void Stopped() => _hasSent = false;

    public bool RotateDue(float headingDegrees, float lastSentDegrees) =>
        _rotateLeft < 0f
        && (float.IsNaN(lastSentDegrees) || Mathf.Abs(Mathf.AngleDifference(
            Mathf.DegToRad(headingDegrees), Mathf.DegToRad(lastSentDegrees))) > Mathf.DegToRad(HeadingEpsilonDegrees));

    public void Rotated() => _rotateLeft = RotateSeconds;

    public static Vector2 Lead(Vector2 position, Vector2 facing, float signedSpeed)
    {
        if (facing.LengthSquared() <= 0f) return position;
        return position + facing.Normalized() * signedSpeed * LeadSeconds;
    }
}
