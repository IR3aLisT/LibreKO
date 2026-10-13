namespace LibreKO.Domain;

public enum SkillLearnState
{
    Learned,
    Learnable,
    Locked,
}

public readonly record struct SkillLearnInfo(SkillLearnState State, int PointsShort, int LevelNeeded, bool NeedsClassChange);

public static class SkillLearn
{
    public const double RepeatDelaySeconds = 0.4;
    public const double RepeatEverySeconds = 0.08;

    public static SkillLearnInfo For(int masteryTree, int required, int pointsInTree, int classCode, int level)
    {
        if (!MasteryPoints.IsTree(masteryTree))
            return level <= 0 || level >= required
                ? new SkillLearnInfo(SkillLearnState.Learned, 0, 0, false)
                : new SkillLearnInfo(SkillLearnState.Locked, 0, required, false);
        if (pointsInTree >= required) return new SkillLearnInfo(SkillLearnState.Learned, 0, 0, false);
        if (!MasteryPoints.ClassHasTree(classCode, masteryTree))
            return new SkillLearnInfo(SkillLearnState.Locked, required - pointsInTree, 0, true);
        int shortBy = required - pointsInTree;
        if (MasteryPoints.CapInTree(classCode, masteryTree, level) >= required)
            return new SkillLearnInfo(SkillLearnState.Learnable, shortBy, 0, false);
        int levelNeeded = masteryTree == MasteryPoints.MasterTree ? MasteryPoints.MasterTreeMinLevel + required : required;
        return new SkillLearnInfo(SkillLearnState.Locked, shortBy, levelNeeded, false);
    }
}
