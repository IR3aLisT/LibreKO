using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class FriendlyTargetTests
{
    [Theory]
    [InlineData(false, false, false, false, FriendlyPick.Self)]
    [InlineData(false, false, false, true, FriendlyPick.PartyPick)]
    [InlineData(true, true, true, true, FriendlyPick.Selected)]
    [InlineData(true, true, false, true, FriendlyPick.PartyPick)]
    [InlineData(true, true, false, false, FriendlyPick.Nothing)]
    [InlineData(true, false, false, false, FriendlyPick.Nothing)]
    public void PartySkillsPreferTheMemberInViewThenThePartyPick(
        bool hasSelection, bool valid, bool inParty, bool hasPick, FriendlyPick expected) =>
        Assert.Equal(expected, FriendlyTarget.Choose(SkillTarget.Party, hasSelection, valid, inParty, hasPick));

    [Theory]
    [InlineData(false, false, FriendlyPick.Nothing)]
    [InlineData(true, false, FriendlyPick.Nothing)]
    [InlineData(true, true, FriendlyPick.Selected)]
    public void FriendOnlySkillsNeedAFriendlySelection(bool hasSelection, bool valid, FriendlyPick expected) =>
        Assert.Equal(expected, FriendlyTarget.Choose(SkillTarget.FriendOnly, hasSelection, valid, false, false));

    [Theory]
    [InlineData(false, false, FriendlyPick.Self)]
    [InlineData(true, false, FriendlyPick.Self)]
    [InlineData(true, true, FriendlyPick.Selected)]
    public void FriendWithMeSkillsFallBackToSelf(bool hasSelection, bool valid, FriendlyPick expected) =>
        Assert.Equal(expected, FriendlyTarget.Choose(SkillTarget.FriendWithMe, hasSelection, valid, false, false));
}
