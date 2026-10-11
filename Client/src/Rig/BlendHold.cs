using Godot;

namespace LibreKO;

public static class BlendHold
{
    private const string GenerationMeta = "blend_hold_generation";
    private const string UntilMeta = "blend_hold_until";
    private const float Held = 0f;
    private const float Running = 1f;
    private const double MsPerSecond = 1000.0;

    private static double Now => Time.GetTicksMsec() / MsPerSecond;

    public static void Play(AnimationPlayer anim, string name, double blend, bool hold = true)
    {
        int generation = anim.GetMeta(GenerationMeta, 0).AsInt32() + 1;
        anim.SetMeta(GenerationMeta, generation);
        if (!hold || blend <= 0 || !anim.IsInsideTree())
        {
            anim.SetMeta(UntilMeta, 0.0);
            anim.Play(name, blend);
            return;
        }
        anim.SetMeta(UntilMeta, Now + blend);
        anim.Play(name, blend, Held);
        anim.GetTree().CreateTimer(blend).Timeout += () => Resume(anim, name, generation);
    }

    public static double Remaining(AnimationPlayer? anim) =>
        anim == null ? 0 : System.Math.Max(0, anim.GetMeta(UntilMeta, 0.0).AsDouble() - Now);

    public static void Release(AnimationPlayer anim)
    {
        if (Remaining(anim) <= 0) return;
        Resume(anim, anim.CurrentAnimation, anim.GetMeta(GenerationMeta, 0).AsInt32());
    }

    private static void Resume(AnimationPlayer anim, string name, int generation)
    {
        if (!GodotObject.IsInstanceValid(anim) || anim.GetMeta(GenerationMeta, 0).AsInt32() != generation) return;
        anim.SetMeta(UntilMeta, 0.0);
        if (anim.CurrentAnimation == name) anim.Play(name, -1, Running);
    }
}
