namespace LibreKO.Game.World;

public static class NameRules
{
    public const char FirstCharacter = '!';
    public const char LastCharacter = '~';

    public static bool IsPlain(string name) => name.All(c => c is >= FirstCharacter and <= LastCharacter);
}
