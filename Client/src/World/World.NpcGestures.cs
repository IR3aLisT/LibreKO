using Godot;

namespace LibreKO;

public partial class World
{
    private const int NpcGestureFirst = 12;
    private const int NpcGestureLast = 15;
    private const uint NpcGestureOneIn = 10;
    private const string IdleState = "idle";

    private void TickNpcGesture(Ent e, double now)
    {
        if (!e.IsNpc || e.IsMonster || e.Statue || e.Anim == null || e.Clip != IdleState)
        {
            e.IdleLoopAt = 0;
            return;
        }
        double loop = e.Anim.CurrentAnimationLength;
        if (loop <= 0) return;
        if (e.IdleLoopAt <= 0)
        {
            e.IdleLoopAt = now + loop;
            return;
        }
        if (now < e.IdleLoopAt) return;
        e.IdleLoopAt = now + loop;
        if (GD.Randi() % NpcGestureOneIn != 0) return;
        int gesture = NpcGestureFirst + (int)(GD.Randi() % (uint)(NpcGestureLast - NpcGestureFirst + 1));
        if (AnimationNameAt(e.Anim, gesture) == null) return;
        e.IdleLoopAt = 0;
        PlaySkillAction(e.Id, gesture, System.Array.Empty<string>(), ActionRankStruck);
        e.NpcGesturing = true;
    }
}
