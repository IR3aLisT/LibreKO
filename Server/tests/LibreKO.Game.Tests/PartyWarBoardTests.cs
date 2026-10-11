using FluentAssertions;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace LibreKO.Game.Tests;

public class PartyWarBoardTests
{
    private const int LeaderId = 1;
    private const int MemberId = 2;

    private readonly SessionManager _sessions = new();
    private readonly Dictionary<int, List<Packet>> _sent = [];
    private readonly PartyWarBoardService _board;
    private readonly UserSession _leader;
    private readonly UserSession _member;

    public PartyWarBoardTests()
    {
        _board = new PartyWarBoardService(_sessions, Substitute.For<ILogger<PartyWarBoardService>>());
        _leader = Player(LeaderId);
        _member = Player(MemberId);
        var party = _sessions.Parties.CreateParty(LeaderId);
        party.MemberIds[1] = MemberId;
        _leader.PartyIndex = _member.PartyIndex = party.Index;
        _board.MemberJoined(party.Index, LeaderId);
        _board.MemberJoined(party.Index, MemberId);
    }

    private UserSession Player(int id)
    {
        var client = Substitute.For<IClient>();
        client.Id.Returns(Guid.NewGuid());
        var sent = new List<Packet>();
        _sent[id] = sent;
        client.SendPacket(Arg.Do<Packet>(p => sent.Add(p)), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        return _sessions.CreateSession(client, id, id);
    }

    private static (byte Target, byte Mode, List<(int Id, short Share)> Rows) Read(Packet packet)
    {
        packet.GetOpcode().Should().Be((byte)GameOpcodes.GS_PARTY);
        packet.ResetOffset();
        packet.ReadByte().Should().Be((byte)PartySubOpcode.WarBoard);
        byte target = packet.ReadByte();
        byte mode = packet.ReadByte();
        int count = packet.ReadByte();
        var rows = new List<(int, short)>();
        for (int i = 0; i < count; i++)
            rows.Add((packet.ReadInt(), packet.ReadShort()));
        return (target, mode, rows);
    }

    [Fact]
    public void SharesAreThousandthsOfThePartyTotal()
    {
        var shares = PartyWarBoardService.ShareOf([(LeaderId, 300), (MemberId, 100)]);

        shares.Should().Equal(
            new PartyPacketWriter.WarBoardShare(LeaderId, 750),
            new PartyPacketWriter.WarBoardShare(MemberId, 250));
    }

    [Fact]
    public void NoTotalGivesEveryoneZero()
    {
        PartyWarBoardService.ShareOf([(LeaderId, 0), (MemberId, 0)])
            .Select(s => s.Share).Should().Equal((ushort)0, (ushort)0);
    }

    [Fact]
    public async Task AskingForAModeAnswersWithTheCurrentTable()
    {
        _board.CountHealing(_member, 40);

        await _board.SetModeAsync(_leader, PartyWarBoardService.TargetBoard, PartyWarBoardService.ModeHealing);

        var reply = Read(_sent[LeaderId].Single());
        reply.Target.Should().Be(PartyWarBoardService.TargetBoard);
        reply.Mode.Should().Be(PartyWarBoardService.ModeHealing);
        reply.Rows.Should().Equal((LeaderId, (short)0), (MemberId, (short)PartyWarBoardService.ShareScale));
    }

    [Fact]
    public async Task TurningAMeterOffSendsNothing()
    {
        await _board.SetModeAsync(_leader, PartyWarBoardService.TargetRows, PartyWarBoardService.ModeOff);

        _sent[LeaderId].Should().BeEmpty();
    }

    [Fact]
    public async Task APushGoesOnlyToMembersWatchingThatMode()
    {
        await _board.SetModeAsync(_member, PartyWarBoardService.TargetRows, PartyWarBoardService.ModeHealing);
        _sent[MemberId].Clear();
        _board.CountDamage(_leader, 90);
        _board.CountDamage(_member, 10);

        await _board.PushAsync();

        var leaderPush = Read(_sent[LeaderId].Single());
        leaderPush.Target.Should().Be(PartyWarBoardService.TargetRows);
        leaderPush.Mode.Should().Be(PartyWarBoardService.ModeDamage);
        leaderPush.Rows.Should().Equal((LeaderId, (short)900), (MemberId, (short)100));
        var memberPush = Read(_sent[MemberId].Single());
        memberPush.Mode.Should().Be(PartyWarBoardService.ModeHealing);
    }

    [Fact]
    public async Task NothingIsPushedWhenNothingChanged()
    {
        _board.CountDamage(_leader, 5);
        await _board.PushAsync();
        _sent[LeaderId].Clear();

        await _board.PushAsync();

        _sent[LeaderId].Should().BeEmpty();
    }

    [Fact]
    public async Task AJoinerStartsFromZero()
    {
        _board.CountDamage(_member, 50);
        _board.MemberJoined(_member.PartyIndex, MemberId);
        _board.CountDamage(_leader, 50);

        await _board.SetModeAsync(_leader, PartyWarBoardService.TargetBoard, PartyWarBoardService.ModeDamage);

        Read(_sent[LeaderId].Single()).Rows.Should().Equal((LeaderId, (short)1000), (MemberId, (short)0));
    }
}
