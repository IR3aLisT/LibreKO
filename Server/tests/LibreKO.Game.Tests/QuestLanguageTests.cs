using FluentAssertions;
using System.Runtime.CompilerServices;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.World;
using LibreKO.Quests.Localization;
using NSubstitute;

namespace LibreKO.Game.Tests;

public class QuestLanguageTests
{
    private const string Chinese = "zh";
    private const string ShaimerLine = "Master! Shaimer is looking for you urgently, Master!";
    private const string ShaimerLineInChinese = "主人！沙默急着找你呢，主人！";

    private static string LanguagesDirectory([CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", "..", "LibreKO.Game", "Quests", "lang"));

    [Fact]
    public void AChineseSessionReadsTheChineseQuestText()
    {
        var session = new UserSession(Substitute.For<IClient>(), characterId: 1, accountId: 1)
        {
            Language = GameLanguage.Chinese,
        };
        var translations = new QuestTranslations();
        translations.LoadDirectory(LanguagesDirectory());

        session.LanguageCode.Should().Be(Chinese);
        translations.Knows(Chinese).Should().BeTrue();
        translations.Translate(session.LanguageCode, ShaimerLine).Should().Be(ShaimerLineInChinese);
    }
}
