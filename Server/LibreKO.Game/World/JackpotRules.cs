using LibreKO.Common.Enums;

namespace LibreKO.Game.World;

public static class JackpotRules
{
    public const int Chances = 10_000;
    public const int None = 1;
    public static readonly int[] Multipliers = [2, 10, 50, 100, 500, 1000];

    public static bool Active(UserSession session) =>
        session.ActiveBuffs.Values.Any(buff => buff.BuffType == BuffType.Jackpot);

    public static int Times(IReadOnlyList<int> rates, int roll)
    {
        int bound = 0;
        for (int i = 0; i < Multipliers.Length && i < rates.Count; i++)
        {
            bound += Math.Max(0, rates[i]);
            if (roll <= bound)
                return Multipliers[i];
        }
        return None;
    }

    public static int Roll(UserSession session, IReadOnlyList<int> rates) =>
        Active(session) ? Times(rates, Random.Shared.Next(1, Chances + 1)) : None;
}
