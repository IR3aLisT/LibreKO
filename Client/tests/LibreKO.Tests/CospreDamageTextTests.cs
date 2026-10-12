using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class CospreDamageTextTests
{
    [Theory]
    [InlineData(0, 9614)]
    [InlineData(1, 9609)]
    [InlineData(4, 9612)]
    [InlineData(5, 0)]
    [InlineData(7, 0)]
    public void DamageReceivedClassesPrintTheRetailTexts(int classCode, int text) =>
        Assert.Equal(text, CospreDamageText.Received(classCode));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 9605)]
    [InlineData(5, 9615)]
    public void DamageDealtClassZeroPrintsNothing(int classCode, int text) =>
        Assert.Equal(text, CospreDamageText.Dealt(classCode));
}
