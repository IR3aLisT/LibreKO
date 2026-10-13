using FluentAssertions;
using LibreKO.Common.Enums;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;

namespace LibreKO.Game.Tests;

public class SizeStateTests
{
    [Theory]
    [InlineData(BuffType.Size, SizeStates.BezoarSkill, PlayerAppearanceState.Giant)]
    [InlineData(BuffType.Size, SizeStates.MaximizeScrollSkill, PlayerAppearanceState.GiantTarget)]
    [InlineData(BuffType.Size, SizeStates.RiceCakeSkill, PlayerAppearanceState.Dwarf)]
    [InlineData(BuffType.Size, SizeStates.MinimizeScrollSkill, PlayerAppearanceState.Dwarf)]
    [InlineData(BuffType.ReduceTarget, 0, PlayerAppearanceState.GiantTarget)]
    public void SizeBuffsPickTheirLook(BuffType type, int skillId, PlayerAppearanceState expected)
    {
        SizeStates.Of(type, skillId).Should().Be(expected);
    }

    [Fact]
    public void OtherBuffsAndUnknownSizeSkillsLeaveTheSizeAlone()
    {
        SizeStates.Of(BuffType.Size, 490999).Should().BeNull();
        SizeStates.Of(BuffType.Speed, SizeStates.BezoarSkill).Should().BeNull();
    }
}
