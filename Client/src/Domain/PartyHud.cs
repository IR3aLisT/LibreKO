namespace LibreKO.Domain;

public enum PartyHpLook { Normal, Lasting, Drop, Slow, Undead }

public static class PartyHud
{
    public const int MaxRows = 8;
    public const byte StatusDamageOverTime = 1;
    public const byte StatusPoison = 2;
    public const byte StatusSlow = 3;
    public const byte StatusGreyHp = 5;

    private static readonly (byte Status, PartyHpLook Look)[] LookOrder =
    {
        (StatusGreyHp, PartyHpLook.Undead),
        (StatusDamageOverTime, PartyHpLook.Lasting),
        (StatusPoison, PartyHpLook.Drop),
        (StatusSlow, PartyHpLook.Slow),
    };

    public static float Fill(int current, int max) =>
        max > 0 ? System.Math.Clamp((float)current / max, 0f, 1f) : 0f;

    public static PartyHpLook HpLook(System.Collections.Generic.IReadOnlyCollection<byte>? statuses, long second)
    {
        if (statuses == null || statuses.Count == 0) return PartyHpLook.Normal;
        int present = 0;
        foreach (var (status, _) in LookOrder)
            if (Contains(statuses, status)) present++;
        if (present == 0) return PartyHpLook.Normal;
        long pick = second % present;
        foreach (var (status, look) in LookOrder)
        {
            if (!Contains(statuses, status)) continue;
            if (pick-- == 0) return look;
        }
        return PartyHpLook.Normal;
    }

    public static string ClassIcon(int classCode) => CharacterClassCatalog.Family(classCode) switch
    {
        1 => "system/stat-str",
        2 => "system/stat-dex",
        3 => "system/stat-int",
        4 => "system/stat-hp",
        5 => "system/combat-defence",
        _ => "system/user",
    };

    private static bool Contains(System.Collections.Generic.IReadOnlyCollection<byte> statuses, byte status)
    {
        foreach (byte s in statuses)
            if (s == status) return true;
        return false;
    }
}
