using Godot;

namespace LibreKO;

public partial class World
{
    private const int CastBreakRoll = 100;

    private void BreakCastByChance()
    {
        int skillId = _castingSkillId;
        if (skillId == 0 || SkillData.Get(skillId) is not { HasCastPhase: true } s) return;
        if (CastInterrupt.IsReleased(Now(), _castingUntil, PendingCastPreEffect(skillId))) return;
        if ((int)(GD.Randi() % CastBreakRoll) < s.Success) return;
        CancelSelfCast(skillId);
        CombatNotice(SystemText(TextCastingFailed, "Casting failed"));
    }
}
