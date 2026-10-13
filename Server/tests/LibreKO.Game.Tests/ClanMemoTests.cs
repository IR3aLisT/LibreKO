using FluentAssertions;
using LibreKO.Common.Domain.Entities;
using LibreKO.Common.Domain.Services;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace LibreKO.Game.Tests;

public class ClanMemoTests : GameTestBase
{
    private const short ClanId = 7;
    private const short OtherClanId = 8;
    private const string Name = "Rikka";

    private static ServiceProvider Provider() =>
        CreateProvider(db =>
        {
            db.Characters.Add(new Character { AccountId = 1, Slot = 0, Name = Name, Level = 60, Class = 104, MapId = 21, KnightsId = ClanId });
            db.SaveChanges();
        });

    [Fact]
    public async Task AStoredMemoComesBackInTheMemberList()
    {
        using var provider = Provider();
        int characterId = await GetCharacterIdAsync(provider, Name);
        using (var scope = provider.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IKnightsRepository>().SetClanMemoAsync(characterId, "Back at 9");

        using var read = provider.CreateScope();
        var members = await read.ServiceProvider.GetRequiredService<IKnightsRepository>().GetMembersAsync(ClanId);
        members.Single().Memo.Should().Be("Back at 9");
    }

    [Fact]
    public async Task OnlyTheWriterGetsTheMemoReply()
    {
        using var provider = CreateProvider(db =>
        {
            db.Characters.Add(new Character { AccountId = 1, Slot = 0, Name = Name, Level = 60, Class = 104, MapId = 21, KnightsId = ClanId });
            db.Characters.Add(new Character { AccountId = 2, Slot = 0, Name = "Zeus", Level = 70, Class = 101, MapId = 21, KnightsId = ClanId });
            db.SaveChanges();
        });
        var sessions = provider.GetRequiredService<SessionManager>();
        var (writer, writerPackets) = await OnlineMember(provider, sessions, Name, 1);
        var (_, otherPackets) = await OnlineMember(provider, sessions, "Zeus", 2);

        var packet = new Packet(GameOpcodes.GS_KNIGHTS_PROCESS);
        packet.WriteByte(3);
        packet.WriteString("Back at 9");
        packet.ResetOffset();
        await provider.GetRequiredService<IKnightsManagementPacketService>().HandleUpdateMemoAsync(writer, packet);

        otherPackets.Should().BeEmpty();
        var reply = writerPackets.Should().ContainSingle().Subject;
        reply.ResetOffset();
        reply.ReadByte().Should().Be((byte)KnightsSubOpcode.UpdateMemo);
        reply.ReadByte().Should().Be(3);
        reply.ReadByte().Should().Be(KnightsPacketWriter.Succeeded);
        reply.ReadString().Should().Be("Back at 9");
    }

    [Fact]
    public async Task ATooLongMemoIsRefusedWithTheLengthCode()
    {
        using var provider = Provider();
        var (writer, packets) = await OnlineMember(provider, provider.GetRequiredService<SessionManager>(), Name, 1);

        var packet = new Packet(GameOpcodes.GS_KNIGHTS_PROCESS);
        packet.WriteByte(3);
        packet.WriteString(new string('x', KnightsPacketConstants.MaxMemoLength + 1));
        packet.ResetOffset();
        await provider.GetRequiredService<IKnightsManagementPacketService>().HandleUpdateMemoAsync(writer, packet);

        var reply = packets.Should().ContainSingle().Subject;
        reply.ResetOffset();
        reply.ReadByte();
        reply.ReadByte().Should().Be(3);
        reply.ReadByte().Should().Be(KnightsPacketConstants.MemoLengthRefused);
    }

    private static async Task<(UserSession Session, List<Packet> Sent)> OnlineMember(ServiceProvider provider, SessionManager sessions, string name, int account)
    {
        var client = Substitute.For<IClient>();
        client.Id.Returns(Guid.NewGuid());
        var sent = new List<Packet>();
        client.SendPacket(Arg.Do<Packet>(sent.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var session = sessions.CreateSession(client, await GetCharacterIdAsync(provider, name), account);
        session.Name = name;
        session.KnightsId = ClanId;
        return (session, sent);
    }

    [Fact]
    public async Task ChangingClansClearsTheMemo()
    {
        using var provider = Provider();
        int characterId = await GetCharacterIdAsync(provider, Name);
        using (var scope = provider.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IKnightsRepository>();
            await repo.SetClanMemoAsync(characterId, "Farming");
            await repo.SyncCharacterClanStateAsync(characterId, OtherClanId, 1);
        }

        using var read = provider.CreateScope();
        var members = await read.ServiceProvider.GetRequiredService<IKnightsRepository>().GetMembersAsync(OtherClanId);
        members.Single().Memo.Should().BeEmpty();
    }
}
