using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using LibreKO.Game.Configuration;
using LibreKO.Game.World;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace LibreKO.Game.Tests;

public class VoiceRelayTests
{
    private static readonly byte[] VectorKey = Enumerable.Range(1, 16).Select(i => (byte)i).ToArray();
    private const uint VectorSession = 0x01020304;

    [Fact]
    public void SealingMatchesTheSharedVectors()
    {
        using var client = new VoiceSeal(VectorSession, VectorKey);
        var datagram = new byte[64];
        int length = client.Seal(VoiceSeal.ClientToServer, [VoiceDatagram.Hello], datagram);
        Convert.ToHexString(datagram, 0, length).ToLowerInvariant()
            .Should().Be("040302010100000038a12a628534651a41e7e527b35b823c93");

        using var server = new VoiceSeal(VectorSession, VectorKey);
        var frame = new byte[] { VoiceDatagram.Frame, 7, 0, 0, 0, 9, 0, (byte)'a', (byte)'b', (byte)'c' };
        length = server.Seal(VoiceSeal.ServerToClient, frame, datagram);
        Convert.ToHexString(datagram, 0, length).ToLowerInvariant()
            .Should().Be("0403020101000000cafd4e7530bc019e1661f10d3da937b78fd7f7c87374abaa5d37");
    }

    [Fact]
    public void OpeningRejectsTamperingReplaysAndTheWrongDirection()
    {
        using var sender = new VoiceSeal(VectorSession, VectorKey);
        using var receiver = new VoiceSeal(VectorSession, VectorKey);
        var datagram = new byte[64];
        var plain = new byte[64];
        int length = sender.Seal(VoiceSeal.ClientToServer, [VoiceDatagram.Frame, 1, 0, 42], datagram);
        var sealedBytes = datagram[..length];

        receiver.TryOpen(VoiceSeal.ServerToClient, sealedBytes, plain, out _).Should().BeFalse();
        var tampered = (byte[])sealedBytes.Clone();
        tampered[VoiceSeal.HeaderLength] ^= 1;
        receiver.TryOpen(VoiceSeal.ClientToServer, tampered, plain, out _).Should().BeFalse();

        receiver.TryOpen(VoiceSeal.ClientToServer, sealedBytes, plain, out int opened).Should().BeTrue();
        plain[..opened].Should().Equal(VoiceDatagram.Frame, 1, 0, 42);
        receiver.TryOpen(VoiceSeal.ClientToServer, sealedBytes, plain, out _).Should().BeFalse();
    }

    [Fact]
    public void TheReplayWindowTakesLateFramesOnceAndDropsOnesTooOld()
    {
        var window = new ReplayWindow();
        foreach (uint counter in new uint[] { 1, 2, 5, 100 })
        {
            window.Allows(counter).Should().BeTrue();
            window.Mark(counter);
        }
        window.Allows(0).Should().BeFalse();
        window.Allows(100).Should().BeFalse();
        window.Allows(99).Should().BeTrue();
        window.Mark(99);
        window.Allows(99).Should().BeFalse();
        window.Allows(37).Should().BeTrue();
        window.Allows(36).Should().BeFalse();
    }

    [Fact]
    public void TheRosterRateLimitsSpeakersAndSkipsDeafenedListeners()
    {
        var roster = new VoiceRoster();
        var speaker = roster.Join(1, "room:a", canSpeak: true, maxPerRoom: 0, out _)!;
        var deaf = roster.Join(2, "room:a", canSpeak: true, maxPerRoom: 0, out _)!;
        roster.Join(3, "room:a", canSpeak: true, maxPerRoom: 0, out _);
        var silenced = roster.Join(4, "room:a", canSpeak: false, maxPerRoom: 0, out _)!;
        roster.Join(5, "room:b", canSpeak: true, maxPerRoom: 0, out _);
        deaf.Flags = VoiceFlags.Deafened;

        roster.ListenersOf(speaker).Select(m => m.CharacterId).Should().BeEquivalentTo([3, 4]);
        roster.AllowFrame(silenced, 60, 0).Should().BeFalse();
        roster.AllowFrame(speaker, VoiceRoster.MaxFrameBytes + 1, 0).Should().BeFalse();
        for (int i = 0; i < VoiceRoster.MaxFramesPerSecond; i++)
            roster.AllowFrame(speaker, 60, 10).Should().BeTrue();
        roster.AllowFrame(speaker, 60, 20).Should().BeFalse();
        roster.AllowFrame(speaker, 60, 1010).Should().BeTrue();
        speaker.Flags = VoiceFlags.MicMuted;
        roster.AllowFrame(speaker, 60, 3000).Should().BeFalse();
    }

    [Fact]
    public void ARoomWithACapRefusesTheNextVoiceSeat()
    {
        var roster = new VoiceRoster();
        roster.Join(1, "room:a", true, maxPerRoom: 2, out _).Should().NotBeNull();
        roster.Join(2, "room:a", true, maxPerRoom: 2, out _).Should().NotBeNull();
        roster.Join(3, "room:a", true, maxPerRoom: 2, out var others).Should().BeNull();
        others.Select(m => m.CharacterId).Should().BeEquivalentTo([1, 2]);
        roster.CountIn("room:a").Should().Be(2);
    }

    [Fact]
    public void TheOggReaderTakesTheLengthFromTheLastGranule()
    {
        var file = RoomMediaTests.OggOpus(channels: 1, durationMs: 12_340);
        OggOpusInfo.TryRead(file, out var info).Should().BeTrue();
        info.Should().Be(new OggOpusInfo(1, 12_340));

        var broken = (byte[])file.Clone();
        broken[0] = (byte)'X';
        OggOpusInfo.TryRead(broken, out _).Should().BeFalse();
        OggOpusInfo.TryRead(file[..^10], out _).Should().BeFalse();
    }

    [Fact]
    public async Task TheRelayAnswersAHelloAndForwardsAFrameOverUdp()
    {
        int port = FreeUdpPort();
        var settings = new GameServerSettings();
        settings.ChatRooms.Voice.BindHost = "127.0.0.1";
        settings.ChatRooms.Voice.UdpPort = port;
        var roster = new VoiceRoster();
        var relay = new VoiceRelayService(roster, new SessionManager(), Options.Create(settings), Substitute.For<ILogger<VoiceRelayService>>());
        var speaker = roster.Join(1, "room:a", true, 0, out _)!;
        var listener = roster.Join(2, "room:a", true, 0, out _)!;
        using var speakerSeal = new VoiceSeal(speaker.SessionId, speaker.Key);
        using var listenerSeal = new VoiceSeal(listener.SessionId, listener.Key);
        using var speakerSocket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var listenerSocket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var server = new IPEndPoint(IPAddress.Loopback, port);
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await relay.StartAsync(stop.Token);
        try
        {
            await WaitForPortAsync(relay);
            var datagram = new byte[256];
            var plain = new byte[256];

            int length = listenerSeal.Seal(VoiceSeal.ClientToServer, [VoiceDatagram.Hello], datagram);
            await listenerSocket.SendAsync(datagram.AsMemory(0, length), server, stop.Token);
            var ack = await listenerSocket.ReceiveAsync(stop.Token);
            listenerSeal.TryOpen(VoiceSeal.ServerToClient, ack.Buffer, plain, out int ackLength).Should().BeTrue();
            plain[..ackLength].Should().Equal(VoiceDatagram.Hello);

            length = speakerSeal.Seal(VoiceSeal.ClientToServer, [VoiceDatagram.Frame, 5, 0, 11, 22, 33], datagram);
            await speakerSocket.SendAsync(datagram.AsMemory(0, length), server, stop.Token);
            var heard = await listenerSocket.ReceiveAsync(stop.Token);
            listenerSeal.TryOpen(VoiceSeal.ServerToClient, heard.Buffer, plain, out int heardLength).Should().BeTrue();
            plain[0].Should().Be(VoiceDatagram.Frame);
            BinaryPrimitives.ReadInt32LittleEndian(plain.AsSpan(1)).Should().Be(1);
            BinaryPrimitives.ReadUInt16LittleEndian(plain.AsSpan(5)).Should().Be(5);
            plain[VoiceDatagram.ServerFrameHeader..heardLength].Should().Equal(11, 22, 33);
        }
        finally
        {
            await relay.StopAsync(CancellationToken.None);
        }
    }

    private static int FreeUdpPort()
    {
        using var probe = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)probe.Client.LocalEndPoint!).Port;
    }

    private static async Task WaitForPortAsync(VoiceRelayService relay)
    {
        for (int i = 0; i < 100 && relay.UdpPort == 0; i++)
            await Task.Delay(20);
        relay.UdpPort.Should().NotBe(0);
    }
}
