using LibreKO.Domain;
using LibreKO.Network;
using Xunit;

namespace LibreKO.Tests;

public class ExpSealTests
{
    private static Packet Bytes(params object[] parts)
    {
        var p = new Packet(GameOpcodes.GS_EXP_SEAL);
        foreach (var part in parts)
        {
            switch (part)
            {
                case byte b: p.WriteByte(b); break;
                case short s: p.WriteShort(s); break;
                case long l: p.WriteLong(l); break;
            }
        }
        p.ResetOffset();
        return p;
    }

    [Fact]
    public void TheRepliesAreReadInThe2619Shapes()
    {
        Assert.Equal(new ExpSealReply(ExpSeal.SubOn, 1, 0), ExpSealReply.Read(Bytes(ExpSeal.SubOn, (byte)1)));
        Assert.Equal(new ExpSealReply(ExpSeal.SubStore, 1, 100_000_000), ExpSealReply.Read(Bytes(ExpSeal.SubStore, (short)1, 100_000_000L)));
        Assert.Equal(new ExpSealReply(ExpSeal.SubStore, 2, 0), ExpSealReply.Read(Bytes(ExpSeal.SubStore, (short)2)));
        Assert.Equal(new ExpSealReply(ExpSeal.SubTotal, 1, 1_500), ExpSealReply.Read(Bytes(ExpSeal.SubTotal, 1_500L)));
        Assert.Equal(new ExpSealReply(ExpSeal.SubUseJar, 9, 0), ExpSealReply.Read(Bytes(ExpSeal.SubUseJar, (short)9)));
    }

    [Fact]
    public void RefusalsUseThe2619Texts()
    {
        Assert.Equal(ExpSeal.TooLittleText, ExpSeal.StoreRefusal(2));
        Assert.Equal(ExpSeal.WhileOnText, ExpSeal.StoreRefusal(3));
        Assert.Null(ExpSeal.StoreRefusal(1));
        Assert.Equal((ExpSeal.RebirthLimitText, false), ExpSeal.UseRefusal(9));
        Assert.Equal((ExpSeal.JarFailedText, true), ExpSeal.UseRefusal(4));
        Assert.Null(ExpSeal.UseRefusal(1));
    }

    [Fact]
    public void TheLocalChecksComeInThe2619Order()
    {
        Assert.Equal(ExpSeal.NoJarText, ExpSeal.LocalRefusal(false, 0, 0));
        Assert.Equal(ExpSeal.TooLittleText, ExpSeal.LocalRefusal(true, 99_999_999, 100_000_000));
        Assert.Equal(ExpSeal.NoAmountText, ExpSeal.LocalRefusal(true, 200_000_000, 0));
        Assert.Null(ExpSeal.LocalRefusal(true, 200_000_000, 100_000_000));
    }

    [Fact]
    public void TheSealedBarIsTheTotalOverOneBillion() => Assert.Equal(25.0, ExpSeal.Percent(250_000_000));
}
