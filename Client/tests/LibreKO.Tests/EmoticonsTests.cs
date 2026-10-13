using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class EmoticonsTests
{
    private const int Smile = 1;
    private const int ThumbsUp = 47;

    public EmoticonsTests() => Emoticons.Seed(new[]
    {
        new EmoticonInfo(Smile, "(Smile)", 2, 2),
        new EmoticonInfo(ThumbsUp, "(Thumbs up)", 86, 44),
    });

    [Fact]
    public void KnownCodesBecomeEmoticonsBetweenTheText()
    {
        var parts = Emoticons.Split("hi (Smile) there(Thumbs up)");

        Assert.Equal(new[]
        {
            new EmoticonSegment("hi ", 0),
            new EmoticonSegment("(Smile)", Smile),
            new EmoticonSegment(" there", 0),
            new EmoticonSegment("(Thumbs up)", ThumbsUp),
        }, parts);
    }

    [Fact]
    public void UnknownOrOpenBracketsStayText()
    {
        Assert.Equal(new[] { new EmoticonSegment("see (this) and (that", 0) }, Emoticons.Split("see (this) and (that"));
        Assert.False(Emoticons.Contains("plain words"));
    }

    [Fact]
    public void CodesMatchWhateverTheCase()
    {
        Assert.True(Emoticons.Contains("(smile)"));
        Assert.Equal(Smile, Emoticons.Split("(SMILE)")[0].Emoticon);
    }
}
