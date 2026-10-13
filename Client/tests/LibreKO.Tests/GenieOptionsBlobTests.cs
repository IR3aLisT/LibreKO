using System;
using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class GenieOptionsBlobTests
{
    private const int OldSize = GenieOptionsBlob.SecondLayoutSize;
    private const int Size = GenieOptionsBlob.Size;
    private const int Heal = 111545;
    private const int ManaRestore = 111560;
    private const int Buff = 111654;
    private const int PartyBuff = 111657;
    private const int Strike = 111503;

    private static GenieCellKind KindOf(int id) => id switch
    {
        Heal or ManaRestore => GenieCellKind.Recovery,
        Strike => GenieCellKind.Attack,
        _ => GenieCellKind.Support,
    };

    private static byte[] SecondLayout(GenieMode modes, int[] self, int[] party)
    {
        var old = new byte[OldSize];
        old[GenieOptionsBlob.LayoutOffset] = GenieOptionsBlob.SecondLayout;
        old[GenieOptionsBlob.ModesOffset] = (byte)modes;
        old[GenieOptionsBlob.HpPercentOffset] = 55;
        old[GenieOptionsBlob.RecoveryPercentOffset] = 40;
        GenieOptionsBlob.WriteIds(old, GenieOptionsBlob.AttackOffset, new[] { Strike });
        GenieOptionsBlob.WriteIds(old, GenieOptionsBlob.SecondSelfOffset, self);
        GenieOptionsBlob.WriteIds(old, GenieOptionsBlob.SecondPartyOffset, party);
        return old;
    }

    [Fact]
    public void AFirstLayoutBlobIsUpgradedKeepingItsHpPercentForRecovery()
    {
        var old = new byte[OldSize];
        old[GenieOptionsBlob.LayoutOffset] = GenieOptionsBlob.FirstLayout;
        old[GenieOptionsBlob.HpPercentOffset] = 55;

        var upgraded = GenieOptionsBlob.Upgrade(old, Size, KindOf);

        Assert.NotNull(upgraded);
        Assert.Equal(Size, upgraded.Length);
        Assert.Equal(GenieOptionsBlob.CurrentLayout, upgraded[GenieOptionsBlob.LayoutOffset]);
        Assert.Equal(55, upgraded[GenieOptionsBlob.RecoveryPercentOffset]);
        Assert.Equal(GenieOptionsBlob.FirstLayout, old[GenieOptionsBlob.LayoutOffset]);
    }

    [Fact]
    public void SelfAndPartySkillsMoveIntoRecoveryAndSupportWithPartyBoxes()
    {
        var old = SecondLayout(GenieMode.Attack | GenieMode.Support, new[] { Heal, Buff }, new[] { Buff, PartyBuff, ManaRestore });

        var setup = GenieSetup.FromBytes(old, KindOf)!;

        Assert.Equal(Strike, setup.Attack[0]);
        Assert.Equal(new[] { Heal, ManaRestore }, setup.Recovery[..2]);
        Assert.Equal(new[] { Buff, PartyBuff }, setup.Support[..2]);
        Assert.True(setup.SupportParty[0], "a buff in both lists keeps its self cell and gets the Party box");
        Assert.True(setup.SupportParty[1]);
        Assert.True(setup.Has(GenieMode.Support));
        Assert.True(setup.Has(GenieMode.Recovery), "heals that used to fire from the self list keep firing");
        Assert.Equal(55, setup.HpPercent);
        Assert.Equal(40, setup.RecoveryPercent);
    }

    [Fact]
    public void TheUpgradeKeepsTheDarkKnightMaceRowOnSoNothingChangesForOldSetups()
    {
        var setup = GenieSetup.FromBytes(SecondLayout(GenieMode.Attack, new int[8], new int[8]), KindOf)!;

        Assert.True(setup.Has(GenieOther.DarkKnightMace));
        Assert.False(setup.Has(GenieMode.Recovery));
        Assert.Equal(GenieOptionsBlob.DefaultPetPercent, setup.PetPercent);
    }

    [Fact]
    public void MoreThanTwelveSupportSkillsAreCutAtTwelve()
    {
        var self = new int[8];
        var party = new int[8];
        for (int i = 0; i < 8; i++) { self[i] = 200000 + i; party[i] = 300000 + i; }

        var setup = GenieSetup.FromBytes(SecondLayout(GenieMode.Support, self, party), KindOf)!;

        Assert.Equal(200007, setup.Support[7]);
        Assert.Equal(300003, setup.Support[11]);
        Assert.False(setup.SupportParty[7]);
        Assert.True(setup.SupportParty[11]);
    }

    [Fact]
    public void ASetupSurvivesARoundTrip()
    {
        var setup = new GenieSetup
        {
            Modes = GenieMode.Recovery | GenieMode.Combo,
            MoreModes = GenieMoreMode.PetPotion,
            Extras = GenieExtra.HuntInPlace | GenieExtra.HideRange,
            Other = GenieOther.HammerArmour | GenieOther.FeedFamiliar | GenieOther.Arrows,
            HpPercent = 61, MpPercent = 22, RecoveryPercent = 44, PetPercent = 18, Range = 70,
            TownHpCount = 25, TownMpCount = 7, HammerPercent = 33,
        };
        setup.Attack[7] = Strike;
        setup.Recovery[3] = Heal;
        setup.Support[11] = PartyBuff;
        setup.SupportParty[11] = true;

        var back = GenieSetup.FromBytes(setup.ToBytes(), KindOf)!;

        Assert.Equal(setup.ToBytes(), back.ToBytes());
        Assert.Equal(PartyBuff, back.Support[11]);
        Assert.True(back.SupportParty[11]);
        Assert.True(back.Has(GenieOther.HammerArmour));
        Assert.Equal(33, back.HammerPercent);
    }

    [Fact]
    public void ACurrentBlobIsKeptAsItIs()
    {
        var blob = new byte[Size];
        blob[GenieOptionsBlob.LayoutOffset] = GenieOptionsBlob.CurrentLayout;

        Assert.Same(blob, GenieOptionsBlob.Upgrade(blob, Size, KindOf));
    }

    [Theory]
    [InlineData(0, Size)]
    [InlineData(9, Size)]
    [InlineData(GenieOptionsBlob.CurrentLayout, Size - 1)]
    public void AnUnknownLayoutOrSizeIsRefused(int layout, int length)
    {
        var blob = new byte[length];
        blob[GenieOptionsBlob.LayoutOffset] = (byte)layout;

        Assert.Null(GenieOptionsBlob.Upgrade(blob, Size, KindOf));
    }

    [Fact]
    public void OutOfRangeStoredValuesFallBackToTheDefaults()
    {
        var bytes = new GenieSetup().ToBytes();
        bytes[GenieOptionsBlob.HammerPercentOffset] = 0;
        bytes[GenieOptionsBlob.PetPercentOffset] = 0;

        var setup = GenieSetup.FromBytes(bytes, KindOf)!;

        Assert.Equal(GenieSetup.DefaultHammerPercent, setup.HammerPercent);
        Assert.Equal(GenieOptionsBlob.DefaultPetPercent, setup.PetPercent);
    }
}

public class GenieRecoveryTests
{
    [Theory]
    [InlineData(true, MagicType.DotHeal, GenieRecovery.HpDirect, 720, 0, true)]
    [InlineData(true, MagicType.DotHeal, GenieRecovery.HpDirect, 0, 240, true)]
    [InlineData(false, MagicType.DotHeal, GenieRecovery.HpDirect, 720, 0, false)]
    [InlineData(true, MagicType.DotHeal, GenieRecovery.MpDirect, 300, 0, false)]
    [InlineData(true, MagicType.Buff, GenieRecovery.HpDirect, 720, 0, false)]
    public void HpRecoveryIsAFriendlyOverTimeHeal(bool friendly, int type1, int direct, int first, int time, bool heals) =>
        Assert.Equal(heals, GenieRecovery.RestoresHp(friendly, type1, direct, first, time));

    [Fact]
    public void ManaRestoresAreTheMpDirectHeals()
    {
        Assert.True(GenieRecovery.RestoresMp(true, MagicType.DotHeal, GenieRecovery.MpDirect, 300));
        Assert.False(GenieRecovery.RestoresMp(true, MagicType.DotHeal, GenieRecovery.HpDirect, 300));
    }

    [Theory]
    [InlineData(300, 1000, 30, true)]
    [InlineData(301, 1000, 30, false)]
    [InlineData(0, 0, 30, false)]
    public void BelowIsAtOrUnderThePercent(int value, int max, int percent, bool below) =>
        Assert.Equal(below, GenieRecovery.Below(value, max, percent));

    [Theory]
    [InlineData(true, true, 499, true)]
    [InlineData(true, true, 500, false)]
    [InlineData(false, true, 10, false)]
    [InlineData(true, false, 10, false)]
    public void TheFamiliarIsFedUnderFivePercent(bool enabled, bool summoned, int satisfaction, bool feeds) =>
        Assert.Equal(feeds, GenieRecovery.WantsFeed(enabled, summoned, satisfaction));

    [Fact]
    public void TheOtherRowsFollowTheRetailOrder()
    {
        var order = Array.ConvertAll(GenieTownReturn.OtherRows, row => row.Option);

        Assert.Equal(new[]
        {
            GenieOther.HpPotions, GenieOther.MpPotions, GenieOther.Arrows, GenieOther.PartyBroken, GenieOther.LastSurvivor,
            GenieOther.WeaponBroken, GenieOther.HammerWeapons, GenieOther.AutoSpirit, GenieOther.FeedFamiliar,
            GenieOther.DarkKnightMace, GenieOther.HammerArmour,
        }, order);
    }
}
