namespace LibreKO.Domain;

public static class PkLadder
{
    public const double FreshSeconds = 10.0;
    public const int RowsShown = 10;
    public const int TitleText = 43730;
    public const string Notice = "◁◁ Formulated in accordance from current players data. Ladder is refreshed between noon and midnight.▷▷";

    private const int RonarkLand = 71;
    private const int Ardream = 72;
    private const int RonarkLandBase = 73;

    public static bool IsLadderZone(int zone) => zone is RonarkLand or Ardream or RonarkLandBase;

    public static bool IsFresh(double receivedAt, double now) => receivedAt > 0 && now - receivedAt <= FreshSeconds;

    public static string Points(int points) => points.ToString("n0");

    public static string Bonus(int bonus) => bonus > 0 ? $"(+{bonus:n0})" : "";

    public static string Place(int place) => place > 0 ? place.ToString() : "-";
}
