using LibreKO.Domain;
using LibreKO.Network;
using Xunit;

namespace LibreKO.Tests;

public class FriendRepliesTests
{
    private const byte AddSub = 3;
    private const byte RemoveSub = 4;

    private static Packet Reply(byte sub)
    {
        var p = new Packet(GameOpcodes.GS_FRIEND_PROCESS);
        p.WriteByte(sub);
        return p;
    }

    private static Packet Ready(Packet p)
    {
        p.ResetOffset();
        p.ReadByte();
        return p;
    }

    [Fact]
    public void AnAcceptedFriendArrivesWithItsStatus()
    {
        var p = Reply(AddSub);
        p.WriteByte(FriendTexts.Succeeded);
        p.WriteString("Rikka");
        p.WriteInt(2);
        p.WriteByte(1);

        var reply = FriendReplies.ReadAdd(Ready(p));

        Assert.NotNull(reply);
        Assert.Equal(FriendTexts.Succeeded, reply.Value.Code);
        Assert.Equal("Rikka", reply.Value.Added.Name);
        Assert.Equal(2, reply.Value.Added.CharId);
        Assert.Equal(1, reply.Value.Added.Status);
    }

    [Fact]
    public void AFailedAddCarriesOnlyItsCode()
    {
        var p = Reply(AddSub);
        p.WriteByte(2);

        var reply = FriendReplies.ReadAdd(Ready(p));

        Assert.Equal(2, reply!.Value.Code);
        Assert.Equal("", reply.Value.Added.Name);
    }

    [Fact]
    public void ARemovedFriendIsNamed()
    {
        var p = Reply(RemoveSub);
        p.WriteByte(FriendTexts.Succeeded);
        p.WriteString("Rikka");

        Assert.Equal((FriendTexts.Succeeded, "Rikka"), FriendReplies.ReadRemove(Ready(p)));
    }

    [Theory]
    [InlineData(1, 44561)]
    [InlineData(6, 44566)]
    [InlineData(8, 44568)]
    public void RequestRefusalsMapToTheirTexts(byte code, int text)
    {
        Assert.Equal(text, FriendTexts.RequestResultText(code));
        Assert.Null(FriendTexts.RequestResultText(0));
    }

    [Theory]
    [InlineData(0, 10406)]
    [InlineData(1, 10400)]
    [InlineData(2, 10401)]
    public void AddResultsMapToTheirTexts(byte code, int text)
    {
        Assert.Equal(text, FriendTexts.AddResultText(code));
    }
}
