namespace LibreKO.Domain;

public static class ExpSeal
{
    public const byte SubOn = 1;
    public const byte SubOff = 2;
    public const byte SubStore = 3;
    public const byte SubTotal = 4;
    public const byte SubUseJar = 5;
    public const byte Succeeded = 1;

    public const byte StateUnknown = 0;
    public const byte StateOn = 1;
    public const byte StateOff = 2;

    public const int EmptyJar = 810354000;
    public const int SmallJarFlag = 49;
    public const int LargeJarFlag = 50;
    public const int RebirthJarFlag = 51;
    public const int SmallJar = 810355000;
    public const int LargeJar = 810356000;
    public const int RebirthJar = 810357000;
    public const double Capacity = 1_000_000_000.0;
    public const byte NoBagPos = 255;

    public static readonly long[] Amounts = { 100_000_000, 500_000_000, 1_000_000_000 };
    public const long MinimumStore = 100_000_000;

    public const int TooLittleText = 33350;
    public const int HelpText = 33351;
    public const int WhileOnText = 33352;
    public const int NoJarText = 33353;
    public const int NoAmountText = 33354;
    public const int GainText = 33356;
    public const int RebirthLimitText = 33357;
    public const int JarFailedText = 33358;
    public const int ObtainText = 33359;
    public const int EmptyRefusalText = 33361;

    private const short TooLittle = 2;
    private const short WhileOn = 3;
    private const short Empty = 6;
    private const short RebirthLimit = 9;

    public static int? StoreRefusal(short code) => code switch
    {
        TooLittle => TooLittleText,
        WhileOn => WhileOnText,
        _ => null,
    };

    public static (int Id, bool WithCode)? UseRefusal(short code) => code switch
    {
        Succeeded => null,
        WhileOn => (WhileOnText, false),
        Empty => (EmptyRefusalText, false),
        RebirthLimit => (RebirthLimitText, false),
        _ => (JarFailedText, true),
    };

    public static int? LocalRefusal(bool jarPlaced, long sealedTotal, long amount) =>
        !jarPlaced ? NoJarText
        : sealedTotal < MinimumStore ? TooLittleText
        : amount <= 0 ? NoAmountText
        : null;

    public static int JarForFlag(int flag) => flag switch
    {
        SmallJarFlag => SmallJar,
        LargeJarFlag => LargeJar,
        RebirthJarFlag => RebirthJar,
        _ => 0,
    };

    public static double Percent(long total) => total / Capacity * 100.0;
}
