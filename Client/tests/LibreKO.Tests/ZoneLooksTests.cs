using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class ZoneLooksTests
{
    private const int Moradon = 21;
    private const int KnightRoyale = 76;
    private const int ChaosDungeon = 85;

    [Fact]
    public void DeityZonesDressYouAsYourOwnDeityAndEveryoneElseAsTheOther()
    {
        Assert.Equal(ZoneLooks.ElMoradDeitySkill, ZoneLooks.SelfLook(ChaosDungeon, Nations.ElMorad));
        Assert.Equal(ZoneLooks.KarusDeitySkill, ZoneLooks.OtherLook(ChaosDungeon, Nations.ElMorad, Nations.ElMorad));
        Assert.Equal(ChatSender.ElMoradDeityText, ZoneLooks.SelfNameText(ChaosDungeon, Nations.ElMorad));
        Assert.Equal(ChatSender.KarusDeityText, ZoneLooks.OtherNameText(ChaosDungeon, Nations.ElMorad, false, false, false));
        Assert.Equal(ZoneLooks.KarusDeityWeapon, ZoneLooks.OtherWeapon(ChaosDungeon, Nations.ElMorad, Nations.Karus));
    }

    [Fact]
    public void UnderTheCastleScalesByYourNation()
    {
        Assert.Equal(ZoneLooks.ElMoradDeitySkill, ZoneLooks.OtherLook(ZoneLooks.DelosBasement, Nations.Karus, Nations.Karus));
        Assert.Equal(ZoneLooks.KarusDeitySkill, ZoneLooks.SelfLook(ZoneLooks.DelosBasement, Nations.Karus));
        Assert.Equal(ZoneLooks.KarusDeityScale, ZoneLooks.LookScale(ZoneLooks.DelosBasement, Nations.Karus, ZoneLooks.ElMoradDeitySkill));
        Assert.Equal(ZoneLooks.ElMoradDeityScale, ZoneLooks.LookScale(ZoneLooks.DelosBasement, Nations.ElMorad, ZoneLooks.KarusDeitySkill));
        Assert.Equal(0, ZoneLooks.SelfWeapon(ZoneLooks.DelosBasement, Nations.Karus));
    }

    [Fact]
    public void DungeonDefenceDressesEachPlayerAsTheirOwnNation()
    {
        Assert.Equal(ZoneLooks.ElMoradDeitySkill, ZoneLooks.OtherLook(ZoneLooks.DungeonDefence, Nations.Karus, Nations.ElMorad));
        Assert.Equal(ZoneLooks.ElMoradDeityWeapon, ZoneLooks.OtherWeapon(ZoneLooks.DungeonDefence, Nations.Karus, Nations.ElMorad));
    }

    [Fact]
    public void KnightRoyaleDressesEveryoneAsPathosAndHidesStrangersWhileYouHaveAParty()
    {
        Assert.Equal(ZoneLooks.PathosSkill, ZoneLooks.SelfLook(KnightRoyale, Nations.Karus));
        Assert.Equal(ZoneLooks.PathosSkill, ZoneLooks.OtherLook(KnightRoyale, Nations.Karus, Nations.ElMorad));
        Assert.Equal(ChatSender.PathoistText, ZoneLooks.OtherNameText(KnightRoyale, Nations.Karus, false, true, false));
        Assert.Equal(0, ZoneLooks.OtherNameText(KnightRoyale, Nations.Karus, false, true, true));
        Assert.Equal(0, ZoneLooks.OtherNameText(KnightRoyale, Nations.Karus, true, true, false));
    }

    [Fact]
    public void OrdinaryZonesKeepLooksAndPrisonHidesPlayers()
    {
        Assert.Equal(0, ZoneLooks.SelfLook(Moradon, Nations.Karus));
        Assert.Equal(0, ZoneLooks.OtherLook(Moradon, Nations.Karus, Nations.ElMorad));
        Assert.True(ZoneLooks.HidesOtherPlayers(ZoneLooks.Prison));
        Assert.False(ZoneLooks.HidesOtherPlayers(Moradon));
    }

    [Fact]
    public void ChatGesturesAreMutedInDeityZonesAndDungeonDefence()
    {
        Assert.True(ZoneLooks.MutesChatGestures(ChaosDungeon));
        Assert.True(ZoneLooks.MutesChatGestures(ZoneLooks.DungeonDefence));
        Assert.False(ZoneLooks.MutesChatGestures(Moradon));
        Assert.True(ZoneLooks.KeepsNamesOutOfChat(ChaosDungeon));
        Assert.False(ZoneLooks.KeepsNamesOutOfChat(ZoneLooks.DungeonDefence));
    }

    [Theory]
    [InlineData(ZoneLooks.KarusKurian, ZoneLooks.KarusDevilModel)]
    [InlineData(ZoneLooks.ElMoradKurian, ZoneLooks.ElMoradDevilModel)]
    [InlineData(1, 0)]
    public void OnlyKuriansHaveADevilModel(int race, int model) =>
        Assert.Equal(model, ZoneLooks.DevilModel(race));
}
