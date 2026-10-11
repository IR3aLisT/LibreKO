namespace LibreKO.Domain;

public sealed class AttackChain
{
    public const int BasicAttack = 1;
    public const int InstantSkill = 6;
    public const double Window = 0.7;

    private const int FirstEffect = 70128;
    private const int EffectCount = 3;

    private const int Slots = 6;

    private readonly int[] _kinds = new int[Slots];
    private readonly double[] _times = new double[Slots];
    private int _next;

    public bool Chained { get; private set; }

    public bool Record(int kind, double now)
    {
        if (_next >= Slots) _next = 0;
        int slot = _next;
        int previous = (slot + Slots - 1) % Slots;
        bool hasPrevious = _times[previous] > 0;
        bool sameKind = slot == 0 ? _kinds[previous] == kind : kind == BasicAttack && _kinds[previous] == BasicAttack;
        _kinds[slot] = kind;
        _times[slot] = now;
        _next = slot + 1;
        Chained = hasPrevious && !sameKind && now - _times[previous] < Window;
        return Chained;
    }

    public static int Effect(uint roll) => FirstEffect + (int)(roll % EffectCount);

    public bool Consume()
    {
        bool chained = Chained;
        Chained = false;
        return chained;
    }
}
