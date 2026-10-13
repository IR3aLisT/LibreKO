using System;

namespace LibreKO.Domain;

public sealed class CameraShake
{
    public const float GateSeconds = 5f;
    public const float GateAmplitude = 0.01f;
    public const float GateRate = 10f;
    public const float LookFactor = 2f;

    private float _left;
    private float _phase;
    private float _amplitude;
    private float _rate;

    public bool Active { get; private set; }

    public void Start(float seconds, float amplitude, float rate)
    {
        Active = true;
        _left = seconds;
        _amplitude = amplitude;
        _rate = rate;
    }

    public float Step(float delta)
    {
        if (!Active) return 0f;
        float offset = -MathF.Sin(_phase) * _amplitude;
        _left -= delta;
        if (_phase < MathF.PI)
        {
            _phase += _rate * MathF.PI * delta;
        }
        else
        {
            _phase = 0f;
            if (_left < 0f)
            {
                Active = false;
                _left = 0f;
            }
        }
        return offset;
    }
}
