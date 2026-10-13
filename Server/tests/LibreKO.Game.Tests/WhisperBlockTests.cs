using FluentAssertions;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace LibreKO.Game.Tests;

public class WhisperBlockTests : GameTestBase
{
    private const int NoWhisperPartner = -1;

    [Theory]
    [InlineData(false, false, ChatTargetResult.Connected)]
    [InlineData(true, false, ChatTargetResult.SenderBlocked)]
    [InlineData(false, true, ChatTargetResult.TargetBlocked)]
    [InlineData(true, true, ChatTargetResult.SenderBlocked)]
    public async Task AWhisperTargetIsRefusedWhileEitherSideBlocksPrivateMessages(
        bool senderBlocks, bool targetBlocks, ChatTargetResult expected)
    {
        using var provider = CreateProvider(_ => { });
        var sessions = provider.GetRequiredService<SessionManager>();
        var (sender, client, sent) = Player(sessions, 600, "Zeus");
        var (target, _, _) = Player(sessions, 601, "Rikka");
        var social = provider.GetRequiredService<ISocialPacketCoordinator>();

        await social.HandleChatTargetAsync(client, BlockToggle(senderBlocks));
        target.BlockPrivateChat = targetBlocks;
        await social.HandleChatTargetAsync(client, WhisperTo(target.Name));

        var reply = sent.Should().ContainSingle().Subject;
        reply.ResetOffset();
        reply.ReadByte().Should().Be(ChatTargetPacketWriter.TypeWhisper);
        reply.ReadShort().Should().Be((short)expected);
        sender.PrivateChatUser.Should().Be(expected == ChatTargetResult.Connected ? target.CharacterId : NoWhisperPartner);
    }

    private static Packet BlockToggle(bool block)
    {
        var packet = new Packet(GameOpcodes.GS_CHAT_TARGET);
        packet.WriteByte(ChatTargetPacketWriter.TypeBlockToggle);
        packet.WriteByte(block ? (byte)1 : (byte)0);
        packet.ResetOffset();
        return packet;
    }

    private static Packet WhisperTo(string name)
    {
        var packet = new Packet(GameOpcodes.GS_CHAT_TARGET);
        packet.WriteByte(ChatTargetPacketWriter.TypeWhisper);
        packet.WriteString(name);
        packet.ResetOffset();
        return packet;
    }

    private static (UserSession Session, IClient Client, List<Packet> Sent) Player(
        SessionManager sessions, int characterId, string name)
    {
        var client = Substitute.For<IClient>();
        client.Id.Returns(Guid.NewGuid());
        var sent = new List<Packet>();
        client.SendPacket(Arg.Do<Packet>(sent.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var session = sessions.CreateSession(client, characterId, characterId + 100);
        session.Name = name;
        return (session, client, sent);
    }
}
