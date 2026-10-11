using FluentAssertions;
using LibreKO.Game.World;

namespace LibreKO.Game.Tests;

public class NameRulesTests
{
    [Theory]
    [InlineData("Zeus", true)]
    [InlineData("x_Rikka-1", true)]
    [InlineData("two words", false)]
    [InlineData("Zeús", false)]
    [InlineData("Zeus😀", false)]
    public void NamesStayPlainPrintableCharacters(string name, bool plain) =>
        NameRules.IsPlain(name).Should().Be(plain);
}
