using LibreKO.Domain;
using LibreKO.Network;
using Xunit;

namespace LibreKO.Tests;

public class HealStrikeTests
{
    private static Packet StatBlock(byte healStrikes)
    {
        var p = new Packet(GameOpcodes.GS_ITEM_MOVE);
        p.WriteShort(120);
        p.WriteShort(80);
        p.WriteInt(5000);
        p.WriteByte(healStrikes);
        p.WriteByte(0);
        p.WriteShort(2400);
        p.WriteShort(1800);
        for (int i = 0; i < 11; i++) p.WriteShort((short)i);
        p.ResetOffset();
        return p;
    }

    [Fact]
    public void TheByteAfterTheMaxWeightIsTheHealStrikeFlag()
    {
        Assert.True(ItemStatsBlock.TryRead(StatBlock(1), out var armed));
        Assert.True(armed.HealStrikesEnemies);
        Assert.Equal(5000, armed.MaxWeight);
        Assert.Equal(2400, armed.MaxHp);
        Assert.Equal(1800, armed.MaxMp);
        Assert.Equal(10, armed.PoisonR);

        Assert.True(ItemStatsBlock.TryRead(StatBlock(0), out var plain));
        Assert.False(plain.HealStrikesEnemies);
    }

    [Fact]
    public void HealthHealsAndHealsWithBothAmountsStrike()
    {
        Assert.True(HealStrike.StrikesWith(MagicType.DotHeal, 1, 720, 0));
        Assert.True(HealStrike.StrikesWith(MagicType.DotHeal, 1, 0, 100));
        Assert.True(HealStrike.StrikesWith(MagicType.DotHeal, 2, 50, 50));
        Assert.False(HealStrike.StrikesWith(MagicType.DotHeal, 2, 50, 0));
        Assert.False(HealStrike.StrikesWith(MagicType.Buff, 1, 720, 0));
    }
}
