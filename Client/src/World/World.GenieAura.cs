using Godot;

namespace LibreKO;

public partial class World
{
    private const int GenieAuraFxId = 30745;
    private const int GenieAuraKey = -GenieAuraFxId;
    private const int GenieAuraPhase = 0;
    private const int GenieAuraOnBody = -1;

    private void OnGenieAura(int entityId, bool on)
    {
        if (entityId != _myId) ShowGenieAura(entityId, on);
    }

    private void ShowGenieAura(int entityId, bool on)
    {
        if (on) _genieUsers.Add(entityId);
        else _genieUsers.Remove(entityId);
        bool showing = _skillFx.TryGetValue((entityId, GenieAuraKey, GenieAuraPhase), out var nodes)
            && nodes.Exists(GodotObject.IsInstanceValid);
        if (on == showing) return;
        StopSkillFx(entityId, GenieAuraKey);
        if (on && Fx.NameForId(GenieAuraFxId) is { } fxName)
            SpawnOwnedFxPart(entityId, GenieAuraKey, GenieAuraPhase, fxName, GenieAuraOnBody,
                worldSentinel: false, oneShot: false);
    }
}
