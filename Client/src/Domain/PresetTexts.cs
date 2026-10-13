namespace LibreKO.Domain;

public static class PresetTexts
{
    public const int StatKind = 1;
    public const int SkillKind = 2;
    private const int Applied = 1;

    public static (int Id, string Fallback, bool WithCode) For(int kind, int result) => (kind, result) switch
    {
        (StatKind, Applied) => (43324, "You have successfully apply the Stat", false),
        (StatKind, 2) => (43325, "Available after Stat Redistribution", false),
        (StatKind, 3) => (43326, "You have failed to apply the Stat", false),
        (StatKind, 7) => (43327, "Stat Class Error", false),
        (StatKind, 8) => (43328, "Stat Point Class Error", false),
        (StatKind, _) => (43329, "Stat Preset Error:", true),
        (SkillKind, Applied) => (43330, "You have successfully applied the Skill", false),
        (SkillKind, 2) => (43331, "Available after Skill Redistribution", false),
        (SkillKind, 5) => (43332, "You have failed to apply the Skill", false),
        (SkillKind, 8) => (43334, "Stat Point Class Error", false),
        (SkillKind, 9) => (43333, "Available at level 10 or above", false),
        (SkillKind, 10) => (43335, "Available after 1st Job Change", false),
        (SkillKind, 11) => (43336, "Available after 2nd Job Change", false),
        (SkillKind, 12) => (43341, "Fail to apply Master Skill", false),
        (SkillKind, _) => (43337, "Preset Skill Error:", true),
        _ => (43338, "Preset Stat Error:", true),
    };
}
