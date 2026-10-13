using System;
using System.Text;

namespace LibreKO.Domain;

public static class ClanBonusCard
{
    public const int OnText = 43453;
    public const string OnFallback = "O";
    public const int OffText = 43452;
    public const string OffFallback = "X";
    public const int NeedText = 42100;
    public const string NeedFallback = "If number of online clan members is %d or more, you can receive clan buff.";
    public const int BonusText = 42101;
    public const string BonusFallback =
        "EXP %d%% increase \n NP +%d increase per kill \n (Bonus buff amount varies based on number of online clan members)";
    public const int Threshold = 5;
    public const int MarkCount = 6;
    private const int ExpBase = 15;
    private const int ExpCap = 65;
    private const int NpStep = 10;

    public static int Lit(int online) => online < Threshold ? 0 : Math.Min(MarkCount, online / NpStep + 1);

    public static int ExpPercent(int online) => online < Threshold ? 0 : Math.Min(ExpCap, online + ExpBase);

    public static string Marks(int online, string on, string off)
    {
        int lit = Lit(online);
        var marks = new StringBuilder();
        for (int i = 0; i < MarkCount; i++)
        {
            if (i > 0) marks.Append(' ');
            marks.Append(i < lit ? on : off);
        }
        return marks.ToString();
    }

    public static string Tooltip(int online, string need, string bonus) =>
        online < Threshold
            ? TextTemplate.Fill(need, Threshold)
            : TextTemplate.Fill(bonus, ExpPercent(online), Lit(online));
}
