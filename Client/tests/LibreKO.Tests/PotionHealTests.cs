using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class PotionHealTests
{
    private const int HealthRestored = 720, ManaRestored = 1920;
    private const int ManaShell = 3, HealthPercent = 5;
    private const int InvalidTarget = 99;

    [Theory]
    [InlineData(HealTarget.HpPurchase, HealthRestored, HealTarget.Hp, HealthRestored)]
    [InlineData(HealTarget.HpPurchase, HealthRestored, HealTarget.Mp, 0)]
    [InlineData(HealTarget.MpPurchase, ManaRestored, HealTarget.Mp, ManaRestored)]
    [InlineData(HealTarget.MpPurchase, ManaRestored, HealTarget.Hp, 0)]
    public void MaestroPotionsRestoreTheirOwnVital(int directType, int amount, int target, int expected)
    {
        Assert.Equal(expected, ItemData.PotionHealFor(MagicType.DotHeal, directType, amount, target));
    }

    [Theory]
    [InlineData(HealTarget.Hp, HealthRestored, HealTarget.Hp, HealthRestored)]
    [InlineData(HealTarget.Hp, HealthRestored, HealTarget.Mp, 0)]
    [InlineData(HealTarget.Mp, ManaRestored, HealTarget.Mp, ManaRestored)]
    [InlineData(HealTarget.Mp, ManaRestored, HealTarget.Hp, 0)]
    public void OrdinaryPotionsKeepTheirRestoration(int directType, int amount, int target, int expected)
    {
        Assert.Equal(expected, ItemData.PotionHealFor(MagicType.DotHeal, directType, amount, target));
    }

    [Theory]
    [InlineData(MagicType.Buff, HealTarget.Hp, HealthRestored, HealTarget.Hp)]
    [InlineData(MagicType.Melee, HealTarget.HpPurchase, HealthRestored, HealTarget.Hp)]
    [InlineData(MagicType.DotHeal, HealTarget.HpPurchase, 0, HealTarget.Hp)]
    [InlineData(MagicType.DotHeal, HealTarget.MpPurchase, -ManaRestored, HealTarget.Mp)]
    [InlineData(MagicType.DotHeal, ManaShell, ManaRestored, HealTarget.Mp)]
    [InlineData(MagicType.DotHeal, HealthPercent, HealthRestored, HealTarget.Hp)]
    [InlineData(MagicType.DotHeal, 0, HealthRestored, 0)]
    [InlineData(MagicType.DotHeal, HealTarget.HpPurchase, HealthRestored, HealTarget.HpPurchase)]
    [InlineData(MagicType.DotHeal, HealTarget.MpPurchase, ManaRestored, InvalidTarget)]
    public void OtherEffectsAndInvalidTargetsAreNotRestorationPotions(int type, int directType, int amount, int target)
    {
        Assert.Equal(0, ItemData.PotionHealFor(type, directType, amount, target));
    }
}
