using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class EquipViewLayoutTests
{
    [Fact]
    public void TheCostumeGridFollowsTheRetailCells()
    {
        Assert.Equal(new[]
        {
            InventoryConstants.CosTattoo, InventoryConstants.CosHelmet, InventoryConstants.CosFairy,
            InventoryConstants.CosGloveRight, InventoryConstants.CosPauldron, InventoryConstants.CosGloveLeft,
            InventoryConstants.CosTalisman, InventoryConstants.CosWing, InventoryConstants.CosEmblem,
        }, EquipViewLayout.CostumeCells);
    }

    [Fact]
    public void LevelAndStatsReadLikeTheRetailCard()
    {
        Assert.Equal("24 / 5", EquipViewLayout.LevelLine(24, 5));
        Assert.Equal("61", EquipViewLayout.LevelLine(61, 0));
        Assert.Equal("160(+10)", EquipViewLayout.StatLine(160, 10));
        Assert.Equal("72", EquipViewLayout.StatLine(72, 0));
    }
}
