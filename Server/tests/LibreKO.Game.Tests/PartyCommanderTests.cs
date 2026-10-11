using FluentAssertions;
using LibreKO.Game.World;
using Xunit;

namespace LibreKO.Game.Tests;

public class PartyCommanderTests
{
    private const short Leader = 101;
    private const short Member = 102;

    [Fact]
    public void TheLeaderCommandsUntilCommandIsPassedAndThenOnlyTheNewCommanderDoes()
    {
        var party = new PartyGroup(1);
        party.MemberIds[0] = Leader;
        party.MemberIds[1] = Member;

        party.CommanderId.Should().Be(Leader);
        party.IsCommandLeader(Leader).Should().BeTrue();

        party.CommandLeaderId = Member;

        party.CommanderId.Should().Be(Member);
        party.IsCommandLeader(Member).Should().BeTrue();
        party.IsCommandLeader(Leader).Should().BeFalse();
    }
}
