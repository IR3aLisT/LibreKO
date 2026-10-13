using System.Collections.Generic;
using Godot;
using LibreKO.Domain;

namespace LibreKO;

public partial class World
{
    private const float FxWarmAhead = 4f;

    private readonly HashSet<string> _warmedFx = new();

    private List<string> UnwarmedHotbarFx()
    {
        var names = new List<string>();
        foreach (int id in _hotbar)
        {
            if (id == 0 || !SkillData.IsSkill(id) || SkillData.Get(id) is not { } skill) continue;
            foreach (var name in new[] { skill.SelfFx1, skill.SelfFx2, skill.FlyingFx, skill.TargetFx })
                if (!string.IsNullOrEmpty(name) && Fx.Has(name) && _warmedFx.Add(name)) names.Add(name);
        }
        return names;
    }

    private Node3D? WarmHotbarFxInView()
    {
        var names = UnwarmedHotbarFx();
        if (names.Count == 0 || _camera == null || !IsInstanceValid(_camera)) return null;
        var warm = new Node3D { Name = "FxWarmHotbar", Position = new Vector3(0f, 0f, -FxWarmAhead) };
        _camera.AddChild(warm);
        foreach (var name in names) Fx.Spawn(name, warm, Vector3.Zero);
        GD.Print($"[load] warming {names.Count} hotbar effects");
        return warm;
    }

    private void WarmHotbarFxResources()
    {
        var names = UnwarmedHotbarFx();
        if (names.Count == 0) return;
        var warm = new Node3D { Name = "FxWarmHotbar", Visible = false };
        AddChild(warm);
        foreach (var name in names) Fx.Spawn(name, warm, Vector3.Zero);
        warm.QueueFree();
    }
}
