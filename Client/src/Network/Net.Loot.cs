using System;
using System.Collections.Generic;

namespace LibreKO.Network;

public partial class Net
{
    public const int GoldItemId = 900000000;

    public const int LootMaxItems = 8;
    public const int LootWireSlots = 12;

    public const float LootRange = 11f;

    public event Action<int, int>? LootDropEvent;

    public event Action<int, List<LootEntry>>? LootContentsEvent;

    public event Action<int, int, int>? LootTakenEvent;

    public event Action<int>? LootFailEvent;

    public event Action<int, int>? LootRefusedEvent;

    private void HandleItemDrop(Packet p)
    {
        if (p.RemainingBytes < 9) return;
        int sourceId = p.ReadInt();
        int bundleId = p.ReadInt();
        bool hasItems = p.ReadByte() != 0;
        if (hasItems) LootDropEvent?.Invoke(sourceId, bundleId);
    }

    private void HandleBundleOpen(Packet p)
    {
        if (p.RemainingBytes < 5) return;
        int bundleId = p.ReadInt();
        int result = p.ReadByte();
        if (result != LootOpenResult.Opened)
        {
            LootRefusedEvent?.Invoke(bundleId, result);
            return;
        }

        var entries = new List<LootEntry>(LootMaxItems);
        for (int i = 0; i < LootWireSlots && p.RemainingBytes >= 6; i++)
        {
            int itemId = p.ReadInt();
            int count = p.ReadUShort();
            if (itemId != 0) entries.Add(new LootEntry(itemId, count));
        }
        LootContentsEvent?.Invoke(bundleId, entries);
    }
}
