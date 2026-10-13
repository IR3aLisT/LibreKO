using Godot;

namespace LibreKO;

public partial class FxWeaponGlow : Node3D, IFxTicked
{
    public int FxSlot { get; set; } = FxTickLane.Unlisted;

    private const int Layers = 3;
    private const int Tails = 2;
    private const float RollDegrees = 5f;
    private const float WobbleHz = 1.2f;
    private const float WobbleSpan = 0.07f;
    private const float WobbleBias = 0.035f;
    private const float ScatterBias = 0.25f;
    private const float ScatterDiv = 100f;
    private const int ScatterMod = 50;
    private const float TexPhaseMax = 10f;
    private const float ScaleFactor = 0.7f;

    private readonly System.Collections.Generic.List<Node3D> _tails = new();
    private MeshInstance3D[] _shine = System.Array.Empty<MeshInstance3D>();
    private Node3D[] _meshes = System.Array.Empty<Node3D>();
    private string _fxName = "";
    private int _renderFlags;
    private int _blendWord;
    private bool _reduced;
    private const int ReducedLayers = 1;
    private const int ReducedTails = 1;

    private static readonly System.Collections.Generic.Dictionary<(string Fx, int Layer, int Frame), ShaderMaterial> SharedMaterials =
        Shutdown.Track(new System.Collections.Generic.Dictionary<(string Fx, int Layer, int Frame), ShaderMaterial>());
    private int[] _shown = System.Array.Empty<int>();
    private Texture2D?[] _frames = System.Array.Empty<Texture2D?>();
    private float _fps = 1f;
    private double _age;
    private double _texAge;
    private Shell? _shell;
    private Aabb _bounds;

    public static Node3D? Create(string fxName, string tailFxName, string guideStem,
                                 string guideDir = WeaponGuideDir)
    {
        if (BuildShine(fxName, guideStem, guideDir) is not { } root) return null;
        root.AddTails(tailFxName);
        return FxRegistry.Track(root, fxName, "weapon glow");
    }

    public static Node3D? CreateTailOnly(string tailFxName, Aabb bounds)
    {
        if (tailFxName.Length == 0 || bounds.Size == Vector3.Zero) return null;
        var root = new FxWeaponGlow { Name = $"fx_{tailFxName}_scatter", _bounds = bounds };
        root.AddTails(tailFxName);
        return root._tails.Count > 0 ? FxRegistry.Track(root, tailFxName, "weapon glow tail") : null;
    }

    private sealed class Shell(Mesh mesh)
    {
        public readonly Mesh Mesh = mesh;
        public Vector3 Normal = Vector3.Up;
    }

    private static readonly System.Collections.Generic.Dictionary<string, Shell?> ShellCache =
        Shutdown.Track(new System.Collections.Generic.Dictionary<string, Shell?>(), shell => shell?.Mesh);

    private const string WeaponGuideDir = "res://assets/items/weapon/fxguide";

    private static Shell? GuideShell(string guideStem, string guideDir)
    {
        string guidePath = $"{guideDir}/{guideStem}.glb";
        if (ShellCache.TryGetValue(guidePath, out var cached)) return cached;

        if (!ResourceLoader.Exists(guidePath) || ResourceLoader.Load(guidePath) is not PackedScene guideScene)
        {
            ShellCache[guidePath] = null;
            return null;
        }

        Node3D? guide = null;
        try { guide = guideScene.Instantiate<Node3D>(); }
        catch (System.InvalidOperationException e) { GD.PushWarning($"[fx] guide '{guideStem}' instantiate failed: {e.Message}"); }
        if (guide == null) return null;

        var mesh = FindMesh(guide)?.Mesh;
        guide.QueueFree();
        if (mesh == null) return null;
        var shell = new Shell(mesh);
        ShellCache[guidePath] = shell;
        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                var normal = FirstNormal(mesh);
                Callable.From(() => shell.Normal = normal).CallDeferred();
            }
            catch (System.Exception e) { GD.PushWarning($"[fx] guide '{guideStem}' normal unreadable: {e.Message}"); }
        });
        return shell;
    }

    private static FxWeaponGlow? BuildShine(string fxName, string guideStem, string guideDir)
    {
        if (GuideShell(guideStem, guideDir) is not { } shell || Fx.LoadDescriptor(fxName) is not { } descriptor)
            return null;

        Godot.Collections.Dictionary? part = null;
        foreach (var value in descriptor["parts"].AsGodotArray())
        {
            var candidate = value.AsGodotDictionary();
            if (candidate["type"].AsString() == "BillBoard")
            {
                part = candidate;
                break;
            }
        }
        if (part == null) return null;

        var root = new FxWeaponGlow
        {
            Name = $"fx_{fxName}_guide",
            _bounds = shell.Mesh.GetAabb(),
            _shell = shell,
            _shine = new MeshInstance3D[Layers],
            _meshes = new Node3D[Layers],
            _shown = new[] { -1, -1, -1 },
            _fxName = fxName,
            _renderFlags = FxShading.RenderFlags(part) | Fx.RfDoubleSided | Fx.RfNotZWrite,
            _blendWord = FxShading.Packed(part),
        };
        var textures = part["tex"].AsGodotArray();
        root._frames = new Texture2D?[textures.Count];
        for (int i = 0; i < textures.Count; i++)
            root._frames[i] = Fx.FrameTexture(part, i);
        root._fps = Mathf.Max(0.01f, Fx.ReadF(part, "texFPS", 1f));
        root._texAge = GD.Randf() * TexPhaseMax;

        for (int i = 0; i < Layers; i++)
        {
            var layer = new Node3D { Name = $"layer{i}" };
            root._meshes[i] = layer;
            root.AddChild(layer);
            var shine = new MeshInstance3D
            {
                Name = $"shine{i}",
                Mesh = shell.Mesh,
                MaterialOverride = root.SharedMaterial(i, 0),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Layers = FxShading.LayerBit,
            };
            root._shine[i] = shine;
            layer.AddChild(shine);
        }
        return root;
    }

    private ShaderMaterial SharedMaterial(int layer, int frame)
    {
        var key = (_fxName, layer, frame);
        if (SharedMaterials.TryGetValue(key, out var material)) return material;
        material = FxShading.SurfaceMaterial(_renderFlags, _blendWord, _frames.Length > frame ? _frames[frame] : null);
        material.RenderPriority = layer;
        SharedMaterials[key] = material;
        return material;
    }

    private void SetReduced(bool reduced)
    {
        if (_reduced == reduced) return;
        _reduced = reduced;
        for (int i = ReducedLayers; i < _meshes.Length; i++)
            if (IsInstanceValid(_meshes[i])) _meshes[i].Visible = !reduced;
        for (int i = ReducedTails; i < _tails.Count; i++)
            if (IsInstanceValid(_tails[i])) _tails[i].Visible = !reduced;
    }

    private bool _layerUser;

    public override void _EnterTree()
    {
        FxDriver.Lane(this, FxLane.Gear)?.Join(this);
        if (_shine.Length == 0 || _layerUser) return;
        _layerUser = true;
        FxLayer.Users++;
    }

    public override void _ExitTree()
    {
        FxDriver.Lane(this, FxLane.Gear)?.Leave(this);
        if (!_layerUser) return;
        _layerUser = false;
        FxLayer.Users--;
    }

    private void AddTails(string tailFxName)
    {
        if (tailFxName.Length == 0 || _bounds.Size == Vector3.Zero) return;
        float scale = 1f;
        if (Fx.LoadDescriptor(tailFxName) is { } d
            && d.ContainsKey("scaleMode") && d["scaleMode"].AsInt32() != 0)
            scale = (_bounds.Size.Y + _bounds.Size.Z) * ScaleFactor;
        for (int i = 0; i < Tails; i++)
        {
            if (Fx.Spawn(tailFxName, this, ScatterPoint(), sizeScale: scale) is { } tail)
                _tails.Add(tail);
        }
    }

    private Vector3 ScatterPoint()
    {
        var p = Vector3.Zero;
        for (int axis = 0; axis < 3; axis++)
        {
            float u = ScatterBias + Scatter.Next(ScatterMod) / ScatterDiv;
            p[axis] = _bounds.Position[axis] + _bounds.Size[axis] * u;
        }
        return p;
    }

    private const float GlowNearDist = 25f;
    private const int GlowFarEvery = 4;
    private const int GlowDistCheckFrames = 16;
    private static readonly System.Random Scatter = new();
    private int _frame;
    private bool _far;

    public void FxTick(double delta)
    {
        using var scope = Perf.Measure(Perf.Section.Gear);
        if (Fx.ShuttingDown) return;
        _age += delta;
        _texAge += delta;
        _frame++;
        if (_frame % GlowDistCheckFrames == 0)
        {
            _far = Fx.FrameCamera(this, out var cam)
                && cam.Origin.DistanceSquaredTo(GlobalPosition) > GlowNearDist * GlowNearDist;
            SetReduced(_far);
        }
        if (_far && _frame % GlowFarEvery != 0) return;

        for (int i = _tails.Count - 1; i >= 0; i--)
        {
            if (!IsInstanceValid(_tails[i])) { _tails.RemoveAt(i); continue; }
            _tails[i].Position = ScatterPoint();
        }

        if (_frames.Length == 0 || _meshes.Length < Layers) return;

        float phase = (float)(_age * WobbleHz);
        float wobble = (phase - Mathf.Floor(phase)) * WobbleSpan - WobbleBias;
        var axis = _shell?.Normal ?? Vector3.Up;
        _meshes[1].Transform = new Transform3D(
            new Basis(Vector3.Up, Mathf.DegToRad(-RollDegrees)), axis * wobble);
        _meshes[2].Transform = new Transform3D(
            new Basis(Vector3.Up, Mathf.DegToRad(RollDegrees)), axis * -wobble);

        int frame = Mathf.PosMod((int)(_texAge * _fps), _frames.Length);
        int layersShown = _reduced ? ReducedLayers : Layers;
        for (int i = 0; i < layersShown; i++)
        {
            int f = (frame + i) % _frames.Length;
            if (f == _shown[i]) continue;
            _shown[i] = f;
            if (_frames[f] == null) continue;
            _shine[i].MaterialOverride = SharedMaterial(i, f);
        }
    }

    private static Vector3 FirstNormal(Mesh shell)
    {
        if (shell.GetSurfaceCount() < 1) return Vector3.Up;
        var arrays = shell.SurfaceGetArrays(0);
        int slot = (int)Mesh.ArrayType.Normal;
        if (arrays.Count <= slot || arrays[slot].VariantType != Variant.Type.PackedVector3Array)
            return Vector3.Up;
        var normals = arrays[slot].AsVector3Array();
        if (normals.Length == 0) return Vector3.Up;
        var n = normals[0];
        return n.LengthSquared() > 1e-6f ? n.Normalized() : Vector3.Up;
    }

    private static MeshInstance3D? FindMesh(Node node)
    {
        if (node is MeshInstance3D mesh) return mesh;
        foreach (var child in node.GetChildren())
            if (FindMesh(child) is { } found)
                return found;
        return null;
    }
}
