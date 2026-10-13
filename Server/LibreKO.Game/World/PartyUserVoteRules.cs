using System.Text;

namespace LibreKO.Game.World;

public static class PartyUserVoteRules
{
    public const byte JuraidZone = 87;
    public const int Cost = 4_000_000;
    public const int MinimumVoters = 3;
    public const int CooldownSeconds = 300;
    public const int AnswerSeconds = 30;
    public const int OtherTextMaxBytes = 60;
    public const byte ReasonOther = 4;
    public const byte LastReason = ReasonOther;
    public const byte AnswerNo = 0;
    public const byte AnswerYes = 1;
    public const byte AnswerDraw = 2;

    public const short Started = 1;
    public const short NotHere = -1;
    public const short NoSuchUser = -2;
    public const short AlreadyRunning = -3;
    public const short TooFewVoters = -4;
    public const short CoolingDown = -5;
    public const short AlreadyAnswered = -6;
    public const short TextTooLong = -7;
    public const short NotEnoughCoins = -8;

    public static bool TextFits(string text) => Encoding.UTF8.GetByteCount(text) <= OtherTextMaxBytes;

    public static bool Warns(IEnumerable<byte> answers)
    {
        int yes = 0, no = 0;
        foreach (byte answer in answers)
        {
            if (answer == AnswerYes) yes++;
            else if (answer == AnswerNo) no++;
        }
        return yes > no;
    }

    public static short Standing(short warnings, DateTime? warnedAt, DateTime now, TimeSpan lasts) =>
        warnedAt is { } at && now - at >= lasts ? (short)0 : warnings;

    public static bool Barred(short standing, int limit) => standing >= limit;
}
