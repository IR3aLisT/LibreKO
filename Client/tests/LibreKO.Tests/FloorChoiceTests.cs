using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class FloorChoiceTests
{
    private const float Terrain = 10f;
    private static readonly float[] BridgeAndRoof = { 21f, 13f };

    [Fact]
    public void APlayerUnderTheBridgeStaysOnTheGround() =>
        Assert.Equal(Terrain, FloorChoice.Nearest(Terrain, BridgeAndRoof, 10.2f));

    [Fact]
    public void APlayerOnTheBridgeStandsOnTheBridge() =>
        Assert.Equal(13f, FloorChoice.Nearest(Terrain, BridgeAndRoof, 12.8f));

    [Fact]
    public void WithNoFloorsTheTerrainIsKept() =>
        Assert.Equal(Terrain, FloorChoice.Nearest(Terrain, System.Array.Empty<float>(), 30f));

    [Fact]
    public void LoginTakesTheFloorNearestTheSavedHeight() =>
        Assert.Equal(21f, FloorChoice.Login(Terrain, BridgeAndRoof, 20.5f));

    [Fact]
    public void LoginNeverSinksBelowTheTerrain() =>
        Assert.Equal(Terrain, FloorChoice.Login(Terrain, new[] { 4f }, 4f));

    [Fact]
    public void AJuraidBridgeStandsOnItsAbutmentAboveTheTerrain() =>
        Assert.Equal(18.9f, FloorChoice.Login(15.48f, new[] { 18.9f, -7.4f, -8.9f }, 15.48f + 1f));
}
