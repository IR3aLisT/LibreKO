using LibreKO.Domain;
using LibreKO.Network;
using Xunit;

namespace LibreKO.Tests;

public class TextRunesTests
{
    private const string Grin = "😀";

    [Fact]
    public void EmojiCountAsOneCharacterLikeTheInputBox()
    {
        Assert.Equal(4, TextRunes.Count($"hi{Grin}!"));
        Assert.Equal(5, $"hi{Grin}!".Length);
    }

    [Fact]
    public void TruncateNeverSplitsAnEmoji()
    {
        Assert.Equal($"a{Grin}", TextRunes.Truncate($"a{Grin}{Grin}", 2));
        Assert.Equal($"a{Grin}", TextRunes.TruncateUtf8($"a{Grin}{Grin}", 8));
    }

    [Fact]
    public void CaretColumnsMapToStringIndicesPastEmoji()
    {
        string text = $"{Grin}ab";

        Assert.Equal(3, TextRunes.Utf16Index(text, 2));
        Assert.Equal(2, TextRunes.Column(text, 3));
        Assert.Equal(text.Length, TextRunes.Utf16Index(text, 99));
    }

    [Fact]
    public void ChatTextCrossesTheWireAsUtf8()
    {
        var packet = new Packet(GameOpcodes.GS_CHAT);
        packet.WriteString($"gg {Grin}");
        packet.WriteSByteString("Zeus");

        Assert.Equal($"gg {Grin}", packet.ReadString());
        Assert.Equal("Zeus", packet.ReadSByteString());
    }

    [Fact]
    public void ThePickerOffersOnlyEmojiThatFitOneInputCharacterOrAPresentationPair()
    {
        foreach (string emoji in Emoji.Picker)
            Assert.InRange(TextRunes.Count(emoji), 1, 2);
    }
}
