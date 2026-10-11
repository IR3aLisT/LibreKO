using Godot;

namespace LibreKO;

public static class WeaponIdle
{
    public static void Start(Node3D weapon, string clip)
    {
        if (clip.Length == 0) return;
        foreach (var node in weapon.FindChildren("*", nameof(AnimationPlayer), true, false))
        {
            if (node is not AnimationPlayer anim || !anim.HasAnimation(clip)) continue;
            if (anim.GetAnimation(clip) is { } loop) loop.LoopMode = Animation.LoopModeEnum.Linear;
            anim.Play(clip);
            return;
        }
    }
}
