using System;

namespace LibreKO.Network;

public partial class Net
{
    private readonly struct PendingItemMove
    {
        public readonly byte Dir, Src, Dst;
        public PendingItemMove(byte dir, byte src, byte dst) { Dir = dir; Src = src; Dst = dst; }
    }
    private PendingItemMove? _pendingItemMove;
    private int _pendingRemoveSlot = -1;

    private void ResetInventoryRequests()
    {
        bool moving = _pendingItemMove.HasValue;
        _pendingItemMove = null;
        _pendingRemoveSlot = -1;
        if (moving) ItemMoveResultEvent?.Invoke(false);
    }

    private void HandleItemMove(Packet p)
    {
        if (p.RemainingBytes < 1) return;
        byte command = p.ReadByte();
        if (command == 1)
        {
            if (p.RemainingBytes < 1) return;
            bool ok = p.ReadByte() == 1;
            if (ok) ApplyPendingItemMove();
            else _pendingItemMove = null;
            ItemMoveResultEvent?.Invoke(ok);
            if (ok && ItemStatsBlock.TryRead(p, out var stats))
            {
                ApplyLastStats(stats);
                ItemStatsEvent?.Invoke(stats);
            }
        }
        else if (command == 2)
        {
            if (p.RemainingBytes < 1) return;
            bool ok = p.ReadByte() == 1;
            if (!ok)
            {
                InventoryArrangeFailedEvent?.Invoke();
                return;
            }
            if (p.RemainingBytes < InventoryConstants.HaveMax * 19) return;
            var items = new ItemSlot[InventoryConstants.HaveMax];
            for (int i = 0; i < items.Length; i++)
            {
                int itemId = p.ReadInt();
                short dur = UShortToShort(p.ReadUShort());
                short count = UShortToShort(p.ReadUShort());
                byte flag = p.ReadByte(); p.ReadUShort(); p.ReadInt();
                int expiresAt = p.ReadInt();
                items[i] = new ItemSlot
                {
                    ItemId = itemId, Durability = dur, Count = count, Flag = flag, ExpiresAt = expiresAt,
                };
            }
            ApplyLastInventoryGridRefresh(items);
            InventoryGridRefreshEvent?.Invoke(items);
        }
    }

    private const int ItemCountChangeEntryBytes = 17;

    private void HandleItemCountChange(Packet p)
    {
        if (p.RemainingBytes < 2) return;
        int entries = p.ReadShort();
        for (int i = 0; i < entries; i++)
        {
            if (p.RemainingBytes < ItemCountChangeEntryBytes) return;
            byte kind = p.ReadByte();
            byte pos = p.ReadByte();
            int itemId = p.ReadInt();
            short count = IntToShort(p.ReadInt());
            byte flag = p.ReadByte();
            short dur = p.ReadShort();
            p.ReadInt();
            if (kind is CountChangeReceived or CountChangeReceivedAlt) ItemReceivedEvent?.Invoke(itemId, false);
            else if (flag == CountChangeNewItemFlag) ItemReceivedEvent?.Invoke(itemId, true);
            if (kind == 0 || pos >= InventoryConstants.HaveMax) continue;

            int abs = InventoryConstants.InventoryStart + pos;
            var item = new ItemSlot { ItemId = itemId, Durability = dur, Count = count };
            RaiseItemGained(abs, itemId, count);
            SetLastInventorySlot(abs, item);
            InventorySlotEvent?.Invoke(abs, item);
        }
    }

    public event Action? InventoryArrangeFailedEvent;
    public event Action<int, bool>? ItemReceivedEvent;
    private const byte CountChangeReceived = 2;
    private const byte CountChangeReceivedAlt = 7;
    private const byte CountChangeNewItemFlag = 100;
    public event Action<int>? LootGoldEvent;
    public event Action<int>? LootItemEvent;
    public event Action<string, int>? PartyLootEvent;
    public event Action<int, bool>? TemporaryLootEvent;

    private void HandleItemGet(Packet p)
    {
        if (ItemGetReply.Read(p) is not { } reply) return;
        if (reply.Result == ItemGetReply.Failed)
        {
            LootFailEvent?.Invoke(reply.Failure);
            return;
        }
        if (reply.Result == ItemGetReply.PartyMember)
        {
            PartyLootEvent?.Invoke(reply.Looter ?? "", reply.ItemId);
            LootTakenEvent?.Invoke(reply.BundleId, reply.BundleSlot, reply.ItemId);
            return;
        }
        if (reply.Result is ItemGetReply.TemporaryKept or ItemGetReply.TemporaryLost)
        {
            TemporaryLootEvent?.Invoke(reply.ItemId, reply.Result == ItemGetReply.TemporaryLost);
            return;
        }
        if (reply.IsGold)
        {
            LootGoldEvent?.Invoke(reply.Money);
            GoldChangeEvent?.Invoke(reply.Money);
        }
        else if (reply.Position < InventoryConstants.HaveMax)
        {
            LootItemEvent?.Invoke(reply.ItemId);
            int abs = InventoryConstants.InventoryStart + reply.Position;
            var item = new ItemSlot { ItemId = reply.ItemId, Count = reply.Count };
            PreserveLastDurability(abs, ref item);
            RaiseItemGained(abs, reply.ItemId, reply.Count);
            SetLastInventorySlot(abs, item);
            InventorySlotEvent?.Invoke(abs, item);
        }
        LootTakenEvent?.Invoke(reply.BundleId, reply.BundleSlot, reply.ItemId);
    }

    private const int ItemRemoveNoReason = 0;

    private void HandleItemRemove(Packet p)
    {
        if (p.RemainingBytes < 1) return;
        bool ok = p.ReadByte() == 1;
        int reason = !ok && p.RemainingBytes >= 1 ? p.ReadByte() : ItemRemoveNoReason;
        if (ok && _pendingRemoveSlot >= 0)
        {
            int slot = _pendingRemoveSlot;
            SetLastInventorySlot(slot, default);
            InventorySlotEvent?.Invoke(slot, default);
        }
        _pendingRemoveSlot = -1;
        ItemRemoveResultEvent?.Invoke(ok, reason);
    }

    private void HandleUserLookChange(Packet p)
    {
        if (p.RemainingBytes < 9) return;
        int charId = p.ReadInt();
        int slot = p.ReadByte();
        int itemId = p.ReadInt();
        short durability = p.RemainingBytes >= 2 ? UShortToShort(p.ReadUShort()) : (short)0;
        if (p.RemainingBytes >= 1) p.ReadByte();
        if (_known.TryGetValue(charId, out var e) && !e.IsNpc)
        {
            int gearIndex = VisualGearIndexForLookSlot(slot);
            if (gearIndex >= 0)
            {
                if (e.Gear.Length < InventoryConstants.VisualSlotCount)
                    System.Array.Resize(ref e.Gear, InventoryConstants.VisualSlotCount);
                e.Gear[gearIndex] = itemId;
            }
        }
        LookChangeEvent?.Invoke(charId, slot, itemId, durability);
    }

    private void HandlePointChange(Packet p)
    {
        if (p.RemainingBytes < 13) return;
        int type = p.ReadByte();
        int val = p.ReadShort();
        int mhp = p.ReadShort();
        int mmp = p.ReadShort();
        int hit = p.ReadShort();
        int mw = p.ReadInt();
        PointChangeEvent?.Invoke(type, val, mhp, mmp, hit, mw);
    }

    public event System.Action<GoldChangeReply>? GoldChangeLogEvent;

    private void HandleGoldChange(Packet p)
    {
        if (GoldChangeReply.Read(p) is not { } reply) return;
        GoldChangeLogEvent?.Invoke(reply);
        if (reply.ChangesBalance) GoldChangeEvent?.Invoke(reply.Total);
    }

    private void HandleExpChange(Packet p)
    {
        if (p.RemainingBytes < 1) return;
        byte sub = p.ReadByte();
        if (sub == Domain.Jackpot.ExpSub && p.RemainingBytes >= 6) ExpJackpotEvent?.Invoke(p.ReadInt(), p.ReadUShort());
        else if (sub == 4 && p.RemainingBytes >= 8) ExpChangeEvent?.Invoke(p.ReadLong());
        else if (sub == 5 && p.RemainingBytes >= 8) DeathExpLossEvent?.Invoke(p.ReadLong());
    }

    private void ApplyPendingItemMove()
    {
        if (_pendingItemMove is not { } move)
            return;
        _pendingItemMove = null;
        if (!ItemMove.TryResolveSlots(move.Dir, move.Src, move.Dst, out int from, out int to))
            return;
        EnsureLastInventoryLength(Math.Max(from, to) + 1);
        var e = LastEnter;
        ItemMove.ApplyConfirmed(e.Inventory, move.Dir, from, to, ItemData.Get(e.Inventory[from].ItemId)?.Countable ?? 0);
        LastEnter = e;
        RefreshLastGear();
    }

    private void RefreshLastGear()
    {
        var e = LastEnter;
        var inv = e.Inventory;
        var gear = e.Gear is { Length: InventoryConstants.VisualSlotCount } existing
            ? existing
            : new int[InventoryConstants.VisualSlotCount];
        for (int i = 0; i < InventoryConstants.VisualSlots.Length; i++)
        {
            int slot = InventoryConstants.VisualSlots[i];
            gear[i] = inv != null && slot < inv.Length ? inv[slot].ItemId : 0;
        }
        e.Gear = gear;
        LastEnter = e;
    }

    private void ApplyLastStats(DerivedStats stats)
    {
        Vitals.ApplyMaxima(stats.MaxHp, stats.MaxMp);
        Sheet.ApplyDerived(stats);
        HealStrikesEnemies = stats.HealStrikesEnemies;
    }

    private void ApplyLastInventoryGridRefresh(ItemSlot[] items)
    {
        EnsureLastInventoryLength(InventoryConstants.InventoryStart + items.Length);
        var e = LastEnter;
        for (int i = 0; i < items.Length; i++)
            e.Inventory[InventoryConstants.InventoryStart + i] = items[i];
        LastEnter = e;
    }

    public void MirrorInventorySlot(int absSlot, ItemSlot item) => SetLastInventorySlot(absSlot, item);

    public void RaiseGold(int total) => GoldChangeEvent?.Invoke(total);

    private void SetLastInventorySlot(int absSlot, ItemSlot item)
    {
        if (absSlot < 0) return;
        EnsureLastInventoryLength(absSlot + 1);
        if (item.ItemId == 0 || item.Count == 0)
            item = default;
        var e = LastEnter;
        e.Inventory[absSlot] = item;
        LastEnter = e;
        if (absSlot < InventoryConstants.SlotMax || InventoryConstants.IsCospreSlot(absSlot))
            RefreshLastGear();
    }

    private void RaiseItemGained(int absSlot, int itemId, int newCount)
    {
        if (itemId == 0 || newCount <= 0) return;
        var inv = LastEnter.Inventory;
        int had = inv != null && absSlot >= 0 && absSlot < inv.Length && inv[absSlot].ItemId == itemId
            ? inv[absSlot].Count
            : 0;
        int gained = newCount - had;
        if (gained > 0) ItemGainedEvent?.Invoke(itemId, gained);
    }

    private void PreserveLastDurability(int absSlot, ref ItemSlot item)
    {
        if (item.Durability != 0) return;

        var inv = LastEnter.Inventory;
        if (inv != null && absSlot >= 0 && absSlot < inv.Length
            && inv[absSlot].ItemId == item.ItemId && inv[absSlot].Durability != 0)
        {
            item.Durability = inv[absSlot].Durability;
            return;
        }

        var def = LibreKO.Domain.ItemData.Get(item.ItemId);
        if (def != null && def.Duration > 1)
            item.Durability = (short)def.Duration;
    }

    private void EnsureLastInventoryLength(int length)
    {
        var e = LastEnter;
        var inv = e.Inventory ?? System.Array.Empty<ItemSlot>();
        if (inv.Length < length)
            System.Array.Resize(ref inv, length);
        e.Inventory = inv;
        LastEnter = e;
    }

    private static int VisualGearIndexForLookSlot(int lookSlot)
        => System.Array.IndexOf(InventoryConstants.VisualSlots, lookSlot);
}
