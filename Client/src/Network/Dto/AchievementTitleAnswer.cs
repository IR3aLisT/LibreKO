namespace LibreKO.Network;

public readonly record struct AchievementTitleAnswer(int Sub, bool Reset, int TitleId, int Result, int Seconds)
{
    public const int SelectCover = 16;
    public const int SelectSkill = 17;
    public const int ResetCover = 18;
    public const int ResetSkill = 19;
    public const int Done = 1;
    public const int SelectCooldown = -5;
    public const int ResetCooldown = -1;

    public bool Cooldown => Result == (Reset ? ResetCooldown : SelectCooldown);

    public static bool Handles(int sub) => sub is >= SelectCover and <= ResetSkill;

    public static AchievementTitleAnswer? Read(int sub, Packet p)
    {
        bool reset = sub is ResetCover or ResetSkill;
        int title = 0;
        if (!reset)
        {
            if (p.RemainingBytes < 2 * sizeof(ushort)) return null;
            p.ReadUShort();
            title = p.ReadUShort();
        }
        if (p.RemainingBytes < sizeof(short)) return null;
        int result = p.ReadShort();
        int cooldown = reset ? ResetCooldown : SelectCooldown;
        int seconds = result == cooldown && p.RemainingBytes >= sizeof(ushort) ? p.ReadUShort() : 0;
        return new AchievementTitleAnswer(sub, reset, title, result, seconds);
    }
}
