using System;

namespace LibreKO.Network;

public partial class Net
{
    public event Action<EffectReply>? EffectEvent;

    private void HandleEffect(Packet p)
    {
        if (EffectReply.Read(p) is { } reply) EffectEvent?.Invoke(reply);
    }
}
