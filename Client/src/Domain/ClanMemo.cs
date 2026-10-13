namespace LibreKO.Domain;

public static class ClanMemo
{
    public const int MaxLength = 20;
    public const byte WriteKind = 3;
    public const byte Written = 1;
    public const int TitleText = 200000;
    public const int PromptText = 200001;
    public const int PlaceholderText = 200002;
    private const byte LengthRefused = 2;
    private const byte ClanUnavailable = 3;
    private const byte NotInClan = 5;

    public static int FailureText(byte result) => result switch
    {
        LengthRefused => 30034,
        ClanUnavailable => 30030,
        NotInClan => 6906,
        _ => 30028,
    };
}
