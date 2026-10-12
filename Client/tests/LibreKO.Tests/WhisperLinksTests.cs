using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class WhisperLinksTests
{
    [Fact]
    public void AWebAddressBecomesALink() =>
        Assert.Equal("See [url=https://libreko.org/news][u]https://libreko.org/news[/u][/url] now",
            WhisperLinks.Linkify("See https://libreko.org/news now"));

    [Fact]
    public void BracketsAreEscaped() => Assert.Equal("[lb]GM] hi", WhisperLinks.Linkify("[GM] hi"));

    [Theory]
    [InlineData("https://libreko.org", true)]
    [InlineData("http://x.y/z?a=1", true)]
    [InlineData("item:389010000", false)]
    [InlineData("see https://libreko.org", false)]
    public void OnlyAWholeAddressCountsAsALink(string text, bool link) => Assert.Equal(link, WhisperLinks.IsWebLink(text));
}
