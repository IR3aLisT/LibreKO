using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class IconVeilTests
{
    private const int AnyItem = 120050000;

    private static ItemSlot Slot(ItemFlag flag, short durability = 100) =>
        new() { ItemId = AnyItem, Count = 1, Durability = durability, Flag = (byte)flag };

    [Theory]
    [InlineData(ItemFlag.Unsealed, IconWash.None)]
    [InlineData(ItemFlag.Rented, IconWash.Rented)]
    [InlineData(ItemFlag.CharacterSeal, IconWash.Rented)]
    [InlineData(ItemFlag.Duplicate, IconWash.Duplicate)]
    [InlineData(ItemFlag.Sealed, IconWash.Sealed)]
    [InlineData(ItemFlag.SealedPremium, IconWash.Sealed)]
    [InlineData(ItemFlag.NotBound, IconWash.None)]
    [InlineData(ItemFlag.Bound, IconWash.None)]
    public void EachItemStateGetsItsWash(ItemFlag flag, IconWash wash)
    {
        Assert.Equal(wash, IconVeil.Wash(Slot(flag)));
    }

    [Fact]
    public void AnExhaustedItemIsRedWhateverItsState()
    {
        Assert.Equal(IconWash.Exhausted, IconVeil.Wash(Slot(ItemFlag.Sealed, durability: 0)));
        Assert.Equal(IconWash.Exhausted, IconVeil.Wash(Slot(ItemFlag.Unsealed, durability: 0)));
    }

    [Fact]
    public void AnEmptySlotHasNoWash()
    {
        Assert.Equal(IconWash.None, IconVeil.Wash(default));
    }

    [Theory]
    [InlineData(ItemFlag.Unsealed, true, IconBadge.NotBound)]
    [InlineData(ItemFlag.Unsealed, false, IconBadge.None)]
    [InlineData(ItemFlag.NotBound, false, IconBadge.NotBound)]
    [InlineData(ItemFlag.Bound, true, IconBadge.Bound)]
    [InlineData(ItemFlag.Sealed, true, IconBadge.None)]
    public void BindOnEquipItemsShowTheBindBadges(ItemFlag flag, bool bindsOnEquip, IconBadge badge)
    {
        Assert.Equal(badge, IconVeil.Badge(IconVeil.ShownFlag(flag, bindsOnEquip)));
    }
}
