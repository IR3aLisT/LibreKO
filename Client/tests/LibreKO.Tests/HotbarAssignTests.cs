using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class HotbarAssignTests
{
    private const int Endure = 105620;

    [Theory]
    [InlineData(MagicType.None, 0, false)]
    [InlineData(MagicType.None, 1, false)]
    [InlineData(MagicType.None, HotbarAssign.NoSecondType, true)]
    [InlineData(MagicType.Special, 0, true)]
    [InlineData(MagicType.Special, HotbarAssign.NoSecondType, false)]
    [InlineData(MagicType.Melee, 0, true)]
    [InlineData(MagicType.Buff, MagicType.Stealth, true)]
    public void DragFollowsTheSkillTypes(int type1, int type2, bool expected) =>
        Assert.Equal(expected, HotbarAssign.CanDrag(type1, type2));

    [Theory]
    [InlineData(Endure, MagicType.None, false)]
    [InlineData(HotbarAssign.Shouting, MagicType.None, true)]
    [InlineData(HotbarAssign.Assault, MagicType.None, true)]
    [InlineData(Endure, MagicType.Buff, true)]
    public void AddingRefusesPassivesExceptTheTwoShouts(int skillId, int type1, bool expected) =>
        Assert.Equal(expected, HotbarAssign.CanAdd(skillId, type1));
}
