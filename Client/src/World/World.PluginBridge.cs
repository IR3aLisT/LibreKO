using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LibreKO.Plugins;

namespace LibreKO;

public partial class World
{
    private PluginGameBridge? _pluginBridge;
    private readonly List<GameLogLine> _pluginLog = new();

    private const int PluginHudLayer = 64;
    private const int PluginLogMax = 200;
    private static readonly Color PluginSystemLineColor = new("#9fd3ff");

    private const string ClanWindowId = "clan";
    private const string PresetWindowId = "presets";
    private const string TitlesWindowId = "titles";

    private static readonly Dictionary<string, string> MainWindowKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["character_info"] = "Character",
        ["inventory"] = "Inventory",
        ["skills"] = "Skills",
        ["quests"] = "Quests",
    };

    private void PluginBridgeInit()
    {
        if (PluginHost.Ui.ExtraHud.Count > 0)
        {
            var layer = new CanvasLayer { Layer = PluginHudLayer };
            AddChild(layer);
            foreach (var build in PluginHost.Ui.ExtraHud)
                layer.AddChild(build());
        }
        _pluginBridge = new PluginGameBridge(this);
        _pluginBridge.Subscribe();
        PluginHost.ThemeChanged += OnThemeChanged;
        PluginHost.Game.Attach(_pluginBridge, _pluginBridge, _pluginBridge, _pluginBridge, _pluginBridge,
            _pluginBridge, _pluginBridge, _pluginBridge, _pluginBridge, _pluginBridge, _pluginBridge);
        PluginServicesInit();
        PluginAppearanceInit();
        PluginFriendsInit();
    }

    private void PluginNotifySkills()
    {
        if (_pluginBridge != null) Callable.From(PluginHost.Game.RaiseSkills).CallDeferred();
    }

    private void PluginLogAdd(GameLogKind kind, string text, Color color)
    {
        var line = new GameLogLine(kind, text, color);
        _pluginLog.Add(line);
        if (_pluginLog.Count > PluginLogMax) _pluginLog.RemoveAt(0);
        if (_pluginBridge != null) PluginHost.Game.RaiseLogLine(line);
    }

    private void PluginNotifyQuests()
    {
        if (_pluginBridge != null) Callable.From(PluginHost.Game.RaiseQuests).CallDeferred();
    }

    private void OnThemeChanged()
    {
        if (!_worldReady || _self == null || !IsInstanceValid(_self)) return;
        var at = _self.GlobalPosition;
        var (koX, koZ) = Coord.ToKo(at.X, at.Z);
        Net.I.RebuildWorldInPlace(koX, koZ, at.Y);
    }

    private void PluginBridgeDispose()
    {
        PluginHost.ThemeChanged -= OnThemeChanged;
        PluginHost.Game.Detach();
        _pluginBridge?.Unsubscribe();
        _pluginBridge = null;
        _pluginServices = null;
    }

    private void PluginNotifyInventory() => _pluginBridge?.RaiseInventory();

    private void PluginNotifyTarget() => _pluginBridge?.RefreshTarget();

    private void PluginNotifyHotbar() => _pluginBridge?.RaiseHotbar();

    private void PluginNotifyMap(float headingDegrees) => _pluginBridge?.RaiseMap(headingDegrees);

    private sealed class PluginGameBridge : IGameCharacter, IGameInventory, IGameTarget, IGameWindows, IGameChat,
        IGameHotbar, IGameMap, IGameCommands, IGameLog, IGameQuests, IGameSkills
    {
        private const string SelectPlayerNotice = "Select a player to trade with.";
        private const int NoTargetText = 7680;

        public void GoTown() => _w.TownRecallPress();

        public void ToggleSit() => _w.ToggleSitting();

        public void TradeWithTarget()
        {
            if (!IsPlayer)
            {
                _w.CombatNotice(SelectPlayerNotice);
                return;
            }
            _w.BeginTradeRequest(_targetId, _targetName);
        }

        public void InviteTarget()
        {
            if (!IsPlayer)
            {
                _w.CombatNotice(ItemData.Text(NoTargetText, "No target selected."));
                return;
            }
            _w.InvitePlayerToParty(_targetName);
        }

        public void ShowExitMenu() => _w.ToggleEsc(true);

        public void Run(KeyAction action) => _w.RunKeyAction(action);

        private readonly World _w;
        private int _targetId = -1, _targetHp, _targetMaxHp;
        private string _targetName = "";
        private TargetHpLook _targetLook;
        private float _heading;
        private string _mapStem = "";
        private Texture2D? _mapTexture;

        public PluginGameBridge(World w) => _w = w;

        event Action? IGameCharacter.Changed { add { } remove { } }
        event Action? IGameInventory.Changed { add { } remove { } }
        event Action? IGameTarget.Changed { add { } remove { } }
        event Action? IGameHotbar.Changed { add { } remove { } }
        event Action? IGameMap.Changed { add { } remove { } }
        event Action<string>? IGameChat.LineAdded { add { } remove { } }
        event Action<GameChatLine>? IGameChat.LineArrived { add { } remove { } }
        event Action? IGameChat.InputRequested { add { } remove { } }
        event Action<GameLogLine>? IGameLog.LineAdded { add { } remove { } }
        event Action? IGameQuests.Changed { add { } remove { } }
        event Action? IGameWindows.TopRightRowMoved { add { } remove { } }
        public Rect2 TopRightRow => _w._topRightRow;
        event Action? IGameSkills.Changed { add { } remove { } }

        public IReadOnlyList<GameSkillTab> Tabs =>
            SkillData.Pages(_w._selfClass, _w.SelfTransformModel()).Select(p => new GameSkillTab(p.Category, p.Label)).ToList();

        public int MasteryPool => _w.Mastery.Pool;

        public IReadOnlyList<GameMasteryTree> Trees
        {
            get
            {
                var list = new List<GameMasteryTree>();
                for (int type = MasteryPoints.FirstTree; type <= MasteryPoints.LastTree; type++)
                {
                    bool shown = type != MasteryPoints.MasterTree || MasteryPoints.ClassHasTree(_w._selfClass, type);
                    list.Add(new GameMasteryTree(type, SkillData.PageName(_w._selfClass, type), _w.Mastery.InTree(type),
                        shown, _w.CanSpendMastery(type), _w.MasteryHint(type)));
                }
                return list;
            }
        }

        public IReadOnlyList<GameSkill> Skills(int category)
        {
            var list = new List<GameSkill>();
            foreach (var page in SkillData.Pages(_w._selfClass, _w.SelfTransformModel()))
            {
                if (page.Category != category) continue;
                foreach (var s in page.Skills)
                {
                    bool met = _w.SkillRequirementMet(s);
                    var icon = met ? SkillData.Icon(s.Id) ?? SkillData.EnigmaIcon() : SkillData.EnigmaIcon();
                    list.Add(new GameSkill(s.Id, s.Name, icon, met, SkillTooltip(s)));
                }
            }
            return list;
        }

        public GameSkillInfo Info(int skillId)
        {
            if (SkillData.Get(skillId) is not { } s) return default;
            return new GameSkillInfo(s.Desc.Replace('|', '\n'), s.Msp, s.Level, s.Level, SkillData.MasteryType(s.Tree) > 0,
                SkillData.WeaponRequirementName(s.NeedWeapon),
                s.NeedItem != 0 ? ItemData.DisplayName(s.NeedItem) : "",
                s.ConsumedItem != 0 && s.ConsumedItem != s.NeedItem ? ItemData.DisplayName(s.ConsumedItem) : "");
        }

        public void SpendMastery(int type) => _w.OnMasterySpend(type);

        public void AddToHotbar(int skillId) => _w.AddToHotbar(skillId);

        public void Subscribe()
        {
            var net = Net.I;
            net.SelfHpEvent += OnHp;
            net.SelfMpEvent += OnMp;
            net.PointChangeEvent += OnPoints;
            net.GoldChangeEvent += OnGold;
            net.ExpChangeEvent += OnExp;
            net.ItemStatsEvent += OnStats;
            net.NoticeEvent += OnNotice;
            net.RankAuraShownEvent += OnRankAura;
            net.MailUnreadEvent += OnMailUnread;
        }

        private void OnMailUnread(int unread) => RaiseCharacter();

        private void OnRankAura(int charId, bool shown)
        {
            if (charId == Net.I.MyCharId) RaiseCharacter();
        }

        public void Unsubscribe()
        {
            var net = Net.I;
            net.SelfHpEvent -= OnHp;
            net.SelfMpEvent -= OnMp;
            net.PointChangeEvent -= OnPoints;
            net.GoldChangeEvent -= OnGold;
            net.RankAuraShownEvent -= OnRankAura;
            net.ExpChangeEvent -= OnExp;
            net.ItemStatsEvent -= OnStats;
            net.NoticeEvent -= OnNotice;
        }

        private void OnNotice(string text) => _w.PluginLogAdd(GameLogKind.System, text, PluginSystemLineColor);

        public IReadOnlyList<GameLogLine> History => _w._pluginLog;

        public IReadOnlyList<GameQuestTrack> Tracked => _w.TrackedQuestsForPlugins();

        private void OnHp(int a, int b, int c) => RaiseCharacter();
        private void OnMp(int a, int b) => RaiseCharacter();
        private void OnPoints(int a, int b, int c, int d, int e, int f) => RaiseCharacter();
        private void OnGold(int a) { RaiseCharacter(); RaiseInventory(); }
        private void OnExp(long a) => RaiseCharacter();
        private void OnStats(DerivedStats s) => RaiseCharacter();

        public void RaiseCharacter() => Callable.From(PluginHost.Game.RaiseCharacter).CallDeferred();

        public void RaiseInventory() => Callable.From(PluginHost.Game.RaiseInventory).CallDeferred();

        public void RaiseHotbar() => Callable.From(PluginHost.Game.RaiseHotbar).CallDeferred();

        public void RaiseMap(float headingDegrees)
        {
            _heading = headingDegrees;
            PluginHost.Game.RaiseMap();
        }

        public void RefreshTarget()
        {
            int id = _w._selectedId >= 0 && _w._ents.ContainsKey(_w._selectedId) ? _w._selectedId : -1;
            int hp = 0, max = 0;
            string name = "";
            if (id >= 0 && _w._ents.TryGetValue(id, out var e)) { hp = e.Hp; max = e.MaxHp; name = _w.ShownName(e); }
            var look = id >= 0 ? (TargetHpLook)PartyHud.HpLook(Net.I.PartyStatusOf(id), (long)World.Now()) : TargetHpLook.Normal;
            if (id == _targetId && hp == _targetHp && max == _targetMaxHp && name == _targetName && look == _targetLook) return;
            _targetId = id; _targetHp = hp; _targetMaxHp = max; _targetName = name; _targetLook = look;
            PluginHost.Game.RaiseTarget();
        }

        public string Name => Net.I.LastEnter.Name;
        public int Class => _w._selfClass;
        public string ClassName => World.ClassName(_w._selfClass);
        public int Race => _w._selfRace;
        public int Nation => Net.I.LastEnter.Nation;
        public string NationName => World.NationName(Net.I.LastEnter.Nation);
        public int Level => _w.Sheet.Level;
        public int Hp => _w.Vitals.Hp;
        public int MaxHp => _w.Vitals.MaxHp;
        public int Mp => _w.Vitals.Mp;
        public int MaxMp => _w.Vitals.MaxMp;
        public long Exp => _w.Sheet.Exp;
        public long MaxExp => _w.Sheet.MaxExp;
        public double ExpPercent => _w.Sheet.ExpPercent;
        public int Gold => _w.Sheet.Gold;
        public int KnightCash => _w.Sheet.KnightCash;
        public int Weight => _w.CarriedWeight();
        public int MaxWeight => _w.Sheet.MaxWeight;
        public int Str => _w.Sheet.Str;
        public int Sta => _w.Sheet.Sta;
        public int Dex => _w.Sheet.Dex;
        public int Intel => _w.Sheet.Intel;
        public int Mag => _w.Sheet.Mag;
        public int Points => _w.Sheet.Points;
        public int Ap => _w.Sheet.Ap;
        public int Ac => _w.Sheet.Ac;
        public int Np => _w.Sheet.Np;
        public int Manner => _w.Sheet.Manner;
        public bool NoahKnight => _w.SelfIsNoahKnight;
        public int MailUnread => Net.I.MailUnread;
        public int Resist(int index) => _w.Sheet.ResistAt(index);
        public void AllocateStat(int statRow) => _w.OnAllocate(statRow);
        public int Sp => _w.Vitals.Sp;
        public int MaxSp => _w.Vitals.MaxSp;
        public int RebirthLevel => _w.Sheet.RebirthLevel;
        public int RebirthStat(int statRow) => _w.Sheet.RebirthBonusAtRow(statRow);
        public int StatBonus(int statRow) => _w.Sheet.StatBonusAtRow(statRow);
        string IGameCharacter.ClanName => Net.I.MyClan.InClan ? Net.I.MyClan.Name ?? "" : "";
        public bool HasRankAura => _w.SelfHasRankAura;
        public bool RankAuraShown => Net.I.OwnRankAuraShown;
        public void SetRankAuraShown(bool shown) => Net.I.SetOwnRankAuraShown(shown);
        public Texture2D? Portrait => _w.PortraitTexture;
        public void SetPortraitShown(bool shown) => _w.SetPortraitShown(shown);

        public int Length => _w.Inv.Length;
        public int GridStart => World.GridStart;
        public int GridCount => World.GridCount;

        public GameItem At(int slot)
        {
            if (slot < 0 || slot >= _w.Inv.Length) return GameItem.Empty(slot);
            var it = _w.Inv[slot];
            if (it.IsEmpty) return GameItem.Empty(slot);
            var def = ItemData.Get(it.ItemId);
            return new GameItem(slot, it.ItemId, ItemData.DisplayName(it.ItemId), ItemData.ShownCount(def, it),
                it.Durability, def?.UnbindStones ?? 0, def?.Kind ?? 0, ItemData.Icon(it.ItemId),
                def != null && Inventory.IsTwoHandedSlotType(def.Slot), it.ExpiresAt, it.Flag);
        }

        public void Move(int from, int to) => _w.MoveBetween(from, to);

        public void Use(int slot) => _w.InventoryContext(slot);

        public void Drop(int slot) => _w.AskDeleteItem(slot);

        public void Arrange() => Net.I.SendInventoryArrange();

        void IGameInventory.ShowTooltip(int slot)
        {
            if (slot < 0 || slot >= _w.Inv.Length || _w.Inv[slot].IsEmpty) return;
            _w.ShowItemTooltip(slot, _w.Inv[slot]);
        }

        void IGameInventory.HideTooltip() => _w.HideItemTooltip();

        public bool Has => _targetId >= 0;
        public int Id => _targetId;
        string IGameTarget.Name => _targetName;
        int IGameTarget.Level => _targetId >= 0 && _w._ents.TryGetValue(_targetId, out var e) ? e.Level : 0;
        int IGameTarget.Hp => _targetHp;
        int IGameTarget.MaxHp => _targetMaxHp;
        public bool Hostile => _targetId >= 0 && _w._ents.TryGetValue(_targetId, out var e) && e.Attackable;
        public bool IsPlayer => _targetId >= 0 && _w._ents.TryGetValue(_targetId, out var e) && !e.IsNpc;
        public TargetHpLook HpLook => _targetLook;
        public bool IsMonster => _targetId >= 0 && _w._ents.TryGetValue(_targetId, out var e) && e.IsMonster;
        public int NpcId => _targetId >= 0 && _w._ents.TryGetValue(_targetId, out var e) && e.IsNpc ? e.NpcId : 0;
        int IGameTarget.ClanId => _targetId >= 0 && _w._ents.TryGetValue(_targetId, out var e) && !e.IsNpc ? e.KnightsId : 0;
        string IGameTarget.ClanName => _targetId >= 0 && _w._ents.TryGetValue(_targetId, out var e) && !e.IsNpc ? e.ClanName : "";

        public GameDropTable? Drops
        {
            get
            {
                if (_w.CachedDrops(_targetId) is not { Shown: true } list) return null;
                var items = list.Items.Select(d => new GameDrop(d.ItemId, ItemData.DisplayName(d.ItemId), ItemData.Icon(d.ItemId), (int)d.Band)).ToList();
                return new GameDropTable(list.TargetId, list.CoinMin, list.CoinMax, list.Experience, items);
            }
        }

        public void RequestDrops() => _w.RequestDropsFor(_targetId);

        public IReadOnlyList<GameTargetDrop> QuestDrops => _w.TargetQuestDropsForPlugin();

        public void RaiseDrops(int targetId)
        {
            if (targetId == _targetId) PluginHost.Game.RaiseTarget();
        }

        public void Clear() => _w.Deselect();

        public bool IsOpen(string id)
        {
            if (MainWindowKeys.TryGetValue(id, out var key)) return _w.MainWindowOpen(key);
            return HudWindow.Find(id)?.Visible ?? false;
        }

        public void Open(string id)
        {
            if (MainWindowKeys.TryGetValue(id, out var key)) { _w.ShowMainWindow(key); return; }
            if (HudWindow.Find(id) is { } win) win.Visible = true;
        }

        public void Close(string id)
        {
            if (MainWindowKeys.TryGetValue(id, out var key))
            {
                if (_w.MainWindowOpen(key)) _w.ToggleMainWindow(key);
                return;
            }
            if (HudWindow.Find(id) is { } win) win.Visible = false;
        }

        public void Toggle(string id)
        {
            if (string.Equals(id, ClanWindowId, StringComparison.OrdinalIgnoreCase)) { _w.OpenCharacterPage(CharacterPage.Clan); return; }
            if (string.Equals(id, PresetWindowId, StringComparison.OrdinalIgnoreCase)) { _w.TogglePreset(); return; }
            if (string.Equals(id, TitlesWindowId, StringComparison.OrdinalIgnoreCase)) { _w.ToggleTitlePicker(); return; }
            if (MainWindowKeys.TryGetValue(id, out var key)) { _w.ToggleMainWindow(key); return; }
            if (HudWindow.Find(id) is { } win) win.Visible = !win.Visible;
        }

        IReadOnlyList<string> IGameChat.History => _w.Chat?.History ?? Array.Empty<string>();
        IReadOnlyList<GameChatLine> IGameChat.Lines => _w.Chat?.Lines ?? Array.Empty<GameChatLine>();

        public void Send(string text) => _w.Chat?.SendText(text);

        public void SetTyping(bool typing)
        {
            if (_w.Chat != null) _w.Chat.PluginTyping = typing;
        }

        public int Pages => HotbarLayout.Pages;
        public int SlotsPerPage => HotbarLayout.SlotsPerPage;
        public int Page => _w._hotPage;
        public bool Locked => Config.HotbarLocked;

        public HotSlotInfo Slot(int abs)
        {
            if (abs < 0 || abs >= HotbarLayout.Total) return HotSlotInfo.Empty(abs);
            int id = _w._hotbar[abs];
            if (id == 0) return HotSlotInfo.Empty(abs);
            var s = SkillData.Get(id);
            if (s == null && ItemData.Get(id) is { Effect1: not 0 } def) s = SkillData.Get(def.Effect1);
            float cooldown = s == null ? 0f : _w.SkillCooldown(s, Now());
            bool isSkill = SkillData.IsSkill(id);
            int needId = isSkill ? s?.ConsumedItem ?? 0 : id;
            int count = -1;
            bool enough = true;
            if (needId != 0 && ItemData.Get(needId) != null)
            {
                int need = s is { IsRanged: true } ? Mathf.Max(1, s.NeedArrow) : 1;
                count = _w.CountInBackpack(needId);
                enough = count >= need;
            }
            return new HotSlotInfo(abs, id, isSkill,
                isSkill ? SkillData.Icon(id) : ItemData.Icon(id), cooldown,
                isSkill ? s?.Name ?? "" : ItemData.DisplayName(id),
                isSkill && s != null ? SkillTooltip(s) : "", count, enough,
                isSkill && s != null && !_w.Vitals.HasStamina(s.Sp));
        }

        public void Activate(int slotInPage) => _w.ActivateHotSlot(slotInPage);

        public void ActivateAbs(int abs) => _w.FireHotSlot(abs);

        public int Selected => _w._hotSelected;

        public void Select(int abs) => _w.SelectHotAbs(abs);

        public void SetPage(int page) => _w.SetHotPage(page);

        public void ChangePage(int delta) => _w.ChangeHotPage(delta);

        void IGameHotbar.Drop(int abs, int id, int fromAbs) => _w.DropOntoHotAbs(abs, id, fromAbs);

        void IGameHotbar.Clear(int abs) => _w.ClearHotAbs(abs);

        public void SetLocked(bool locked) => _w.ApplyHotbarLayout(locked, Config.HotbarVertical, Config.HotbarExtraBars);

        void IGameHotbar.ShowTooltip(int abs) => _w.HotAbsHover(abs, true);

        void IGameHotbar.HideTooltip() => _w.HideItemTooltip();

        public int Zone => _w._zone;
        public IReadOnlyList<byte> MonumentOwners => Net.I.MonumentOwners;
        public bool WantedUiOn => _w._vanguardUiOn;
        void IGameMap.ToggleWantedUi() => _w.ToggleVanguardUi();
        void IGameMap.ShowWantedList() => _w.ShowWantedList();
        public string ZoneName => MapName(_w._zone);
        public float X => _w._myKoX;
        public float Z => _w._myKoZ;
        public float HeadingDegrees => _heading;
        public float WorldExtent => _w._miniMap?.WorldExtent ?? 0f;
        public IReadOnlyList<MiniMap.Blip> Blips => _w._blipScratch;
        public int NpcKind => _w._npcFinderKind;
        public bool NpcKindPresent(int kind) => _w.NpcKindPresent(kind);
        public void PickNpcKind(int kind) => _w.PickNpcKind(kind);

        public Texture2D? MapTexture
        {
            get
            {
                string stem = _w._terrain?.ZoneStem ?? ZoneCatalog.Stem(_w._zone) ?? "";
                if (stem != _mapStem)
                {
                    _mapStem = stem;
                    _mapTexture = stem.Length > 0 ? MiniMap.LoadBaked(stem) : null;
                }
                return _mapTexture;
            }
        }
    }
}
