using System.Collections.Generic;
using LibreKO.Network;

namespace LibreKO.Domain;

public static class WantedMatch
{
    public static int FirstBagSlot(IReadOnlyList<ItemSlot> bag, int itemId)
    {
        if (itemId == 0) return -1;
        for (int i = 0; i < bag.Count; i++)
            if (!bag[i].IsEmpty && bag[i].ItemId == itemId) return i;
        return -1;
    }

    public static int FirstWanted(IReadOnlyList<MerchantStallItem> wanted, int itemId)
    {
        if (itemId == 0) return -1;
        for (int i = 0; i < wanted.Count; i++)
            if (!wanted[i].IsEmpty && wanted[i].ItemId == itemId) return i;
        return -1;
    }
}
