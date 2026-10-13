using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class PartyRejoinTests
{
    private const int MaxMembers = 8;

    [Fact]
    public void ARememberedMemberIsInvitedOnceWhenTheLeaderSeesThem()
    {
        var rejoin = new PartyRejoin();
        rejoin.Remember(7, "Rikka");

        Assert.True(rejoin.Recall("rikka", amLeader: true, partyCount: 2, MaxMembers, alreadyMember: false));
        Assert.False(rejoin.Recall("Rikka", amLeader: true, partyCount: 2, MaxMembers, alreadyMember: false));
    }

    [Fact]
    public void AForgottenMemberIsNotInvited()
    {
        var rejoin = new PartyRejoin();
        rejoin.Remember(7, "Rikka");
        rejoin.Forget(7);

        Assert.False(rejoin.Recall("Rikka", amLeader: true, partyCount: 2, MaxMembers, alreadyMember: false));
    }

    [Theory]
    [InlineData(false, 2, false)]
    [InlineData(true, MaxMembers, false)]
    [InlineData(true, 2, true)]
    public void NoInviteWhenNotLeaderFullOrAlreadyIn(bool leader, int count, bool member)
    {
        var rejoin = new PartyRejoin();
        rejoin.Remember(7, "Rikka");

        Assert.False(rejoin.Recall("Rikka", leader, count, MaxMembers, member));
    }

    [Fact]
    public void ItStartsOnAndCanBeTurnedOff()
    {
        var rejoin = new PartyRejoin();
        Assert.True(rejoin.On);
        rejoin.Remember(7, "Rikka");
        rejoin.Toggle();

        Assert.False(rejoin.Recall("Rikka", amLeader: true, partyCount: 2, MaxMembers, alreadyMember: false));
    }
}
