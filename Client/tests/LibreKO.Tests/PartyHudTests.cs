using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class PartyHudTests
{
    private const int WarriorMaster = 206;
    private const int RogueMaster = 208;
    private const int UnknownClass = 0;

    [Theory]
    [InlineData(50, 100, 0.5f)]
    [InlineData(150, 100, 1f)]
    [InlineData(10, 0, 0f)]
    public void BarsFillByTheShareLeft(int current, int max, float expected) =>
        Assert.Equal(expected, PartyHud.Fill(current, max), 3);

    [Fact]
    public void NoStatusKeepsTheRedBar() =>
        Assert.Equal(PartyHpLook.Normal, PartyHud.HpLook(System.Array.Empty<byte>(), 0));

    [Fact]
    public void PoisonTurnsTheBarPurple() =>
        Assert.Equal(PartyHpLook.Drop, PartyHud.HpLook(new[] { PartyHud.StatusPoison }, 0));

    [Fact]
    public void TwoStatusesTakeTurnsEverySecond()
    {
        var statuses = new[] { PartyHud.StatusPoison, PartyHud.StatusDamageOverTime };
        Assert.Equal(PartyHpLook.Lasting, PartyHud.HpLook(statuses, 0));
        Assert.Equal(PartyHpLook.Drop, PartyHud.HpLook(statuses, 1));
        Assert.Equal(PartyHpLook.Lasting, PartyHud.HpLook(statuses, 2));
    }

    [Theory]
    [InlineData(WarriorMaster, "system/stat-str")]
    [InlineData(RogueMaster, "system/stat-dex")]
    [InlineData(UnknownClass, "system/user")]
    public void EachClassFamilyHasItsGlyph(int classCode, string icon) =>
        Assert.Equal(icon, PartyHud.ClassIcon(classCode));
}
