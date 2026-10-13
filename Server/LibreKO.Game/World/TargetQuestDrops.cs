using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Domain.Services;
using LibreKO.Game.Scripting;

namespace LibreKO.Game.World;

public static class TargetQuestDrops
{
    public const int MaxRows = 3;
    private const int ItemIdFloor = 100_000_000;
    private const int GroupIdFloor = 100;

    public readonly record struct Row(int ItemId, ushort Have, ushort Need);

    public static IReadOnlyList<Row> For(UserSession session, NpcInstance npc, IGameDataService data, IQuestDefinitionSource quests)
    {
        if (!npc.IsMonster || npc.DropItemGroup <= 0 || data.GetNpcItem(npc.DropItemGroup, npc.IsMonster) is not { } table)
            return [];
        var drops = DroppedItems(table, data);
        if (drops.Count == 0)
            return [];

        var (open, classGroup, nation, rebirth) = session.WithLock(s => (
            s.Quest.QuestMap.Where(q => q.Value is (byte)QuestStatus.Active or (byte)QuestStatus.ReadyToTurnIn).Select(q => (int)q.Key).ToArray(),
            ClassIdHelper.GroupOf(s.Class), (int)s.Nation, (int)s.RebirthLevel));

        var rows = new List<Row>();
        foreach (var questId in open)
        {
            foreach (var (item, need) in quests.CollectsFor(questId, classGroup, nation, rebirth))
            {
                if (!drops.Contains(item) || rows.Exists(r => r.ItemId == item))
                    continue;
                rows.Add(new Row(item, Clamp(BagCount(session, item)), Clamp(need)));
                if (rows.Count >= MaxRows)
                    return rows;
            }
        }
        return rows;
    }

    private static HashSet<int> DroppedItems(NpcItemData table, IGameDataService data)
    {
        var drops = new HashSet<int>();
        foreach (var (item, percent) in table.GetDrops())
        {
            if (percent <= 0)
                continue;
            if (item >= ItemIdFloor)
                drops.Add(item);
            else if (item >= GroupIdFloor && data.MakeItemGroupTable.TryGetValue(item, out var pool))
                drops.UnionWith(pool);
        }
        return drops;
    }

    private static int BagCount(UserSession session, int itemId) => session.WithLock(s =>
    {
        int total = 0;
        for (int i = InventoryConstants.InventoryStart; i < InventoryConstants.InventoryStart + InventoryConstants.HaveMax; i++)
            if (s.Inventory[i].ItemId == itemId)
                total += s.Inventory[i].Count;
        return total;
    });

    private static ushort Clamp(int value) => (ushort)Math.Clamp(value, 0, ushort.MaxValue);
}
