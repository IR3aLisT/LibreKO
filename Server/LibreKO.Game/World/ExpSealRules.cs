namespace LibreKO.Game.World;

public static class ExpSealRules
{
    public const byte SubOn = 1;
    public const byte SubOff = 2;
    public const byte SubStore = 3;
    public const byte SubTotal = 4;
    public const byte SubUseJar = 5;

    public const byte Succeeded = 1;
    public const short StoreTooLittle = 2;
    public const short StoreWhileOn = 3;
    public const short UseWhileOn = 3;
    public const short UseFailed = 0;

    public const int EmptyJar = 810354000;
    public const int SmallJar = 810355000;
    public const int LargeJar = 810356000;
    public const int RebirthJar = 810357000;
    public const long SmallJarExp = 50_000_000;
    public const long LargeJarExp = 100_000_000;
    public const long FillStep = 100_000_000;

    private static readonly long[] Amounts = [100_000_000, 500_000_000, 1_000_000_000];

    public static bool IsOfferedAmount(long amount) => Array.IndexOf(Amounts, amount) >= 0;

    public static int FilledJarsFor(long amount) => (int)(amount / FillStep);

    public static long ExpOfJar(int jarId) => jarId switch
    {
        SmallJar => SmallJarExp,
        LargeJar or RebirthJar => LargeJarExp,
        _ => 0,
    };
}
