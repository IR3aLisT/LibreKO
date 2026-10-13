namespace LibreKO.Domain;

public static class CharacterLevelText
{
    public static string Of(int level, int rebirth) =>
        rebirth > 0 ? $"Level {level} / {rebirth}" : $"Level {level}";
}
