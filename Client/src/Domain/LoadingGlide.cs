namespace LibreKO.Domain;

public sealed class LoadingGlide
{
    public const double GlideSeconds = 0.35;

    private double _from;
    private double _to;
    private double _startedAt;

    public double Target => _to;

    public void Set(double value, double now)
    {
        value = System.Math.Clamp(value, 0, 1);
        double shown = At(now);
        if (value < shown)
        {
            _from = _to = value;
            _startedAt = now;
            return;
        }
        _from = shown;
        _to = value;
        _startedAt = now;
    }

    public double At(double now)
    {
        double t = GlideSeconds <= 0 ? 1 : System.Math.Clamp((now - _startedAt) / GlideSeconds, 0, 1);
        double eased = 1 - (1 - t) * (1 - t);
        return _from + (_to - _from) * eased;
    }

    public static int Percent(double value) => (int)System.Math.Round(System.Math.Clamp(value, 0, 1) * 100);
}
