using LibreKO.Domain;
using LibreKO.Network;
using Xunit;

namespace LibreKO.Tests;

public class ClanBoardsTests
{
    private static Packet Reply(System.Action<Packet> body)
    {
        var p = new Packet(GameOpcodes.GS_KNIGHTS_PROCESS);
        body(p);
        p.ResetOffset();
        return p;
    }

    [Fact]
    public void TheStaffStatusListsTheApplicants()
    {
        var p = Reply(w =>
        {
            w.WriteByte(ClanBoards.StateStaff);
            w.WriteShort(1);
            w.WriteByte(50);
            w.WriteByte(112);
            w.WriteString("Rin");
            w.WriteString("Hello");
        });

        var status = Net.ReadBoardStatus(p)!;

        Assert.True(status.Staff);
        Assert.Equal(new[] { new ClanApplicant(50, 112, "Rin", "Hello") }, status.Applicants);
    }

    [Fact]
    public void AnApplicantsStatusNamesTheClanTheyAppliedTo()
    {
        var p = Reply(w =>
        {
            w.WriteByte(ClanBoards.StateClanless);
            w.WriteShort(1);
            w.WriteShort(10);
            w.WriteShort(4);
            w.WriteString("Wolves");
        });

        var status = Net.ReadBoardStatus(p)!;

        Assert.True(status.Applied);
        Assert.Equal((10, 4, "Wolves"), (status.AppliedClanId, status.AppliedMarkVersion, status.AppliedClanName));
    }

    [Fact]
    public void AdvertRowsReadEveryField()
    {
        var p = Reply(w =>
        {
            w.WriteShort(1);
            w.WriteShort(10);
            w.WriteString("Wolves");
            w.WriteByte(3);
            w.WriteByte(2);
            w.WriteString("Alpha");
            w.WriteShort(4);
            w.WriteString("Join us");
        });

        Assert.Equal(new[] { new ClanAdvert(10, "Wolves", 3, 2, "Alpha", 4, "Join us") }, Net.ReadAdverts(p));
    }

    [Fact]
    public void WishRowsReadEveryFieldAndDropAnOversizedPage()
    {
        var p = Reply(w =>
        {
            w.WriteShort(1);
            w.WriteString("Rin");
            w.WriteString("Priest");
            w.WriteByte(72);
            w.WriteByte(112);
            w.WriteByte(25);
            w.WriteByte(1);
            w.WriteInt(101);
            w.WriteString("2026-10-09");
        });
        var oversized = Reply(w => w.WriteShort(ClanBoards.RowsPerPage + 1));

        Assert.Equal(new[] { new WishPost("Rin", "Priest", 72, 112, 25, true, 101, "2026-10-09") }, Net.ReadWishPosts(p));
        Assert.Empty(Net.ReadWishPosts(oversized));
    }

    [Fact]
    public void TheWishStatusDependsOnTheRole()
    {
        var recruiter = Net.ReadWishStatus(Reply(w => { w.WriteByte(ClanBoards.RoleRecruiter); w.WriteString("Kaito"); }));
        var member = Net.ReadWishStatus(Reply(w => w.WriteByte(ClanBoards.RoleMember)));
        var seeker = Net.ReadWishStatus(Reply(w => { w.WriteByte(ClanBoards.RoleSeeker); w.WriteByte(3); }));

        Assert.Equal("Kaito", recruiter.RequestedName);
        Assert.True(member.Member);
        Assert.Equal(3, seeker.Received);
    }

    [Fact]
    public void ReceivedRequestsReadLeaderBeforeClan()
    {
        var p = Reply(w =>
        {
            w.WriteByte(1);
            w.WriteShort(10);
            w.WriteString("Alpha");
            w.WriteString("Wolves");
            w.WriteByte(3);
            w.WriteByte(2);
            w.WriteShort(4);
            w.WriteString("Come with us");
        });

        Assert.Equal(new[] { new WishRequest(10, "Alpha", "Wolves", 3, 2, 4, "Come with us") }, Net.ReadWishRequests(p));
    }

    [Theory]
    [InlineData("25", true)]
    [InlineData("1", true)]
    [InlineData("99", true)]
    [InlineData("0", false)]
    [InlineData("100", false)]
    [InlineData("-5", false)]
    [InlineData("2a", false)]
    [InlineData("", false)]
    public void AgeMustBeOneToNinetyNineDigits(string text, bool valid)
    {
        Assert.Equal(valid, ClanBoards.AgeValid(text));
    }

    [Fact]
    public void ResultCodesMapToThe2619Texts()
    {
        Assert.Equal(16510, ClanBoards.ApplyResultText(ClanBoards.KindRegister, 0));
        Assert.Equal(16507, ClanBoards.ApplyResultText(ClanBoards.KindDelete, 2));
        Assert.Equal(16504, ClanBoards.AdvertResultText(ClanBoards.KindRegister, 3));
        Assert.Equal(18633, ClanBoards.PromoteText(4));
        Assert.Equal(40056, ClanBoards.IntroResultText(ClanBoards.KindRegister, 3));
        Assert.Equal(40061, ClanBoards.RequestResultText(ClanBoards.KindRegister, 4));
        Assert.Equal(40064, ClanBoards.WishPromoteText(4));
        Assert.Equal(15701, ClanBoards.ClassLineText(5));
    }
}
