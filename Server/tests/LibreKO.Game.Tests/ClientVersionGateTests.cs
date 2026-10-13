using FluentAssertions;
using LibreKO.Game.Protocol;

namespace LibreKO.Game.Tests;

public class ClientVersionGateTests
{
    [Theory]
    [InlineData("", null, true)]
    [InlineData("", "0.0.1", true)]
    [InlineData("0.2.0", "0.2.0", true)]
    [InlineData("0.2.0", "0.10.0", true)]
    [InlineData("0.2.0", "0.1.9", false)]
    [InlineData("0.2.0", null, false)]
    [InlineData("0.2.0", "dev", false)]
    [InlineData("not a version", "0.0.1", true)]
    public void OnlyAClientAtOrAboveTheMinimumGetsIn(string minimum, string? client, bool accepted) =>
        ClientVersionGate.Accepts(minimum, client).Should().Be(accepted);
}
