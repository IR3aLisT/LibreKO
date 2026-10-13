using System;
using System.Collections.Generic;
using System.Linq;

namespace LibreKO.Domain;

public readonly record struct GmCommandInfo(string Group, string Syntax, string Description);

public static class GmCommands
{
    public static bool Matches(GmCommandInfo command, string search)
    {
        string needle = search.Trim();
        return needle.Length == 0
            || command.Syntax.Contains(needle, StringComparison.OrdinalIgnoreCase)
            || command.Description.Contains(needle, StringComparison.OrdinalIgnoreCase)
            || command.Group.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<(string Group, IReadOnlyList<GmCommandInfo> Commands)> Grouped(
        IEnumerable<GmCommandInfo> commands, string search) =>
        commands.Where(command => Matches(command, search))
            .GroupBy(command => command.Group)
            .Select(group => (group.Key, (IReadOnlyList<GmCommandInfo>)group.ToList()))
            .ToList();
}
