using FluentAssertions;
using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Game.Protocol;
using Xunit;

namespace LibreKO.Game.Tests;

public class UpgradeAnnouncementTests
{
    private const byte Earring = 91;
    private const byte Sword = 1;

    [Theory]
    [InlineData(Sword, 0, 7, false)]
    [InlineData(Sword, 0, 8, true)]
    [InlineData(Sword, UpgradeAnnouncements.ReverseItemClass, 10, false)]
    [InlineData(Sword, UpgradeAnnouncements.ReverseItemClass, 11, true)]
    [InlineData(Earring, 0, 1, false)]
    [InlineData(Earring, 0, 2, true)]
    public void SuccessesAreAnnouncedFromPlusEightReverseElevenAndAccessoryTwo(byte kind, short itemClass, int grade, bool announced) =>
        UpgradeAnnouncements.Announced(new ItemData { Kind = kind, ItemClass = itemClass }, grade).Should().Be(announced);
}
