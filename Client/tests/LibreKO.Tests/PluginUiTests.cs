using LibreKO.Plugins;
using Xunit;

namespace LibreKO.Tests;

public class PluginUiTests
{
    [Fact]
    public void AWindowWithoutRulesIsLeftAlone()
    {
        var ui = new PluginUi();
        Assert.Null(ui.RuleFor("inventory"));
        Assert.False(ui.IsWindowOverridden("inventory"));
        Assert.False(ui.HudHidden(HudPart.StatusBars));
        Assert.Null(ui.HudReplacement(HudPart.StatusBars));
        Assert.Null(ui.DialogBuilder);
    }

    [Fact]
    public void HidingAndReplacingBothCountAsOverrides()
    {
        var ui = new PluginUi();
        ui.HideWindow("inventory");
        ui.ReplaceWindow("skills", _ => null!);
        Assert.True(ui.RuleFor("inventory")!.Hidden);
        Assert.NotNull(ui.RuleFor("skills")!.Replacement);
        Assert.True(ui.IsWindowOverridden("inventory"));
        Assert.True(ui.IsWindowOverridden("SKILLS"));
    }

    [Fact]
    public void ExtendersAccumulateAndDoNotOverride()
    {
        var ui = new PluginUi();
        ui.ExtendWindow("clan", _ => { });
        ui.ExtendWindow("clan", _ => { });
        Assert.Equal(2, ui.RuleFor("clan")!.Extenders.Count());
        Assert.False(ui.IsWindowOverridden("clan"));
    }

    [Fact]
    public void AReplacedHudPartIsAlsoHidden()
    {
        var ui = new PluginUi();
        ui.ReplaceHud(HudPart.Hotbar, () => null!);
        ui.HideHud(HudPart.MiniMap);
        Assert.True(ui.HudHidden(HudPart.Hotbar));
        Assert.NotNull(ui.HudReplacement(HudPart.Hotbar));
        Assert.True(ui.HudHidden(HudPart.MiniMap));
        Assert.Null(ui.HudReplacement(HudPart.MiniMap));
        Assert.False(ui.HudHidden(HudPart.Chat));
    }

    [Fact]
    public void ResetForgetsEverything()
    {
        var ui = new PluginUi();
        ui.HideWindow("inventory");
        ui.HideHud(HudPart.Chat);
        ui.ReplaceDialogs(_ => null!);
        ui.Reset();
        Assert.Null(ui.RuleFor("inventory"));
        Assert.False(ui.HudHidden(HudPart.Chat));
        Assert.Null(ui.DialogBuilder);
    }

    [Fact]
    public void RemovingAnOwnerTakesOutOnlyItsEntries()
    {
        var host = new PluginUi();
        var first = host.ForOwner("first");
        var second = host.ForOwner("second");
        first.ReplaceHud(HudPart.Chat, () => null!);
        first.ExtendWindow("clan", _ => { });
        first.AddHud(() => null!);
        second.HideWindow("inventory");
        second.ExtendWindow("clan", _ => { });
        second.ReplaceDialogs(_ => null!);

        Assert.True(host.RemoveOwner("second"));
        Assert.Null(host.RuleFor("inventory"));
        Assert.Single(host.RuleFor("clan")!.Extenders);
        Assert.Null(host.DialogBuilder);
        Assert.NotNull(host.HudReplacement(HudPart.Chat));
        Assert.Single(host.ExtraHud);
        Assert.False(host.RemoveOwner("second"));
    }

    [Fact]
    public void AnEarlierOwnersReplacementShowsThroughAgain()
    {
        var host = new PluginUi();
        System.Func<WindowHost, Godot.Control> mine = _ => null!;
        System.Func<WindowHost, Godot.Control> theirs = _ => null!;
        host.ForOwner("base").ReplaceWindow("skills", mine);
        host.ForOwner("theme").ReplaceWindow("skills", theirs);
        Assert.Same(theirs, host.RuleFor("skills")!.Replacement);
        host.RemoveOwner("theme");
        Assert.Same(mine, host.RuleFor("skills")!.Replacement);
    }

    [Fact]
    public void AWindowFrameFallsBackToTheEveryWindowFrame()
    {
        var host = new PluginUi();
        System.Func<WindowFrame, Godot.Control> every = _ => null!;
        System.Func<WindowFrame, Godot.Control> inventory = _ => null!;
        var theme = host.ForOwner("theme");
        theme.FrameWindow(PluginUi.EveryWindow, every);
        theme.FrameWindow("inventory", inventory);
        Assert.Same(inventory, host.FrameFor("inventory"));
        Assert.Same(every, host.FrameFor("skills"));
        Assert.False(host.IsWindowOverridden("inventory"));
        host.RemoveOwner("theme");
        Assert.Null(host.FrameFor("inventory"));
        Assert.Null(host.FrameFor("skills"));
    }

    [Fact]
    public void TheKnownWindowListCoversTheMainPanel()
    {
        foreach (var id in new[] { "inventory", "character_info", "skills", "quests", "party", "npc_dialog" })
            Assert.Contains(id, PluginUi.KnownWindowIds);
        Assert.Equal(PluginUi.KnownWindowIds.Length, PluginUi.KnownWindowIds.Distinct().Count());
    }
}
