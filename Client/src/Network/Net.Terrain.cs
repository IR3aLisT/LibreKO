using System;
using LibreKO.Domain;

namespace LibreKO.Network;

public partial class Net
{
    public event Action<GroundEffect>? GroundEffectEvent;
    public GroundEffect GroundEffect { get; private set; }
    public float GroundSpeedFactor { get; private set; } = 1f;

    private void HandleZoneTerrain(Packet p)
    {
        if (p.RemainingBytes < 1) return;
        byte sub = p.ReadByte();
        byte value = p.RemainingBytes >= 1 ? p.ReadByte() : (byte)GroundEffect.None;
        if (GroundEffects.Read(sub, value) is not { } effect) return;
        GroundEffect = effect;
        GroundSpeedFactor = GroundEffects.SpeedFactor(effect, GroundSpeedFactor);
        GroundEffectEvent?.Invoke(effect);
    }

    private void ForgetGroundEffect()
    {
        GroundEffect = GroundEffect.None;
        GroundSpeedFactor = 1f;
    }
}
