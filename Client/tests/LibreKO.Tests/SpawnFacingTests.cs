using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class SpawnFacingTests
{
    [Fact]
    public void AStructureWithoutADirectionKeepsItsAuthoredFacing()
    {
        Assert.True(NpcTypes.KeepsSpawnFacing(0, NpcTypes.Lever, hasWalkCycle: true));
        Assert.True(NpcTypes.KeepsSpawnFacing(0, NpcTypes.Gate, hasWalkCycle: false));
    }

    [Fact]
    public void AModelThatCannotWalkKeepsItsAuthoredFacing() =>
        Assert.True(NpcTypes.KeepsSpawnFacing(0, NpcTypes.Monster, hasWalkCycle: false));

    [Fact]
    public void AWalkingUnitWithoutADirectionGetsASpreadFacing() =>
        Assert.False(NpcTypes.KeepsSpawnFacing(0, NpcTypes.Merchant, hasWalkCycle: true));

    [Fact]
    public void AnySentDirectionIsKept() =>
        Assert.True(NpcTypes.KeepsSpawnFacing(90, NpcTypes.Merchant, hasWalkCycle: true));
}
