namespace LibreKO.Domain;

public static class LootOpenResult
{
    public const int Empty = 0;
    public const int Opened = 1;
    public const int Gone = 2;
    public const int Expired = 3;
    public const int NotYours = 4;
    public const int NotYourParty = 5;
    public const int Captcha = 20;

    private const int TextEmpty = 6065;
    private const int TextNoAuthority = 6514;
    private const int TextCaptcha = 43583;

    public static bool BoxIsEmpty(int result) => result is Empty or Gone or Expired;

    public static int TextId(int result) => result switch
    {
        NotYours or NotYourParty => TextNoAuthority,
        Captcha => TextCaptcha,
        _ => TextEmpty,
    };

    public static string Fallback(int result) => result switch
    {
        NotYours or NotYourParty => "You do not have the authority.",
        Captcha => "Loot/Trade/Menu Usage Fail. Requires Captcha verification",
        _ => "The box is empty.",
    };
}
