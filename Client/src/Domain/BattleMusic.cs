namespace LibreKO.Domain;

public static class BattleMusic
{
    public const float StopRange = 12f;
    public const double CheckSeconds = 1.0;

    public static bool Starts(int moral) => moral == SkillTarget.EnemyOnly;

    public static int Track(int karusBattle, int elMoradBattle, bool karus) => karus ? karusBattle : elMoradBattle;

    public static bool Ends(bool playing, float nearestHostile) => playing && nearestHostile >= StopRange;
}
