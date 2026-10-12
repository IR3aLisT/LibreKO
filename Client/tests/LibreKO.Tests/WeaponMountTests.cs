using Godot;
using Xunit;

namespace LibreKO.Tests;

public class WeaponMountTests
{
    private const float WrinkleTuarekGauntletScale = 0.643f;
    private static readonly Vector3 WrinkleTuarekGauntletPosition = new(0.127f, -0.365f, 0.029f);
    private const int Precision = 4;

    [Fact]
    public void ThePlugOffsetIsScaledWithThePlug()
    {
        var scale = new Vector3(WrinkleTuarekGauntletScale, WrinkleTuarekGauntletScale, WrinkleTuarekGauntletScale);
        var local = WeaponMount.PlugLocal(WrinkleTuarekGauntletPosition, Quaternion.Identity, scale);
        Assert.Equal(WrinkleTuarekGauntletPosition.Y * WrinkleTuarekGauntletScale, local.Origin.Y, Precision);
        Assert.Equal(WrinkleTuarekGauntletPosition.X * WrinkleTuarekGauntletScale, local.Origin.X, Precision);
    }

    [Fact]
    public void ANonUniformScaleStretchesThePlugAlongItsOwnAxes()
    {
        const float stretch = 2f;
        var quarterTurn = new Quaternion(Vector3.Up, Mathf.Pi / 2f);
        var local = WeaponMount.PlugLocal(Vector3.Zero, quarterTurn, new Vector3(stretch, 1f, 1f));
        var plugX = local.Basis * Vector3.Right;
        Assert.Equal(stretch, plugX.Length(), Precision);
        Assert.Equal(-stretch, plugX.Z, Precision);
    }
}
