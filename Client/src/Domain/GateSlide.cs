using System;

namespace LibreKO.Domain;

public sealed class GateSlide
{
    public const float OpenHeight = 10f;
    public const float Speed = 0.5f;
    public const float Wobble = 0.05f;
    public const float WobbleRate = 18.849556f;
    public const float Bounce = 0.3f;
    public const float SettlePhase = 0.3f;

    public float Current { get; private set; }
    public float Target { get; private set; }
    public float Phase { get; private set; }
    public bool BounceArmed { get; private set; } = true;

    public bool Moving => Current != Target;

    public void Place(bool open)
    {
        Current = Target = open ? OpenHeight : 0f;
        Phase = 0f;
        BounceArmed = true;
    }

    public void SetOpen(bool open)
    {
        Target = open ? OpenHeight : 0f;
        Phase = 0f;
        BounceArmed = true;
    }

    public bool Step(float delta)
    {
        if (!Moving) return false;
        Phase = Phase <= MathF.PI ? Phase + delta * WobbleRate : 0f;
        float step = MathF.Sin(Phase) * Wobble + delta * Speed;
        bool down = Current > Target;
        float next = down ? Current - step : Current + step;
        if (down ? Target < next : next < Target)
        {
            Current = next;
            return false;
        }
        if (BounceArmed && down)
        {
            BounceArmed = false;
            Current = next + Bounce;
            return true;
        }
        Phase = SettlePhase;
        Current = Target;
        BounceArmed = true;
        return false;
    }
}
