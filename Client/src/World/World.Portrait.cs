using Godot;

namespace LibreKO;

public partial class World
{
    private const uint PortraitLayer = 1u << 19;
    private const int PortraitPixels = 384;
    private const float PortraitDistance = 3.4f;
    private const float PortraitEyeHeight = 1.05f;
    private const float PortraitFov = 32f;
    private const float PortraitAmbient = 0.55f;
    private const double PortraitRemarkSeconds = 0.5;

    private SubViewport? _portraitViewport;
    private Camera3D? _portraitCamera;
    private int _portraitViewers;
    private double _portraitRemarkAt;

    private Texture2D? PortraitTexture
    {
        get
        {
            EnsurePortrait();
            return _portraitViewport?.GetTexture();
        }
    }

    private void SetPortraitShown(bool shown)
    {
        _portraitViewers = Mathf.Max(0, _portraitViewers + (shown ? 1 : -1));
        EnsurePortrait();
        if (_portraitViewport == null) return;
        _portraitViewport.RenderTargetUpdateMode = _portraitViewers > 0
            ? SubViewport.UpdateMode.Always
            : SubViewport.UpdateMode.Disabled;
        _portraitRemarkAt = 0;
    }

    private void EnsurePortrait()
    {
        if (_portraitViewport != null || !IsInsideTree()) return;
        _portraitViewport = new SubViewport
        {
            Size = new Vector2I(PortraitPixels, PortraitPixels),
            TransparentBg = true,
            World3D = GetViewport().World3D,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
        };
        _portraitCamera = new Camera3D
        {
            CullMask = PortraitLayer,
            Fov = PortraitFov,
            Current = true,
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.ClearColor,
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = Colors.White,
                AmbientLightEnergy = PortraitAmbient,
            },
        };
        _portraitViewport.AddChild(_portraitCamera);
        AddChild(_portraitViewport);
    }

    private void PortraitTick(double now)
    {
        if (_portraitViewers <= 0 || _portraitCamera == null || _self == null) return;
        if (now >= _portraitRemarkAt)
        {
            _portraitRemarkAt = now + PortraitRemarkSeconds;
            MarkPortraitLayer(_self);
        }
        var front = new Vector3(_faceDir.X, 0f, _faceDir.Z);
        if (front.LengthSquared() < 0.01f) front = Vector3.Forward;
        var eye = _self.GlobalPosition + Vector3.Up * (PortraitEyeHeight - _selfLift);
        _portraitCamera.GlobalPosition = eye + front.Normalized() * PortraitDistance;
        _portraitCamera.LookAt(eye, Vector3.Up);
    }

    private static void MarkPortraitLayer(Node node)
    {
        if (node is VisualInstance3D visual) visual.Layers |= PortraitLayer;
        foreach (var child in node.GetChildren()) MarkPortraitLayer(child);
    }
}
