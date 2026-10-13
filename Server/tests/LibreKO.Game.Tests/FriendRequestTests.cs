using FluentAssertions;
using LibreKO.Common.Domain.Entities;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Common.Infrastructure.Persistence;
using LibreKO.Game.Protocol;
using LibreKO.Game.World;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace LibreKO.Game.Tests;

public class FriendRequestTests : GameTestBase
{
    private const byte Accept = 1;
    private const byte Decline = 0;

    private sealed record Player(UserSession Session, IClient Client, List<Packet> Sent);

    private static Player Join(ServiceProvider provider, int characterId, string name, AccountNation nation = AccountNation.Karus)
    {
        var client = Substitute.For<IClient>();
        client.Id.Returns(Guid.NewGuid());
        var sent = new List<Packet>();
        client.SendPacket(Arg.Do<Packet>(sent.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var session = provider.GetRequiredService<SessionManager>().CreateSession(client, characterId, accountId: characterId + 1000);
        session.Name = name;
        session.Nation = nation;
        return new Player(session, client, sent);
    }

    private static Task Send(ServiceProvider provider, Player player, FriendSubOpcode sub, string name, byte? answer = null)
    {
        var packet = new Packet(GameOpcodes.GS_FRIEND_PROCESS);
        packet.WriteByte((byte)sub);
        packet.WriteString(name);
        if (answer is { } value)
            packet.WriteByte(value);
        packet.ResetOffset();
        return provider.GetRequiredService<ISocialPacketCoordinator>().HandleFriendProcessAsync(player.Client, packet);
    }

    private static Packet Last(Player player, FriendSubOpcode sub)
    {
        var packet = player.Sent.Last(p => p.GetOpcode() == (byte)GameOpcodes.GS_FRIEND_PROCESS && SubOf(p) == (byte)sub);
        packet.ResetOffset();
        packet.ReadByte();
        return packet;
    }

    private static byte SubOf(Packet packet)
    {
        packet.ResetOffset();
        return packet.ReadByte();
    }

    private static bool Friends(ServiceProvider provider, int characterId, int friendId)
    {
        using var scope = provider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>().Friendships
            .Any(f => f.CharacterId == characterId && f.FriendCharacterId == friendId);
    }

    [Fact]
    public async Task AskingAPlayerSendsThemTheRequestAndAddsNothingYet()
    {
        using var provider = CreateProvider(_ => { });
        var zeus = Join(provider, 1, "Zeus");
        var rikka = Join(provider, 2, "Rikka");

        await Send(provider, zeus, FriendSubOpcode.Add, "Rikka");

        Last(rikka, FriendSubOpcode.Request).ReadString().Should().Be("Zeus");
        Friends(provider, 1, 2).Should().BeFalse();
    }

    [Fact]
    public async Task AnAcceptedRequestAddsTheFriendAndTellsTheAsker()
    {
        using var provider = CreateProvider(_ => { });
        var zeus = Join(provider, 1, "Zeus");
        var rikka = Join(provider, 2, "Rikka");

        await Send(provider, zeus, FriendSubOpcode.Add, "Rikka");
        await Send(provider, rikka, FriendSubOpcode.Request, "Zeus", Accept);

        Friends(provider, 1, 2).Should().BeTrue();
        var reply = Last(zeus, FriendSubOpcode.Add);
        reply.ReadByte().Should().Be((byte)FriendAddResult.Succeeded);
        reply.ReadString().Should().Be("Rikka");
        reply.ReadInt().Should().Be(2);
    }

    [Fact]
    public async Task ADeclinedRequestTellsTheAsker()
    {
        using var provider = CreateProvider(_ => { });
        var zeus = Join(provider, 1, "Zeus");
        var rikka = Join(provider, 2, "Rikka");

        await Send(provider, zeus, FriendSubOpcode.Add, "Rikka");
        await Send(provider, rikka, FriendSubOpcode.Request, "Zeus", Decline);

        Last(zeus, FriendSubOpcode.RequestResult).ReadByte().Should().Be((byte)FriendRequestResult.Denied);
        Friends(provider, 1, 2).Should().BeFalse();
    }

    [Fact]
    public async Task TheOtherNationCannotBeAsked()
    {
        using var provider = CreateProvider(_ => { });
        var zeus = Join(provider, 1, "Zeus");
        var hera = Join(provider, 3, "Hera", AccountNation.ElMorad);

        await Send(provider, zeus, FriendSubOpcode.Add, "Hera");

        Last(zeus, FriendSubOpcode.RequestResult).ReadByte().Should().Be((byte)FriendRequestResult.NationMismatch);
        hera.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task AnOfflineOrUnknownPlayerCannotBeAdded()
    {
        using var provider = CreateProvider(_ => { });
        var zeus = Join(provider, 1, "Zeus");

        await Send(provider, zeus, FriendSubOpcode.Add, "Nobody");

        Last(zeus, FriendSubOpcode.RequestResult).ReadByte().Should().Be((byte)FriendRequestResult.CannotAdd);
    }

    [Fact]
    public async Task AskingTwiceIsInProgressAndAFriendIsAlreadyRegistered()
    {
        using var provider = CreateProvider(_ => { });
        var zeus = Join(provider, 1, "Zeus");
        var rikka = Join(provider, 2, "Rikka");

        await Send(provider, zeus, FriendSubOpcode.Add, "Rikka");
        await Send(provider, zeus, FriendSubOpcode.Add, "Rikka");
        Last(zeus, FriendSubOpcode.RequestResult).ReadByte().Should().Be((byte)FriendRequestResult.InProgress);

        await Send(provider, rikka, FriendSubOpcode.Request, "Zeus", Accept);
        await Send(provider, zeus, FriendSubOpcode.Add, "Rikka");
        Last(zeus, FriendSubOpcode.RequestResult).ReadByte().Should().Be((byte)FriendRequestResult.AlreadyRegistered);
    }

    [Fact]
    public async Task LoggingInTellsThoseWhoListYou()
    {
        using var provider = CreateProvider(_ => { });
        var zeus = Join(provider, 1, "Zeus");
        var rikka = Join(provider, 2, "Rikka");
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Friendships.Add(new Friendship { CharacterId = 1, FriendCharacterId = 2, AddedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        await provider.GetRequiredService<ISocialPacketCoordinator>().NotifyFriendLoggedInAsync(rikka.Session);

        Last(zeus, FriendSubOpcode.LoggedIn).ReadString().Should().Be("Rikka");
    }

    [Fact]
    public async Task RemovingAFriendRepliesWithTheName()
    {
        using var provider = CreateProvider(db => db.Characters.AddRange(
            new Character { Id = 1, AccountId = 1001, Name = "Zeus", Race = 1, Class = 101, Face = 1, Hair = 1, Level = 10, Hp = 100, Mp = 100, MapId = 1 },
            new Character { Id = 2, AccountId = 1002, Name = "Rikka", Race = 1, Class = 101, Face = 1, Hair = 1, Level = 10, Hp = 100, Mp = 100, MapId = 1 }));
        var zeus = Join(provider, 1, "Zeus");
        var rikka = Join(provider, 2, "Rikka");
        await Send(provider, zeus, FriendSubOpcode.Add, "Rikka");
        await Send(provider, rikka, FriendSubOpcode.Request, "Zeus", Accept);

        await Send(provider, zeus, FriendSubOpcode.Remove, "Rikka");

        var reply = Last(zeus, FriendSubOpcode.Remove);
        reply.ReadByte().Should().Be((byte)FriendRemoveResult.Succeeded);
        reply.ReadString().Should().Be("Rikka");
        Friends(provider, 1, 2).Should().BeFalse();
    }
}
