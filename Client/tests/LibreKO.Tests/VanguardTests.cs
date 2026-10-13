using LibreKO.Domain;
using LibreKO.Network;
using Xunit;

namespace LibreKO.Tests;

public class VanguardTests
{
    private const string SlainTemplate =
        "- <font color=@#00bfff@>%s</font> slained Vanguard <font color=@#f08080@>%s</font>! (Coordinates :%d, %d) -";

    private static Packet Bytes(params object[] parts)
    {
        var p = new Packet(GameOpcodes.GS_VANGUARD);
        foreach (var part in parts)
        {
            switch (part)
            {
                case byte b: p.WriteByte(b); break;
                case short s: p.WriteShort(s); break;
                case string text: p.WriteString(text); break;
            }
        }
        p.ResetOffset();
        return p;
    }

    [Fact]
    public void TheInviteAndTheResultsAreRead()
    {
        Assert.Equal(VanguardReplyKind.Invite, VanguardReply.Read(Bytes((byte)1, (byte)1))!.Kind);
        Assert.Equal(VanguardReplyKind.Appointed, VanguardReply.Read(Bytes((byte)1, (byte)2, (byte)1))!.Kind);
        Assert.Equal(VanguardReplyKind.AppointFailed, VanguardReply.Read(Bytes((byte)1, (byte)2, (byte)2))!.Kind);
        Assert.Null(VanguardReply.Read(Bytes((byte)1, (byte)2, (byte)7)));
        Assert.Equal(VanguardReplyKind.Closed, VanguardReply.Read(Bytes((byte)3))!.Kind);
    }

    [Fact]
    public void TheWantedListIsACountThenU16Names()
    {
        var reply = VanguardReply.Read(Bytes((byte)2, (byte)1, (byte)2, "Velkan", "Rikka"))!;

        Assert.Equal(VanguardReplyKind.List, reply.Kind);
        Assert.Equal(new[] { "Velkan", "Rikka" }, reply.Names);
    }

    [Fact]
    public void PositionsCarryXZAndTheName()
    {
        var reply = VanguardReply.Read(Bytes((byte)2, (byte)2, (short)2, (short)1008, (short)1010, "Velkan", (short)600, (short)900, "Rikka"))!;

        Assert.Equal(VanguardReplyKind.Positions, reply.Kind);
        Assert.Equal(new VanguardMark(1008, 1010, "Velkan"), reply.Marks[0]);
        Assert.Equal(new VanguardMark(600, 900, "Rikka"), reply.Marks[1]);
        Assert.Equal(new[] { "Velkan", "Rikka" }, reply.Names);
    }

    [Fact]
    public void ARemovalNamesOnePlayer()
    {
        var reply = VanguardReply.Read(Bytes((byte)2, (byte)3, "Velkan"))!;

        Assert.Equal(VanguardReplyKind.Remove, reply.Kind);
        Assert.Equal("Velkan", Assert.Single(reply.Names));
    }

    [Fact]
    public void TheChatListLabelsTheFirstNameAAndCountsBeyondFour()
    {
        var lines = Vanguard.ListLines(new[] { "Velkan", "Rikka", "Arwen", "Zeus", "Ophelia", "Nightwalker" });

        Assert.Equal("---Wanted List---", lines[0]);
        Assert.Equal("A.Velkan", lines[1]);
        Assert.Equal("1.Rikka", lines[2]);
        Assert.Equal("5.Nightwalker", lines[6]);
        Assert.Equal("--- 2 user(s)---", lines[7]);
        Assert.Equal(0, Vanguard.OthersCount(3));
    }

    [Fact]
    public void TheSlainLineDropsTheFontTagsOrTurnsThemIntoColours()
    {
        Assert.Equal("- %s slained Vanguard %s! (Coordinates :%d, %d) -", Vanguard.Plain(SlainTemplate));
        Assert.Equal("- [color=#00bfff]%s[/color] slained Vanguard [color=#f08080]%s[/color]! (Coordinates :%d, %d) -",
            Vanguard.Markup(SlainTemplate));
    }

    [Fact]
    public void AVanguardDeathUsesTheKillerNationTextAndAListedVictimSwapsTheDefeatLine()
    {
        Assert.Equal(Vanguard.KarusSlainText, DeathNotices.TextFor(DeathNotices.VanguardKind, 71, false, Nations.Karus));
        Assert.Equal(Vanguard.ElMoradSlainText, DeathNotices.TextFor(DeathNotices.VanguardKind, 71, false, Nations.ElMorad));
        Assert.Equal(Vanguard.KarusSlainText, DeathNotices.TextFor(DeathNotices.CoordinatesKind, 71, false, Nations.ElMorad, victimListed: true));
        Assert.Equal(DeathNotices.CoordinatesText, DeathNotices.TextFor(DeathNotices.CoordinatesKind, 71, false, Nations.ElMorad));
        Assert.Equal(Vanguard.ListColour, DeathNotices.LineColourFor(71, false, DeathNotices.VanguardKind));
    }
}
