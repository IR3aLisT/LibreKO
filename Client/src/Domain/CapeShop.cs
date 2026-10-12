using System.Globalization;

namespace LibreKO.Domain;

public static class CapeShop
{
    public const int PaintCont = 36000;
    public const int NowEquippedText = 10313;
    public const int CastleConfirmText = 10316;
    public const int PurchaseConfirmText = 10318;
    public const int ContLineText = 10319;
    public const int VoucherConfirmText = 10321;
    public const int VoucherRefusedText = 10322;
    public const int GradeLineText = 15711;
    public const int NoahLineText = 15713;
    private const char RetailLineBreak = '\\';

    public static bool VoucherAllowed(bool castle) => castle;

    public static int ContCost(int capeCont, bool painting) => capeCont + (painting ? PaintCont : 0);

    public static int ConfirmText(bool castle) => castle ? CastleConfirmText : PurchaseConfirmText;

    public static string Amount(int value) => value > 0 ? value.ToString("N0", CultureInfo.InvariantCulture) : "0";

    public static string Fill(string format, params string[] values)
    {
        foreach (var value in values)
        {
            int at = format.IndexOf("%s", System.StringComparison.Ordinal);
            if (at < 0) break;
            format = format[..at] + value + format[(at + 2)..];
        }
        return format.Replace(RetailLineBreak, '\n');
    }
}
