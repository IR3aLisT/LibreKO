using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class NpcPlugSlotsTests
{
    private const int DarkKnight = 6600;
    private const int LostSoul = 5508;
    private const int Werewolf = 500;

    public NpcPlugSlotsTests() => NpcPlugSlots.Seed($$"""{"{{DarkKnight}}":0,"{{LostSoul}}":1}""");

    [Fact]
    public void AManifestWithoutPlugsHoldsNoHandWeapon()
    {
        int slots = NpcPlugSlots.For(DarkKnight);

        Assert.False(NpcPlugSlots.Holds(slots, rightHand: true));
        Assert.False(NpcPlugSlots.Holds(slots, rightHand: false));
    }

    [Fact]
    public void AManifestWithOnePlugHoldsOnlyTheRightHand()
    {
        int slots = NpcPlugSlots.For(LostSoul);

        Assert.True(NpcPlugSlots.Holds(slots, rightHand: true));
        Assert.False(NpcPlugSlots.Holds(slots, rightHand: false));
    }

    [Fact]
    public void AModelBuiltFromPartsHoldsBothHands()
    {
        int slots = NpcPlugSlots.For(Werewolf);

        Assert.True(NpcPlugSlots.Holds(slots, rightHand: true));
        Assert.True(NpcPlugSlots.Holds(slots, rightHand: false));
    }

    [Fact]
    public void APlayerWearerHoldsBothHands()
    {
        var wearer = new HandWearer(Race: 1);

        Assert.True(NpcPlugSlots.Holds(wearer.PlugSlots, rightHand: true));
        Assert.True(NpcPlugSlots.Holds(wearer.PlugSlots, rightHand: false));
    }
}
