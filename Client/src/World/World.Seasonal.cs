using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private void SeasonalInit() => Net.I.SantaEvent += OnSanta;

    private void SeasonalDispose() => Net.I.SantaEvent -= OnSanta;

    private void OnSanta(int state)
    {
        if (Seasonal.FxFor(state) is { } fxId && Fx.NameForId(fxId) is { } fx)
            SpawnFxOn(_myId, fx, 0f);
    }
}
