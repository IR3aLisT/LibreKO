using FluentAssertions;
using LibreKO.Common.Domain.Entities;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace LibreKO.Game.Tests;

public class PlayerInspectServiceTests : GameTestBase
{
    private const byte Moradon = 21;

    [Fact]
    public async Task APartyMemberCarriesTheirPartyIndex()
    {
        using var provider = Provider();
        var (asker, target, sent) = Pair(provider);
        var party = provider.GetRequiredService<SessionManager>().Parties.CreateParty(0);
        target.PartyIndex = party.Index;

        await provider.GetRequiredService<IPlayerInspectService>().HandleUserInformationAsync(asker, Ask(target.Name));

        PartyMarker(sent.Single()).Should().Be((short)party.Index);
    }

    [Fact]
    public async Task APlayerWithoutAPartyCarriesTheNoPartyMarker()
    {
        using var provider = Provider();
        var (asker, target, sent) = Pair(provider);

        await provider.GetRequiredService<IPlayerInspectService>().HandleUserInformationAsync(asker, Ask(target.Name));

        PartyMarker(sent.Single()).Should().Be(PlayerInspectPacketWriter.NoParty);
    }

    [Fact]
    public async Task AnUnknownNameGetsTheNoUserCode()
    {
        using var provider = Provider();
        var (asker, _, sent) = Pair(provider);

        await provider.GetRequiredService<IPlayerInspectService>().HandleUserInformationAsync(asker, Ask("Nobody"));

        var reply = sent.Single();
        reply.ResetOffset();
        reply.ReadByte();
        reply.ReadByte().Should().Be(PlayerInspectPacketWriter.DetailNoUser);
    }

    private static ServiceProvider Provider() =>
        CreateProvider(db =>
        {
            db.Characters.Add(new Character { AccountId = 70, Slot = 0, Name = "Asker", Level = 70, Class = 101, MapId = Moradon });
            db.Characters.Add(new Character { AccountId = 71, Slot = 0, Name = "Target", Level = 72, Class = 105, MapId = Moradon });
            db.SaveChanges();
        });

    private static (UserSession Asker, UserSession Target, List<Packet> Sent) Pair(ServiceProvider provider)
    {
        var sessions = provider.GetRequiredService<SessionManager>();
        var sent = new List<Packet>();
        UserSession Make(string name, int account, bool log)
        {
            var client = Substitute.For<IClient>();
            client.Id.Returns(Guid.NewGuid());
            if (log)
                client.SendPacket(Arg.Do<Packet>(sent.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            int id = GetCharacterIdAsync(provider, name).GetAwaiter().GetResult();
            var session = sessions.CreateSession(client, id, account);
            session.Name = name;
            session.Nation = AccountNation.Karus;
            session.ZoneId = Moradon;
            return session;
        }
        return (Make("Asker", 70, true), Make("Target", 71, false), sent);
    }

    private static Packet Ask(string name)
    {
        var packet = new Packet(GameOpcodes.GS_USER_INFO);
        packet.WriteSByteString(name);
        packet.ResetOffset();
        return packet;
    }

    private static short PartyMarker(Packet reply)
    {
        reply.ResetOffset();
        reply.ReadByte();
        reply.ReadByte().Should().Be(PlayerInspectPacketWriter.DetailAccepted);
        reply.ReadSByteString();
        reply.ReadByte();
        reply.ReadShort();
        reply.ReadInt();
        reply.ReadInt();
        reply.ReadByte();
        reply.ReadShort();
        reply.ReadShort();
        reply.ReadByte();
        reply.ReadByte();
        reply.ReadSByteString();
        reply.ReadSByteString();
        reply.ReadByte();
        reply.ReadByte();
        return reply.ReadShort();
    }
}
