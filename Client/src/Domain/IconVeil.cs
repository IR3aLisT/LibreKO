namespace LibreKO.Domain;

public enum IconWash : byte { None, Exhausted, Rented, Duplicate, Sealed }

public enum IconBadge : byte { None, NotBound, Bound }

public static class IconVeil
{
    public static ItemFlag ShownFlag(ItemFlag flag, bool bindsOnEquip) =>
        flag == ItemFlag.Unsealed && bindsOnEquip ? ItemFlag.NotBound : flag;

    public static IconWash Wash(ItemSlot slot)
    {
        if (slot.IsEmpty) return IconWash.None;
        if (slot.Durability == 0) return IconWash.Exhausted;
        return slot.State switch
        {
            ItemFlag.Rented or ItemFlag.CharacterSeal => IconWash.Rented,
            ItemFlag.Duplicate => IconWash.Duplicate,
            ItemFlag.Sealed or ItemFlag.SealedPremium => IconWash.Sealed,
            _ => IconWash.None,
        };
    }

    public static IconBadge Badge(ItemFlag shownFlag) => shownFlag switch
    {
        ItemFlag.NotBound => IconBadge.NotBound,
        ItemFlag.Bound => IconBadge.Bound,
        _ => IconBadge.None,
    };
}
