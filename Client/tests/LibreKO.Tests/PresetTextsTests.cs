using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class PresetTextsTests
{
    [Theory]
    [InlineData(PresetTexts.StatKind, 1, 43324, false)]
    [InlineData(PresetTexts.StatKind, 2, 43325, false)]
    [InlineData(PresetTexts.StatKind, 8, 43328, false)]
    [InlineData(PresetTexts.StatKind, 4, 43329, true)]
    [InlineData(PresetTexts.SkillKind, 1, 43330, false)]
    [InlineData(PresetTexts.SkillKind, 9, 43333, false)]
    [InlineData(PresetTexts.SkillKind, 8, 43334, false)]
    [InlineData(PresetTexts.SkillKind, 12, 43341, false)]
    [InlineData(PresetTexts.SkillKind, 3, 43337, true)]
    [InlineData(5, 1, 43338, true)]
    public void EachKindAndResultPicksItsText(int kind, int result, int id, bool withCode)
    {
        var text = PresetTexts.For(kind, result);
        Assert.Equal(id, text.Id);
        Assert.Equal(withCode, text.WithCode);
    }
}
