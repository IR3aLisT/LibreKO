using System.Collections.Generic;

namespace LibreKO.Network;

public readonly record struct TargetQuestDrop(int ItemId, int Have, int Need)
{
    private const int EntryBytes = 8;

    public string Line(string itemName) => $"{itemName}: {Have}/{Need}";

    public static IReadOnlyList<TargetQuestDrop> ReadList(Packet p)
    {
        var rows = new List<TargetQuestDrop>();
        if (p.RemainingBytes < 1) return rows;
        int count = p.ReadByte();
        for (int i = 0; i < count && p.RemainingBytes >= EntryBytes; i++)
            rows.Add(new TargetQuestDrop(p.ReadInt(), p.ReadUShort(), p.ReadUShort()));
        return rows;
    }
}
