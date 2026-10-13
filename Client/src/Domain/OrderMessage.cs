namespace LibreKO.Domain;

public readonly record struct OrderMessage(int Shows, float ShowSeconds, bool Blink)
{
    public const float GapSeconds = 1f;
    public const float Top = 110f;
    public const int FontSize = 20;
    private const int SplitMinLength = 10;

    public static readonly OrderMessage Flash = new(3, 2f, true);
    public static readonly OrderMessage FlashOnce = new(1, 2f, true);
    public static readonly OrderMessage Hold = new(1, 3f, false);

    public float TotalSeconds => Blink ? Shows * (ShowSeconds + GapSeconds) : ShowSeconds;

    public bool VisibleAt(float elapsed)
    {
        if (elapsed < 0 || elapsed >= TotalSeconds) return false;
        return !Blink || elapsed % (ShowSeconds + GapSeconds) < ShowSeconds;
    }

    public static string Split(string text, bool tooWide)
    {
        if (!tooWide || text.Length <= SplitMinLength) return text;
        int half = text.Length / 2;
        return text[..half] + "\n" + text[(half + 1)..];
    }
}
