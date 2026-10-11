namespace LibreKO.Domain;

public static class CommanderOrders
{
    public const int MarkingSkill = 492065;
    public const int OrderSkill = 492066;
    public const int MarkSound = 340000;
    public const double MarkSeconds = 60.0;
    public const int NoTargetText = 30952;
    public const int AppointText = 3434;
    public const int NamedText = 3435;
    public const int YouText = 3436;
    public const int KeyHintText = 43641;
    public const float PinReach = 30f;
    public const int ForwardSound = 340001;
    public const int BackSound = 340002;
    public const int BackHomeSound = 340003;
    public const int NoGlyph = -1;
    public const int NoSlot = -1;
    private const int ForwardSlot = 1;
    private const int BackSlot = 2;
    private static readonly (int Id, string Fallback, int Slot)[] SlashWords =
    {
        (9552, "Charge", ForwardSlot), (9553, "Attack", ForwardSlot), (9554, "Go", ForwardSlot),
        (9555, "Retreat", BackSlot), (9556, "Fallback", BackSlot), (9557, "Back", BackSlot),
    };

    public const byte None = 0;
    public const byte Forward = 1;
    public const byte Back = 2;
    public const byte BackHome = 3;
    public const byte Right = 5;
    public const byte Left = 6;

    public readonly record struct Slot(int Skill, byte Order);

    public static readonly Slot[] Bar =
    {
        new(MarkingSkill, None),
        new(492066, Forward),
        new(492067, Back),
        new(492068, BackHome),
        new(492073, Right),
        new(492072, Left),
    };

    public static bool RedFlash(byte order) => order is Back or BackHome;

    public static int Sound(byte order) => order switch
    {
        Forward or Right or Left => ForwardSound,
        Back => BackSound,
        BackHome => BackHomeSound,
        _ => 0,
    };

    public static int Glyph(byte order) => order switch
    {
        Forward => 0,
        Back => 1,
        BackHome => 2,
        Right => 3,
        Left => 4,
        _ => NoGlyph,
    };

    public static bool RedRing(int glyph) => glyph is 1 or 2;

    public static int SheetCell(byte order) => order switch
    {
        Forward => 0,
        Left => 1,
        Right => 2,
        BackHome => 3,
        Back => 4,
        _ => NoGlyph,
    };

    public static int SkillOf(byte order)
    {
        foreach (var slot in Bar)
            if (slot.Order == order && order != None) return slot.Skill;
        return 0;
    }

    public static bool ClearsMark(int markedId, int targetId) => markedId >= 0 && markedId == targetId;

    public static int SlashSlot(string word, System.Func<int, string, string> text)
    {
        foreach (var (id, fallback, slot) in SlashWords)
            if (string.Equals(word, text(id, fallback), System.StringComparison.OrdinalIgnoreCase)) return slot;
        return NoSlot;
    }
}
