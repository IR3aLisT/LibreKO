using System.Collections.Generic;
using Godot;

namespace LibreKO;

public partial class World
{
    private const double HitFlashSeconds = 0.3;
    private static readonly Color HitFlashColour = new(1f, 0.2f, 0.2f);
    private static readonly StringName HitFlashTint = "tint";
    private const string HitFlashCode =
        "shader_type spatial;\n"
        + "render_mode unshaded, blend_mul, depth_draw_never, cull_back, shadows_disabled, fog_disabled;\n"
        + "uniform vec4 tint : source_color = vec4(1.0);\n"
        + "void fragment() { ALBEDO = tint.rgb; }\n";

    private static Shader? _hitFlashShader;

    private sealed class HitFlashState
    {
        public double Start;
        public ShaderMaterial Material = null!;
        public readonly List<GeometryInstance3D> Parts = new();
    }

    private readonly Dictionary<int, HitFlashState> _hitFlashes = new();
    private readonly List<int> _hitFlashDone = new();

    private void HitFlash(int id)
    {
        if (!_ents.TryGetValue(id, out var e) || !IsInstanceValid(e.Body)) return;
        if (_hitFlashes.TryGetValue(id, out var running))
        {
            running.Start = Now();
            return;
        }
        var state = new HitFlashState
        {
            Start = Now(),
            Material = new ShaderMaterial { Shader = _hitFlashShader ??= new Shader { Code = HitFlashCode } },
        };
        state.Material.SetShaderParameter(HitFlashTint, HitFlashColour);
        foreach (var node in e.Body.FindChildren("*", nameof(MeshInstance3D), true, false))
        {
            if (node is not MeshInstance3D mesh || mesh.MaterialOverlay != null || CutOut(mesh)) continue;
            mesh.MaterialOverlay = state.Material;
            state.Parts.Add(mesh);
        }
        if (state.Parts.Count > 0) _hitFlashes[id] = state;
    }

    private static bool CutOut(MeshInstance3D mesh)
    {
        if (mesh.Mesh == null) return true;
        for (int i = 0; i < mesh.Mesh.GetSurfaceCount(); i++)
            if ((mesh.GetActiveMaterial(i) as BaseMaterial3D)?.Transparency is { } mode && mode != BaseMaterial3D.TransparencyEnum.Disabled)
                return true;
        return false;
    }

    private void HitFlashTick(double now)
    {
        if (_hitFlashes.Count == 0) return;
        foreach (var (id, state) in _hitFlashes)
        {
            float t = (float)((now - state.Start) / HitFlashSeconds);
            if (t < 1f)
            {
                state.Material.SetShaderParameter(HitFlashTint, HitFlashColour.Lerp(Colors.White, t));
                continue;
            }
            foreach (var part in state.Parts)
                if (IsInstanceValid(part) && part.MaterialOverlay == state.Material) part.MaterialOverlay = null;
            _hitFlashDone.Add(id);
        }
        foreach (int id in _hitFlashDone) _hitFlashes.Remove(id);
        _hitFlashDone.Clear();
    }
}
