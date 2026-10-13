using System;
using System.Collections.Generic;
using Godot;
using LibreKO.Plugins;

namespace LibreKO;

public partial class World
{
    private void PluginNotifyCapeShop() => Callable.From(PluginHost.Game.RaiseCapeShop).CallDeferred();

    private void BuyCapeWithVoucher()
    {
        _capeTicket.ButtonPressed = true;
        OnCapeBuyPressed();
        _capeTicket.ButtonPressed = false;
    }

    private sealed class PluginCapeShopBridge : IGameCapeShop
    {
        private readonly World _w;

        public PluginCapeShopBridge(World w) => _w = w;

        public bool IsOpen => _w._capeShown;

        public IReadOnlyList<GameCape> Capes
        {
            get
            {
                var capes = new List<GameCape>();
                foreach (var (id, def) in Cape.Catalogue)
                    if (def.Price > 0 || def.Points > 0)
                        capes.Add(new GameCape(id, def.C, def.M, def.Name, _w.MyClan.InClan && !_w.CapeAllowed(def), def.Castle));
                capes.Sort((a, b) => a.Id.CompareTo(b.Id));
                return capes;
            }
        }

        public int Choice => _w._capeChoice;
        public int Current => _w._capeCurrent;
        public string MoneyLine => _w._capePriceLbl.Text;
        public string ContLine => _w._capeContLbl.Text;
        public string GradeLine => _w._capeReqLbl.Text;
        public bool GradeShort => Cape.TryGet(_w._capeChoice, out var def) && _w.MyClan.InClan && !_w.CapeAllowed(def);
        public string Notice => _w._capeNoteLbl.Text;
        public string Status => _w._capeStatus.Text.Length > 0 ? _w._capeStatus.Text : _w._capeHint.Text;
        public bool CanBuy => !_w._capeBuyBtn.Disabled;
        public bool CanVoucher => !_w._capeBuyBtn.Disabled && Cape.TryGet(_w._capeChoice, out var def) && def.Castle;
        public void Select(int capeId) => _w.SelectCape(capeId);
        public void Buy() => _w.OnCapeBuyPressed();
        public void Voucher() => _w.BuyCapeWithVoucher();
        public void Close() => _w.CloseCape();

        event Action? IGameCapeShop.Changed { add { } remove { } }
    }
}
