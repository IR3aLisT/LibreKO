using LibreKO.Domain;
using LibreKO.Network;
using Xunit;

namespace LibreKO.Tests;

public class FlashBonusTests
{
    [Fact]
    public void AStartedFlashCarriesItsKindAndValue()
    {
        var p = new Packet(GameOpcodes.GS_STORY);
        p.WriteByte(1);
        p.WriteByte(FlashTitle.Exp);
        p.WriteByte(0);
        p.WriteInt(30);
        p.ResetOffset();

        Assert.Equal(new FlashBonus(false, FlashTitle.Exp, 30), FlashBonus.Read(p));
        Assert.Equal(0, p.RemainingBytes);
    }

    [Fact]
    public void AnEndedFlashHasNoValue()
    {
        var p = new Packet(GameOpcodes.GS_STORY);
        p.WriteByte(2);
        p.WriteByte(FlashTitle.Drop);
        p.WriteByte(0);
        p.ResetOffset();

        Assert.Equal(new FlashBonus(true, FlashTitle.Drop, 0), FlashBonus.Read(p));
    }

    [Fact]
    public void TheTitleIsTheRetailTextWithItsPercent()
    {
        var text = FlashTitle.TextFor(FlashTitle.Exp)!.Value;
        Assert.Equal(1494, text.Id);
        Assert.Equal("Base Experience for Monster Kills +30%", FlashTitle.Format(text.Fallback, 30));
        Assert.Null(FlashTitle.TextFor(9));
    }
}
