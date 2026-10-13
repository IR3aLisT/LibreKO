namespace LibreKO.Domain;

public static class FriendTexts
{
    public const byte Succeeded = 0;
    public const int RequestSentText = 10405;
    public const int AddedText = 10406;
    public const int LoggedInText = 10404;
    public const int RequestAskText = 44454;
    public const double RequestSeconds = 10.0;

    private const byte AddFailed = 1;
    private const byte AddListFull = 2;
    private const byte RemoveNotOnList = 2;
    private const int AddFailedText = 10400;
    private const int AddListFullText = 10401;
    private const int RemoveFailedText = 10402;
    private const int RemoveNotOnListText = 10403;
    private const int FirstRequestResultText = 44561;
    private const byte FirstRequestResult = 1;
    private const byte LastRequestResult = 9;

    public static int? AddResultText(byte code) => code switch
    {
        Succeeded => AddedText,
        AddFailed => AddFailedText,
        AddListFull => AddListFullText,
        _ => AddFailedText,
    };

    public static int? RemoveResultText(byte code) => code switch
    {
        Succeeded => null,
        RemoveNotOnList => RemoveNotOnListText,
        _ => RemoveFailedText,
    };

    public static int? RequestResultText(byte code) =>
        code is >= FirstRequestResult and <= LastRequestResult ? FirstRequestResultText + code - FirstRequestResult : null;
}
