using System.Buffers.Binary;
using LibreKO.Common.Domain.Entities.GameData;

namespace LibreKO.Game.World;

public readonly record struct RepurchaseEntry(int ItemId, ushort Count, short Durability, byte Flag, long StoredAt)
{
    public long DeletesAt => StoredAt + RepurchaseRules.KeepSeconds;
}

public static class RepurchaseRules
{
    public const int MaxEntries = 20;
    public const long KeepSeconds = 3L * ItemSlot.SecondsPerDay;
    private const int PriceFactor = 3;
    private const int CoinMax = 2_100_000_000;
    private const int EntryBytes = sizeof(int) + sizeof(ushort) + sizeof(short) + sizeof(byte) + sizeof(long);

    public static bool Keeps(ItemSlot slot) => !slot.IsEmpty && !slot.Expires && !slot.IsLinked;

    public static void Record(List<RepurchaseEntry> list, ItemSlot slot, ushort count, long now)
    {
        if (!Keeps(slot) || count == 0)
            return;
        Prune(list, now);
        list.Add(new RepurchaseEntry(slot.ItemId, count, slot.Durability, slot.Flag, now));
        while (list.Count > MaxEntries)
            list.RemoveAt(0);
    }

    public static void Prune(List<RepurchaseEntry> list, long now) => list.RemoveAll(entry => entry.DeletesAt <= now);

    public static int Price(int buyPrice, int count) =>
        (int)Math.Min(CoinMax, Math.Max(0L, (long)buyPrice * count * PriceFactor));

    public static byte[] Encode(IReadOnlyList<RepurchaseEntry> list)
    {
        var bytes = new byte[list.Count * EntryBytes];
        var span = bytes.AsSpan();
        for (var i = 0; i < list.Count; i++)
        {
            var at = span[(i * EntryBytes)..];
            var entry = list[i];
            BinaryPrimitives.WriteInt32LittleEndian(at, entry.ItemId);
            BinaryPrimitives.WriteUInt16LittleEndian(at[4..], entry.Count);
            BinaryPrimitives.WriteInt16LittleEndian(at[6..], entry.Durability);
            at[8] = entry.Flag;
            BinaryPrimitives.WriteInt64LittleEndian(at[9..], entry.StoredAt);
        }
        return bytes;
    }

    public static List<RepurchaseEntry> Decode(byte[]? bytes)
    {
        var list = new List<RepurchaseEntry>();
        if (bytes == null)
            return list;
        var span = bytes.AsSpan();
        for (var offset = 0; offset + EntryBytes <= span.Length; offset += EntryBytes)
        {
            var at = span[offset..];
            list.Add(new RepurchaseEntry(
                BinaryPrimitives.ReadInt32LittleEndian(at),
                BinaryPrimitives.ReadUInt16LittleEndian(at[4..]),
                BinaryPrimitives.ReadInt16LittleEndian(at[6..]),
                at[8],
                BinaryPrimitives.ReadInt64LittleEndian(at[9..])));
        }
        return list;
    }
}
