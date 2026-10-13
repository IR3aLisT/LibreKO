using System.Collections.Generic;
using Godot;

namespace LibreKO;

public interface IFxPart
{
    void Tick(double delta);
}

public interface IFxTicked
{
    int FxSlot { get; set; }
    void FxTick(double delta);
}

public enum FxLane { Gear, Late }

public partial class FxTickLane : Node
{
    public const int Unlisted = -1;
    private readonly List<IFxTicked> _members = new();

    internal int Count => _members.Count;
    internal System.Action? AfterTick;

    internal void Join(IFxTicked member)
    {
        if (member.FxSlot != Unlisted) return;
        member.FxSlot = _members.Count;
        _members.Add(member);
    }

    internal void Leave(IFxTicked member)
    {
        int slot = member.FxSlot;
        if (slot < 0 || slot >= _members.Count || !ReferenceEquals(_members[slot], member)) return;
        var last = _members[^1];
        _members[slot] = last;
        last.FxSlot = slot;
        _members.RemoveAt(_members.Count - 1);
        member.FxSlot = Unlisted;
    }

    public override void _Process(double delta)
    {
        if (Fx.ShuttingDown) return;
        for (int i = _members.Count - 1; i >= 0; i--)
        {
            if (i >= _members.Count) continue;
            var member = _members[i];
            if (member is GodotObject node && !GodotObject.IsInstanceValid(node))
            {
                Leave(member);
                continue;
            }
            member.FxTick(delta);
        }
        AfterTick?.Invoke();
    }
}

public partial class FxDriver : Node
{
    private const int DriverProcessPriority = 800;
    private const int FrustumPlanes = 6;

    private const int GearLanePriority = 0;
    private const int LateLanePriority = 500;

    private static readonly Dictionary<ulong, FxDriver> _perViewport = new();
    private static readonly Dictionary<(ulong, FxLane), FxTickLane> _lanes = new();
    private readonly List<FxInstance> _roots = new();
    private readonly Plane[] _planes = new Plane[FrustumPlanes];
    private int _planeCount;

    internal int Roots => _roots.Count;

    internal static FxDriver? For(Node node)
    {
        var viewport = node.GetViewport();
        if (viewport == null) return null;
        ulong id = viewport.GetInstanceId();
        if (_perViewport.TryGetValue(id, out var driver) && GodotObject.IsInstanceValid(driver)) return driver;
        driver = new FxDriver { Name = "FxDriver", ProcessPriority = DriverProcessPriority };
        _perViewport[id] = driver;
        viewport.CallDeferred(Node.MethodName.AddChild, driver);
        return driver;
    }

    internal void Add(FxInstance root) => _roots.Add(root);

    internal static FxTickLane? Lane(Node node, FxLane lane)
    {
        var viewport = node.GetViewport();
        if (viewport == null) return null;
        var key = (viewport.GetInstanceId(), lane);
        if (_lanes.TryGetValue(key, out var existing) && GodotObject.IsInstanceValid(existing)) return existing;
        var created = new FxTickLane
        {
            Name = $"FxLane{lane}",
            ProcessPriority = lane == FxLane.Late ? LateLanePriority : GearLanePriority,
        };
        _lanes[key] = created;
        viewport.CallDeferred(Node.MethodName.AddChild, created);
        return created;
    }

    internal bool Sees(Vector3 point)
    {
        if (_planeCount == 0) return false;
        for (int i = 0; i < _planeCount; i++)
            if (_planes[i].IsPointOver(point)) return false;
        return true;
    }

    public override void _Process(double delta)
    {
        if (Fx.ShuttingDown) return;
        var viewport = GetViewport();
        var camera = viewport.GetCamera3D();
        bool hasCam = camera != null;
        var camXf = hasCam ? camera!.GlobalTransform : Transform3D.Identity;
        _planeCount = 0;
        if (hasCam)
        {
            var frustum = camera!.GetFrustum();
            _planeCount = Mathf.Min(frustum.Count, FrustumPlanes);
            for (int i = 0; i < _planeCount; i++) _planes[i] = frustum[i];
        }
        Fx.PrimeFrameCamera(viewport, camera, camXf);
        Fx.BuildPendingParts();
        ulong frame = Engine.GetProcessFrames();
        for (int i = _roots.Count - 1; i >= 0; i--)
        {
            var root = _roots[i];
            if (!root.Driven)
            {
                root.Unregistered();
                _roots[i] = _roots[^1];
                _roots.RemoveAt(_roots.Count - 1);
                continue;
            }
            root.Tick(delta, frame, hasCam, camXf);
        }
    }
}
