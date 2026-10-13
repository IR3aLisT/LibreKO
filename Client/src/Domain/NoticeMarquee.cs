namespace LibreKO.Domain;

public static class NoticeMarquee
{
    public const float ScrollSpeed = 75f;
    public const int Passes = 2;
    public const float FadeSeconds = 0.5f;

    public static float PassSeconds(float barWidth, float textWidth) => (barWidth + textWidth) / ScrollSpeed;

    public static float OffsetAt(float barWidth, float elapsed) => barWidth - ScrollSpeed * elapsed;

    public static float ShowSeconds(float barWidth, float textWidth) => Passes * PassSeconds(barWidth, textWidth);
}
