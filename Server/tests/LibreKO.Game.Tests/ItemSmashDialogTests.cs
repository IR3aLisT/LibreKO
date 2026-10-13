using FluentAssertions;
using LibreKO.Game.Protocol.Writers;
using NSubstitute;

namespace LibreKO.Game.Tests;

public class ItemSmashDialogTests
{
    private const int Narki = 31524;
    private const int Karus = 1;
    private const byte DisassembleStyle = 27;

    [Fact]
    public void NarkiOpensTheDisassembleWindow()
    {
        new QuestTalk("31524.quest", "disassemble", Narki, Karus).Host.Received(1).OpenDisassembleWindow();
    }

    [Fact]
    public void TheDisassembleWindowIsDialogStyleTwentySeven()
    {
        var packet = NpcDialogPacketWriter.DisassemblePanel(Narki, "31524");
        packet.ResetOffset();
        packet.ReadInt().Should().Be(Narki);
        packet.ReadByte().Should().Be(DisassembleStyle);
    }
}
