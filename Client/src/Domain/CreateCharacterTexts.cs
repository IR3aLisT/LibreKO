namespace LibreKO.Domain;

public static class CreateCharacterTexts
{
    public const int TitleText = 1901;
    public const int UnknownText = 1915;

    public static int TextFor(int code) => code switch
    {
        1 => 1911,
        2 => 1907,
        3 => 1912,
        4 => 1902,
        5 => 1906,
        6 => 1905,
        7 => 1908,
        8 => 1910,
        9 => 1904,
        10 => 1913,
        12 => 7654,
        _ => UnknownText,
    };
}
