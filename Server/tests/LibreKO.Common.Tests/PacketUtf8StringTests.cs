using FluentAssertions;
using LibreKO.Common.Infrastructure.Network;

namespace LibreKO.Common.Tests;

public class PacketUtf8StringTests
{
    [Theory]
    [InlineData("Hola Mundo")]
    [InlineData("¿Dónde está la Manzana de Moradon?")]
    [InlineData("Menissiah te encargó participar en la Batalla del Caos")]
    [InlineData("Türkçe: ışığın gücü")]
    [InlineData("Русский текст")]
    [InlineData("한국어 텍스트")]
    [InlineData("")]
    public void WriteUtf8String_RoundTripsNonAsciiText(string text)
    {
        var packet = new Packet(GameOpcodes.GS_SELECT_MSG);
        packet.WriteUtf8String(text);

        packet.ReadUtf8String().Should().Be(text);
    }

    [Theory]
    [InlineData("Dónde 😀")]
    [InlineData("⚔️ gg 👍")]
    public void WriteString_RoundTripsUtf8Text(string text)
    {
        var packet = new Packet(GameOpcodes.GS_SELECT_MSG);
        packet.WriteString(text);
        packet.WriteSByteString(text);

        packet.ReadString().Should().Be(text);
        packet.ReadSByteString().Should().Be(text);
    }

    [Fact]
    public void WriteString_KeepsAsciiBytesUnchanged()
    {
        var packet = new Packet(GameOpcodes.GS_SELECT_MSG);
        packet.WriteSByteString("Zeus");

        packet.GetData().Should().Equal(4, (byte)'Z', (byte)'e', (byte)'u', (byte)'s');
    }

    [Fact]
    public void WriteSByteString_CutsLongTextOnACharacterBoundary()
    {
        var packet = new Packet(GameOpcodes.GS_SELECT_MSG);
        packet.WriteSByteString(string.Concat(Enumerable.Repeat("😀", 100)));

        packet.ReadSByteString().Should().Be(string.Concat(Enumerable.Repeat("😀", 63)));
    }

    [Fact]
    public void WriteUtf8String_CarriesTheLongestDialogueInTheTextTable()
    {
        var longest = new string('á', 828);
        var packet = new Packet(GameOpcodes.GS_SELECT_MSG);
        packet.WriteUtf8String(longest);

        packet.ReadUtf8String().Should().Be(longest);
    }

    [Fact]
    public void WriteUtf8String_RejectsTextThatWouldOverflowTheLengthPrefix()
    {
        var packet = new Packet(GameOpcodes.GS_SELECT_MSG);

        var write = () => packet.WriteUtf8String(new string('a', ushort.MaxValue + 1));

        write.Should().Throw<ArgumentException>();
    }
}
