using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class CommandCatalogTests
{
    private static string Text(int id, string fallback) => id switch
    {
        8018 => "chatblock",
        8118 => "/chatblock Character ID",
        8100 => "[Private Message] : Type /%s Character ID",
        9502 => "[Start Base Experience for Monster Kills Point Event 10%%, 30%%, 50%%] /%s Increase Rate",
        _ => fallback,
    };

    [Fact]
    public void ThePrivateGroupSkipsSaveAndChatRoomAndShiftsPastMacro()
    {
        var names = CommandCatalog.Entries(CommandGroup.Private).Select(e => e.Fallback).ToArray();
        Assert.Equal(19, names.Length);
        Assert.DoesNotContain("Save", names);
        Assert.DoesNotContain("Chat_Room", names);
        Assert.DoesNotContain("Macro", names);
        var block = CommandCatalog.Entries(CommandGroup.Private).Single(e => e.Id == CommandId.ChatBlock);
        Assert.Equal((8018, 8118), (block.NameText, block.HintText));
        var achieve = CommandCatalog.Entries(CommandGroup.Private).Single(e => e.Id == CommandId.Achieve);
        Assert.Equal((8021, 8121), (achieve.NameText, achieve.HintText));
    }

    [Fact]
    public void TheGroupsHoldThe2619Counts()
    {
        Assert.Equal(6, CommandCatalog.Entries(CommandGroup.Trade).Count());
        Assert.Equal(5, CommandCatalog.Entries(CommandGroup.Party).Count());
        Assert.Equal(11, CommandCatalog.Entries(CommandGroup.Clan).Count());
        Assert.Equal(4, CommandCatalog.Entries(CommandGroup.Knights).Count());
        var king = CommandCatalog.Entries(CommandGroup.King).ToArray();
        Assert.Equal(6, king.Length);
        Assert.DoesNotContain(king, e => e.NameText == 9405);
        Assert.Equal(9506, king[^1].HintText);
        Assert.Equal(7806, CommandCatalog.GroupText(CommandGroup.King));
        Assert.Equal(7803, CommandCatalog.GroupText(CommandGroup.Clan));
    }

    [Fact]
    public void HintsCarryTheCommandNameAndTypedWordsFindTheirEntry()
    {
        var pm = CommandCatalog.Find("pm", Text)!.Value;
        Assert.Equal(CommandId.Pm, pm.Id);
        Assert.True(pm.Prefill);
        Assert.Equal("[Private Message] : Type /PM Character ID", CommandCatalog.Hint(pm, Text));
        var exp = CommandCatalog.Find("ExperiencePoint", Text)!.Value;
        Assert.Equal("[Start Base Experience for Monster Kills Point Event 10%, 30%, 50%] /ExperiencePoint Increase Rate", CommandCatalog.Hint(exp, Text));
        Assert.Equal(CommandId.ChatBlock, CommandCatalog.Find("CHATBLOCK", Text)!.Value.Id);
        Assert.Null(CommandCatalog.Find("Macro", Text));
    }
}
