using Godot;

namespace LibreKO;

public partial class World
{
    private const double RenderFaultReportSeconds = 10;

    private int _renderFaultsSeen;
    private double _renderFaultReportAt;

    private void RenderFaultTick()
    {
        int faults = RenderFaultWatch.Faults;
        if (faults == _renderFaultsSeen) return;
        int fresh = faults - _renderFaultsSeen;
        if (Config.FxLayer)
        {
            _renderFaultsSeen = faults;
            GD.Print($"[render] missing uniform set (1) x{fresh}: zone {_zone}, fx users {FxLayer.Users}, moving {_selfMoving}; accurate effect blending off for this session");
            Config.SuspendFxLayer();
            CombatNotice("Accurate Effect Blending was turned off for this session after a rendering error.");
            return;
        }
        double now = Now();
        if (now < _renderFaultReportAt) return;
        _renderFaultReportAt = now + RenderFaultReportSeconds;
        _renderFaultsSeen = faults;
        GD.Print($"[render] missing uniform set (1) x{fresh} with accurate effect blending off: zone {_zone}, moving {_selfMoving}");
    }
}
