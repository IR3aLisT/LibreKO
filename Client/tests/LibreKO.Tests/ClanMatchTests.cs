using System.Collections.Generic;
using LibreKO.Domain;
using LibreKO.Network;
using Xunit;

namespace LibreKO.Tests;

public class ClanMatchTests
{
    private const int Wolves = 40;
    private const int Lions = 50;
    private static readonly ClanMatchPair Pair = new(96, Wolves, Lions);

    [Fact]
    public void ThePairIsReadInTheServerOrder()
    {
        var p = new Packet(GameOpcodes.GS_KNIGHTS_PROCESS);
        p.WriteShort(96);
        foreach (var (clan, name) in new[] { (Wolves, "Wolves"), (Lions, "Lions") })
        {
            p.WriteShort((short)clan);
            p.WriteShort(3);
            p.WriteByte(0);
            p.WriteByte(0);
            p.WriteSByteString(name);
            p.WriteSByteString("");
        }
        p.ResetOffset();

        Assert.Equal(Pair, ClanMatchReply.ReadPair(p));
    }

    [Fact]
    public void TheRosterAndVitalsAreRead()
    {
        var p = new Packet(GameOpcodes.GS_BATTLE_EVENT);
        p.WriteUShort(1);
        p.WriteShort(Wolves);
        p.WriteInt(7);
        p.WriteString("Velkan");
        p.WriteShort(900);
        p.WriteShort(800);
        p.WriteByte(0);
        p.WriteShort(105);
        p.WriteShort(400);
        p.WriteShort(300);
        p.WriteByte(1);
        p.ResetOffset();

        Assert.Equal(new ClanMatchFighter(Wolves, 7, "Velkan", 900, 800, 105, 400, 300, 1), Assert.Single(ClanMatchReply.ReadRoster(p)));

        var v = new Packet(GameOpcodes.GS_BATTLE_EVENT);
        v.WriteInt(7);
        v.WriteShort(900);
        v.WriteShort(450);
        v.WriteShort(400);
        v.WriteShort(100);
        v.ResetOffset();
        Assert.Equal(new ClanMatchVitals(7, 900, 450, 400, 100), ClanMatchReply.ReadVitals(v));
    }

    [Fact]
    public void ClanAIsRedClanBIsBlueAnythingElseIsADraw()
    {
        Assert.Equal(MatchBanner.RedWins, ClanMatch.BannerFor(Wolves, Pair));
        Assert.Equal(MatchBanner.BlueWins, ClanMatch.BannerFor(Lions, Pair));
        Assert.Equal(MatchBanner.Draw, ClanMatch.BannerFor(0, Pair));
    }

    [Fact]
    public void TheRosterSplitsIntoTwoTeamsOfEightAndDropsAThirdClan()
    {
        var roster = new List<ClanMatchFighter>();
        for (int i = 0; i < 10; i++) roster.Add(new ClanMatchFighter(Wolves, i, $"W{i}", 1, 1, 101, 1, 1, 1));
        roster.Add(new ClanMatchFighter(Lions, 20, "L", 1, 1, 101, 1, 1, 2));
        roster.Add(new ClanMatchFighter(99, 30, "X", 1, 1, 101, 1, 1, 2));

        var teams = ClanMatch.Split(roster);

        Assert.Equal(ClanMatch.MembersPerTeam, teams[0].Count);
        Assert.Equal(Lions, Assert.Single(teams[1]).Clan);
    }

    [Fact]
    public void ThePlateOnlyShowsInTheMatchZones()
    {
        Assert.True(ClanMatch.ShowsPlate(96));
        Assert.False(ClanMatch.ShowsPlate(77));
        Assert.True(ClanMatch.ColoursNames(77, 96));
        Assert.False(ClanMatch.ColoursNames(21, 96));
    }
}
