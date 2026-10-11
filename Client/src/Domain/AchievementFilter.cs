using System;

namespace LibreKO.Domain;

public enum AchievementStatusFilter
{
    All,
    InProgress,
    Completed,
}

public static class AchievementFilter
{
    public static readonly (AchievementStatusFilter Filter, string Label)[] Options =
    {
        (AchievementStatusFilter.All, "All"),
        (AchievementStatusFilter.InProgress, "In progress"),
        (AchievementStatusFilter.Completed, "Completed"),
    };

    public static bool Passes(AchievementStatusFilter filter, bool completed) => filter switch
    {
        AchievementStatusFilter.InProgress => !completed,
        AchievementStatusFilter.Completed => completed,
        _ => true,
    };

    public static bool Matches(string name, string objective, string search)
    {
        string needle = search.Trim();
        return needle.Length == 0
            || name.Contains(needle, StringComparison.OrdinalIgnoreCase)
            || objective.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }
}
