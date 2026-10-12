using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class CreateCharacterTextsTests
{
    [Theory]
    [InlineData(1, 1911)]
    [InlineData(2, 1907)]
    [InlineData(3, 1912)]
    [InlineData(10, 1913)]
    [InlineData(12, 7654)]
    public void EachRefusalUsesTheRetailText(int code, int text) =>
        Assert.Equal(text, CreateCharacterTexts.TextFor(code));

    [Theory]
    [InlineData(11)]
    [InlineData(99)]
    public void AnUnknownRefusalSaysUnknownError(int code) =>
        Assert.Equal(CreateCharacterTexts.UnknownText, CreateCharacterTexts.TextFor(code));
}
