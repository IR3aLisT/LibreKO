using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LibreKO.Domain;
using LibreKO.Plugins;

namespace LibreKO;

public partial class World
{
    private PluginServiceBridge? _pluginServices;

    private void PluginServicesInit()
    {
        _pluginServices = new PluginServiceBridge(this);
        PluginHost.Game.AttachServices(_pluginServices, _pluginServices, _pluginServices);
        PluginSocialInit();
    }

    private void PluginNotifyVendor()
    {
        if (_pluginServices != null) Callable.From(PluginHost.Game.RaiseVendor).CallDeferred();
    }

    private void PluginNotifyWarehouse()
    {
        if (_pluginServices != null) Callable.From(PluginHost.Game.RaiseWarehouse).CallDeferred();
    }

    private void PluginNotifyVipVault()
    {
        if (_pluginServices != null) Callable.From(PluginHost.Game.RaiseVipVault).CallDeferred();
    }

    private static GameItem PluginItem(int slot, ItemSlot it)
    {
        if (it.IsEmpty) return GameItem.Empty(slot);
        var def = ItemData.Get(it.ItemId);
        return new GameItem(slot, it.ItemId, ItemData.DisplayName(it.ItemId), ItemData.ShownCount(def, it),
            it.Durability, def?.UnbindStones ?? 0, def?.Kind ?? 0, ItemData.Icon(it.ItemId),
            def != null && Inventory.IsTwoHandedSlotType(def.Slot), it.ExpiresAt, it.Flag);
    }

    private sealed class PluginServiceBridge(World w) : IGameVendor, IGameWarehouse, IGameVipVault
    {
        event Action? IGameVendor.Changed { add { } remove { } }
        event Action? IGameWarehouse.Changed { add { } remove { } }
        event Action? IGameVipVault.Changed { add { } remove { } }

        bool IGameVendor.IsOpen => w._vendorShown;
        string IGameVendor.Title => w._vendorShown ? w._vendorNpcName : "";
        bool IGameVendor.PaysInNp => w.LoyaltyShop;
        int IGameVendor.Page => w._vendorPager.Page;
        int IGameVendor.Pages => w._vendorPager.Pages;

        IReadOnlyList<GameItem> IGameVendor.Items =>
            w._vendorCellIds.Select((id, i) => id == 0 ? GameItem.Empty(i) : PluginItem(i, TooltipItem(id))).ToList();

        int IGameVendor.Price(int index) => VendorId(index) is { } id ? ItemData.BuyPrice(id) : 0;

        void IGameVendor.Buy(int index)
        {
            if (VendorId(index) is not { } id) return;
            w.SelectVendorItem(id);
            w.AskBuy(id, -1);
        }

        void IGameVendor.Sell(int bagSlot) => w.VendorTakeFromBag(bagSlot);
        void IGameVendor.SetPage(int page) => w.ShowVendorPage(page);

        void IGameVendor.Search(string text)
        {
            w._vendorSearch.Text = text;
            w.OnVendorSearch();
        }

        void IGameVendor.ShowTooltip(int index)
        {
            if (VendorId(index) is { } id) w.ShowItemTooltip(-1, TooltipItem(id));
        }

        void IGameVendor.HideTooltip() => w.HideItemTooltip();
        void IGameVendor.Close() => w.CloseVendor();

        private int? VendorId(int index) =>
            w._vendorShown && index >= 0 && index < w._vendorCellIds.Length && w._vendorCellIds[index] != 0
                ? w._vendorCellIds[index] : null;

        bool IGameWarehouse.IsOpen => w._whShown;
        int IGameWarehouse.Page => w._whPage;
        int IGameWarehouse.Pages => WarehouseView.UiPages;
        int IGameWarehouse.Gold => w._whMoney;

        IReadOnlyList<GameItem> IGameWarehouse.Items =>
            Enumerable.Range(0, WhPageSize).Select(i => PluginItem(i, w._warehouse[w.WhAbs(i)])).ToList();

        void IGameWarehouse.Store(int bagSlot) => w.WhTakeFromBag(bagSlot);

        void IGameWarehouse.Take(int index)
        {
            if (w._whShown && index >= 0 && index < WhPageSize && !w._warehouse[w.WhAbs(index)].IsEmpty)
                w.WithdrawSlot(w.WhAbs(index));
        }

        void IGameWarehouse.SetPage(int page) => w.TurnWhPage(page - w._whPage);

        void IGameWarehouse.ShowTooltip(int index)
        {
            if (index >= 0 && index < WhPageSize && !w._warehouse[w.WhAbs(index)].IsEmpty)
                w.ShowItemTooltip(-1, w._warehouse[w.WhAbs(index)]);
        }

        void IGameWarehouse.HideTooltip() => w.HideItemTooltip();
        void IGameWarehouse.Close() => w.CloseWarehouse();

        bool IGameVipVault.IsOpen => w._vipWhShown;
        string IGameVipVault.Expiry => w._vipWhShown ? w.VipExpiryText() : "";
        IReadOnlyList<GameItem> IGameVipVault.Items => w._vipWh.Select((slot, i) => PluginItem(i, slot)).ToList();
        void IGameVipVault.Store(int bagSlot) => w.VipTakeFromBag(bagSlot);

        void IGameVipVault.Take(int index)
        {
            if (w._vipWhShown && index >= 0 && index < w._vipWh.Length && !w._vipWh[index].IsEmpty)
                w.AskVipWithdraw(index, -1);
        }

        void IGameVipVault.ShowTooltip(int index)
        {
            if (index >= 0 && index < w._vipWh.Length && !w._vipWh[index].IsEmpty)
                w.ShowItemTooltip(-1, w._vipWh[index]);
        }

        void IGameVipVault.HideTooltip() => w.HideItemTooltip();
        void IGameVipVault.Close() => w.CloseVipWarehouse();
    }
}
