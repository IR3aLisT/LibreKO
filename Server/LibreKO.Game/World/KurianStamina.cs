namespace LibreKO.Game.World;

public static class KurianStamina
{
    public const byte HighLevel = 60;
    public const short LowLevelMax = 150;
    public const short HighLevelMax = 250;
    public const int RegenPerSecond = 10;

    public static bool Applies(UserSession session) => ClassIdHelper.IsPortuKurian(session.Class);

    public static short MaxFor(byte level) => level < HighLevel ? LowLevelMax : HighLevelMax;

    public static bool CanPay(UserSession session, int cost) =>
        cost <= 0 || !Applies(session) || session.KurianSp >= cost;

    public static void Refill(UserSession session)
    {
        session.KurianMaxSp = MaxFor(session.Level);
        session.KurianSp = session.KurianMaxSp;
    }

    public static bool Spend(UserSession session, int cost)
    {
        if (cost <= 0 || !Applies(session))
            return false;

        session.KurianSp = (short)Math.Max(0, session.KurianSp - cost);
        return true;
    }

    public static bool Regenerate(UserSession session, int seconds)
    {
        if (!Applies(session) || session.Hp <= 0)
            return false;

        session.KurianMaxSp = MaxFor(session.Level);
        if (session.KurianSp >= session.KurianMaxSp)
            return false;

        session.KurianSp = (short)Math.Min(session.KurianMaxSp, session.KurianSp + RegenPerSecond * seconds);
        return true;
    }
}
