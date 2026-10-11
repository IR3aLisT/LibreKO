using System.Collections.Generic;
using Godot;
using LibreKO.Domain;

namespace LibreKO;

public partial class World
{
    private const int AdminCommandsWidth = 720;
    private const int AdminCommandsHeight = 380;
    private const float AdminCommandColumnWidth = 340;
    private const string AdminCommandsTab = "Commands";

    private LineEdit _admCommandSearch = null!;
    private VBoxContainer _admCommandList = null!;
    private List<GmCommandInfo> _admCommands = new();

    private Control BuildAdminCommandsTab()
    {
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(AdminCommandsWidth, 0) };
        box.AddThemeConstantOverride("separation", 6);
        box.AddChild(UiTheme.SectionTitle("GM commands", UiIcons.Get("system/help")));

        _admCommandSearch = new LineEdit { PlaceholderText = "Search commands", ClearButtonEnabled = true };
        _admCommandSearch.AddThemeFontSizeOverride("font_size", 12);
        _admCommandSearch.TextChanged += _ => RefreshAdminCommands();
        box.AddChild(_admCommandSearch);

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(AdminCommandsWidth, AdminCommandsHeight),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        box.AddChild(scroll);
        _admCommandList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _admCommandList.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_admCommandList);
        RefreshAdminCommands();
        return box;
    }

    private void OnAdminCommands(List<GmCommandInfo> commands, bool open)
    {
        _admCommands = commands;
        if (!_admEnabled || _admCommandList == null) return;
        RefreshAdminCommands();
        if (!open) return;
        if (!_admShown) ToggleAdminPanel();
        SelectAdminTab(AdminCommandsTab);
    }

    private void RefreshAdminCommands()
    {
        foreach (var child in _admCommandList.GetChildren()) { _admCommandList.RemoveChild(child); child.QueueFree(); }
        var groups = GmCommands.Grouped(_admCommands, _admCommandSearch.Text);
        if (groups.Count == 0)
        {
            _admCommandList.AddChild(UiTheme.Text(
                _admCommands.Count == 0 ? "Loading the command list…" : "No command matches your search.",
                12, UiTheme.TextLo));
            return;
        }
        foreach (var (group, commands) in groups)
        {
            _admCommandList.AddChild(UiTheme.SectionTitle(group));
            var grid = new GridContainer { Columns = Platform.Pick(2, 1) };
            grid.AddThemeConstantOverride("h_separation", 16);
            grid.AddThemeConstantOverride("v_separation", 6);
            _admCommandList.AddChild(grid);
            foreach (var command in commands) grid.AddChild(BuildAdminCommandRow(command));
        }
    }

    private static Control BuildAdminCommandRow(GmCommandInfo command)
    {
        var row = new VBoxContainer { CustomMinimumSize = new Vector2(AdminCommandColumnWidth, 0) };
        row.AddThemeConstantOverride("separation", 0);
        var syntax = UiTheme.Text(command.Syntax, 12, UiTheme.GoldBright);
        syntax.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        syntax.CustomMinimumSize = new Vector2(AdminCommandColumnWidth, 0);
        row.AddChild(syntax);
        var description = UiTheme.Text(command.Description, 11, UiTheme.TextLo);
        description.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        description.CustomMinimumSize = new Vector2(AdminCommandColumnWidth, 0);
        row.AddChild(description);
        return row;
    }
}
