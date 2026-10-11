using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class MagicFailTests
{
    private static short[] Fail(short code, short quiet = 0)
    {
        var data = new short[7];
        data[MagicFail.ResultSlot] = code;
        data[MagicFail.QuietSlot] = quiet;
        return data;
    }

    [Theory]
    [InlineData(MagicFail.Casting, false, true)]
    [InlineData(MagicFail.NoEffect, false, true)]
    [InlineData(MagicFail.NotEnoughMp, false, true)]
    [InlineData((short)0, false, true)]
    [InlineData(MagicFail.ProjectileKilled, false, false)]
    [InlineData(MagicFail.ProjectileKilledByType, false, false)]
    [InlineData(MagicFail.Casting, true, false)]
    [InlineData(MagicFail.NoEffect, true, false)]
    public void CooldownIsRefundedUnlessInFlight(short code, bool flying, bool expected) =>
        Assert.Equal(expected, MagicFail.RefundsCooldown(code, flying));

    [Theory]
    [InlineData(MagicFail.Casting, (short)0, MagicFailText.CastingFailed)]
    [InlineData(MagicFail.Casting, MagicFail.Quiet, MagicFailText.None)]
    [InlineData(MagicFail.NoEffect, (short)0, MagicFailText.SkillFailed)]
    [InlineData(MagicFail.AttackZero, (short)0, MagicFailText.Missed)]
    [InlineData(MagicFail.NotEnoughMp, (short)0, MagicFailText.NotEnoughMp)]
    [InlineData(MagicFail.ProjectileKilled, (short)0, MagicFailText.None)]
    [InlineData(MagicFail.ProjectileKilledByType, (short)0, MagicFailText.None)]
    [InlineData((short)0, (short)0, MagicFailText.None)]
    public void EachCodeHasItsText(short code, short quiet, MagicFailText expected) =>
        Assert.Equal(expected, MagicFail.TextFor(Fail(code, quiet)));

    [Fact]
    public void AShortPacketHasNoCode() =>
        Assert.Equal(0, MagicFail.Code(new short[2]));
}
