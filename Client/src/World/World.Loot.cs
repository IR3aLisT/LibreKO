using System.Collections.Generic;
using Godot;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private sealed class LootBox
    {
        public Node3D Node = null!;
        public Vector3 BasePos;
        public double SpawnTime;
        public int OwnerId;
    }

    private readonly Dictionary<int, LootBox> _boxes = new();
    private const double LootBoxLife = 60.0;
    private const float LootRangeSq = Net.LootRange * Net.LootRange;

    private int _openBundleId = -1;
    private readonly List<LootEntry> _lootEntries = new();

    private CanvasLayer _lootLayer = null!;
    private PanelContainer _lootPanel = null!;
    private readonly ItemSlotView[] _lootCells = new ItemSlotView[LootCellCount];
    private Label _lootPrompt = null!;

    private const int LootCellCount = 12;
    private const int LootColumns = 3;
    private const int LootRows = 4;
    private const float LootCellSize = 45f;
    private const int LootCellGap = 6;
    private const float LootOpenLift = 150f;
    private const float LootBoxAnchorHeight = 0.4f;
    private const float LootScreenMargin = 8f;

    private const string ItemBoxScenePath = "res://assets/objects/itembox_jo_a1.glb";
    private const float ItemBoxScale = 1.0f;
    private const float LootClickPickRadius = 56f;
    private const int LootFailNoRoom = -11;
    private const int LootNoRoomText = 1708;
    private const int LootFailRetry = -7;
    private const int LootRetryText = 14042;
    private const int LootFailCaptcha = -20;
    private const int LootCaptchaText = 43583;
    private PackedScene? _itemBoxScene;
    private bool _itemBoxLoaded;

    private void LootInit()
    {
        BuildLootWindow();
        BuildLootPrompt();
        Net.I.LootDropEvent += OnLootDrop;
        Net.I.LootContentsEvent += OnLootContents;
        Net.I.LootTakenEvent += OnLootTaken;
        Net.I.LootFailEvent += OnLootFail;
        Net.I.LootRefusedEvent += OnLootRefused;
        Net.I.LootItemEvent += OnLootItem;
        Net.I.PartyLootEvent += OnPartyLoot;
        Net.I.TemporaryLootEvent += OnTemporaryLoot;
        Net.I.ItemReceivedEvent += OnItemReceived;
    }

    private void LootDispose()
    {
        Net.I.LootDropEvent -= OnLootDrop;
        Net.I.LootContentsEvent -= OnLootContents;
        Net.I.LootTakenEvent -= OnLootTaken;
        Net.I.LootFailEvent -= OnLootFail;
        Net.I.LootRefusedEvent -= OnLootRefused;
        Net.I.LootItemEvent -= OnLootItem;
        Net.I.PartyLootEvent -= OnPartyLoot;
        Net.I.TemporaryLootEvent -= OnTemporaryLoot;
        Net.I.ItemReceivedEvent -= OnItemReceived;
    }

    private const int ReceivedText = 6092;
    private const int ReceivedNewItemText = 7613;
    private static readonly Color ReceivedTint = Color.Color8(255, 255, 0);

    private void OnItemReceived(int itemId, bool newItem) =>
        CombatLogAdd(ItemData.Text(newItem ? ReceivedNewItemText : ReceivedText, "Received %s.")
            .Replace("%s", ItemData.DisplayName(itemId)), CombatLogKind.Resource, ReceivedTint);

    private const int PickedUpText = 6069;
    private const int PartyMemberGotText = 3413;
    private const int TemporaryKeptText = 43742;
    private const int TemporaryLostText = 43741;
    private static readonly Color LootLogTint = Color.Color8(155, 155, 255);
    private static readonly Color TemporaryKeptTint = Color.Color8(155, 237, 255);
    private static readonly Color TemporaryLostTint = Color.Color8(220, 20, 60);

    private void OnLootItem(int itemId) =>
        CombatLogAdd(ItemData.Text(PickedUpText, "Picked up %s").Replace("%s", ItemData.DisplayName(itemId)),
            CombatLogKind.Resource, LootLogTint);

    private void OnPartyLoot(string looter, int itemId)
    {
        string text = ItemData.Text(PartyMemberGotText, "%s got %s");
        int first = text.IndexOf("%s", System.StringComparison.Ordinal);
        if (first >= 0) text = text[..first] + looter + text[(first + 2)..];
        CombatLogAdd(text.Replace("%s", ItemData.DisplayName(itemId)), CombatLogKind.Resource, LootLogTint);
    }

    private void OnTemporaryLoot(int itemId, bool lost) =>
        CombatLogAdd(ItemData.Text(lost ? TemporaryLostText : TemporaryKeptText, "[%s]")
                .Replace("%s", ItemData.DisplayName(itemId)),
            lost ? CombatLogKind.Status : CombatLogKind.Resource, lost ? TemporaryLostTint : TemporaryKeptTint);

    private void OnLootRefused(int bundleId, int result)
    {
        if (bundleId != _openBundleId) return;
        bool quiet = _autoLooting;
        if (LootOpenResult.BoxIsEmpty(result)) DespawnBox(bundleId);
        else CloseLoot();
        if (!quiet) CombatNotice(ItemData.Text(LootOpenResult.TextId(result), LootOpenResult.Fallback(result)));
    }

    private void OnLootDrop(int sourceId, int bundleId)
    {
        if (_self == null) return;
        if (_boxes.ContainsKey(bundleId)) return;
        if (sourceId == _myId || !_ents.TryGetValue(sourceId, out var e)) return;

        var pos = e.Body.Position - new Vector3(0, e.Lift, 0);
        var node = BuildLootBoxNode();
        node.Position = pos;
        _entities.AddChild(node);
        _boxes[bundleId] = new LootBox { Node = node, BasePos = pos, SpawnTime = Now(), OwnerId = sourceId };
        if (e.Dead) e.CorpseRemoveAt = 0;
    }

    private bool HoldsLootBox(int entityId)
    {
        foreach (var (_, box) in _boxes)
            if (box.OwnerId == entityId) return true;
        return false;
    }

    private readonly List<int> _ownedBoxScratch = new();

    private void DropLootBoxesOf(int entityId)
    {
        _ownedBoxScratch.Clear();
        foreach (var (id, box) in _boxes)
            if (box.OwnerId == entityId) _ownedBoxScratch.Add(id);
        foreach (var id in _ownedBoxScratch) DespawnBox(id);
    }

    private Node3D BuildLootBoxNode()
    {
        var root = new Node3D();

        if (!_itemBoxLoaded)
        {
            _itemBoxLoaded = true;
            if (ResourceLoader.Exists(ItemBoxScenePath))
                _itemBoxScene = ResourceLoader.Load(ItemBoxScenePath) as PackedScene;
        }

        if (_itemBoxScene?.Instantiate() is Node3D model)
        {
            model.Scale = Vector3.One * ItemBoxScale;
            root.AddChild(model);
        }
        else
        {
            root.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.6f, 0.42f, 0.5f) },
                Position = new Vector3(0, 0.21f, 0),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.42f, 0.27f, 0.13f), Roughness = 0.7f },
            });
        }

        return root;
    }

    private void LootTick(double now)
    {
        AutoLootTick();
        if (_boxes.Count == 0) { if (_lootPrompt.Visible) _lootPrompt.Visible = false; return; }

        _despawnScratch.Clear();
        Vector3 me = _self != null ? _self.Position : Vector3.Zero;
        bool anyInRange = false;
        foreach (var (id, box) in _boxes)
        {
            if (now - box.SpawnTime >= LootBoxLife) { _despawnScratch.Add(id); continue; }
            if (box.BasePos.DistanceSquaredTo(me) <= LootRangeSq) anyInRange = true;
        }
        foreach (var id in _despawnScratch) DespawnBox(id);

        bool showPrompt = anyInRange && _openBundleId < 0;
        if (showPrompt != _lootPrompt.Visible)
        {
            _lootPrompt.Visible = showPrompt;
            if (showPrompt)
            {
                var vp = GetViewport().GetVisibleRect().Size;
                _lootPrompt.Position = new Vector2((vp.X - _lootPrompt.Size.X) * 0.5f, vp.Y - 132f);
            }
        }
    }

    private readonly List<int> _despawnScratch = new();

    private void DespawnBox(int bundleId)
    {
        if (_boxes.TryGetValue(bundleId, out var box))
        {
            if (GodotObject.IsInstanceValid(box.Node)) box.Node.QueueFree();
            _boxes.Remove(bundleId);
            if (_ents.TryGetValue(box.OwnerId, out var owner) && owner.Dead && owner.IsNpc
                && owner.CorpseRemoveAt <= 0 && !HoldsLootBox(box.OwnerId))
                owner.CorpseRemoveAt = Now() + CorpseLinger;
        }
        _autoLootTried.Remove(bundleId);
        if (_openBundleId == bundleId) CloseLoot();
    }

    private bool TryClickLootBox(Vector2 mouse)
    {
        if (_self == null || _selfDead || _boxes.Count == 0 || _camera == null) return false;
        Vector3 me = _self.Position;
        int best = -1; float bestD = LootClickPickRadius;
        foreach (var (id, box) in _boxes)
        {
            var world = box.BasePos + Vector3.Up * 0.4f;
            if (_camera.IsPositionBehind(world)) continue;
            float px = _camera.UnprojectPosition(world).DistanceTo(mouse);
            if (px >= bestD) continue;
            if (box.BasePos.DistanceSquaredTo(me) > LootRangeSq) continue;
            bestD = px; best = id;
        }
        if (best < 0)
            return false;
        _openBundleId = best;
        Net.I.SendBundleOpen(best);
        return true;
    }

    private bool HasNearbyLootBox()
    {
        if (_self == null || _selfDead || _boxes.Count == 0) return false;
        Vector3 me = _self.Position;
        foreach (var (_, box) in _boxes)
            if (box.BasePos.DistanceSquaredTo(me) <= LootRangeSq)
                return true;
        return false;
    }

    public bool OpenNearestLootBox()
    {
        if (_self == null || _selfDead || _boxes.Count == 0) return false;
        Vector3 me = _self.Position;
        int best = -1;
        float bestDistSq = LootRangeSq;
        foreach (var (id, box) in _boxes)
        {
            float d = box.BasePos.DistanceSquaredTo(me);
            if (d <= bestDistSq)
            {
                bestDistSq = d;
                best = id;
            }
        }
        if (best < 0) return false;
        _openBundleId = best;
        Net.I.SendBundleOpen(best);
        return true;
    }

    private void BuildLootWindow()
    {
        _lootLayer = new CanvasLayer { Layer = 71, Visible = false };
        AddChild(_lootLayer);

        _lootPanel = new PanelContainer();
        _lootPanel.AddThemeStyleboxOverride("panel", UiTheme.WindowPanel());
        _lootLayer.AddChild(_lootPanel);

        var margin = new MarginContainer();
        UiTheme.Margins(margin, LootCellGap);
        _lootPanel.AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", LootCellGap);
        margin.AddChild(root);

        var grid = new GridContainer { Columns = LootColumns };
        grid.AddThemeConstantOverride("h_separation", LootCellGap);
        grid.AddThemeConstantOverride("v_separation", LootCellGap);
        root.AddChild(grid);
        for (int k = 0; k < LootCellCount; k++)
        {
            int index = k % LootColumns * LootRows + k / LootColumns;
            var cell = new ItemSlotView(LootCellSize) { Index = index };
            cell.Clicked += c => { if (!c.Item.IsEmpty) TakeLoot(c.Index, c.Item.ItemId); };
            cell.RightClicked += c => { if (!c.Item.IsEmpty) ShowLootRuleMenu(c.Item.ItemId); };
            cell.Hovered += c => { if (c.Item.ItemId != Net.GoldItemId) ShowItemTooltip(-1, TooltipItem(c.Item.ItemId)); };
            cell.Unhovered += _ => HideItemTooltip();
            _lootCells[index] = cell;
            grid.AddChild(cell);
        }

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", LootCellGap);
        var takeAll = UiTheme.SmallButton("Take all", "Take every item in the box");
        takeAll.Pressed += TakeAllLoot;
        takeAll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        actions.AddChild(takeAll);
        var filter = UiTheme.SmallButton("Filter", "Choose what auto-loot picks up");
        filter.Pressed += OpenLootFilter;
        actions.AddChild(filter);
        root.AddChild(actions);
    }

    private void BuildLootPrompt()
    {
        var layer = new CanvasLayer { Layer = 65 };
        AddChild(layer);
        _lootPrompt = HudStyle.Label(16, HorizontalAlignment.Center);
        _lootPrompt.Text = "Click the box to loot";
        _lootPrompt.AddThemeColorOverride("font_color", new Color("ffe08a"));
        _lootPrompt.CustomMinimumSize = new Vector2(240, 0);
        _lootPrompt.Visible = false;
        layer.AddChild(_lootPrompt);
    }

    private void OnLootContents(int bundleId, List<LootEntry> entries)
    {
        if (bundleId != _openBundleId) return;
        if (entries.Count == 0) { DespawnBox(bundleId); return; }

        _lootEntries.Clear();
        _lootEntries.AddRange(entries);
        if (AutoLootNext()) return;
        RefreshLootWindow();
        PlaceLootWindow(bundleId);
        _lootLayer.Visible = true;
    }

    private void PlaceLootWindow(int bundleId)
    {
        var screen = GetViewport().GetVisibleRect().Size;
        var size = _lootPanel.GetCombinedMinimumSize();
        var at = new Vector2((screen.X - size.X) * 0.5f, (screen.Y - size.Y) * 0.5f);
        if (_camera != null && _boxes.TryGetValue(bundleId, out var box))
        {
            var anchor = box.BasePos + Vector3.Up * LootBoxAnchorHeight;
            if (!_camera.IsPositionBehind(anchor))
                at = _camera.UnprojectPosition(anchor) - new Vector2(0, LootOpenLift);
        }
        _lootPanel.Position = new Vector2(
            Mathf.Clamp(at.X, LootScreenMargin, Mathf.Max(LootScreenMargin, screen.X - size.X - LootScreenMargin)),
            Mathf.Clamp(at.Y, LootScreenMargin, Mathf.Max(LootScreenMargin, screen.Y - size.Y - LootScreenMargin)));
    }

    private void TakeAllLoot()
    {
        if (_openBundleId < 0 || _lootEntries.Count == 0) return;
        _autoLooting = true;
        AutoLootNext();
    }

    private void RefreshLootWindow()
    {
        for (int i = 0; i < LootCellCount; i++)
        {
            if (i >= _lootEntries.Count) { _lootCells[i].Clear(); continue; }
            var entry = _lootEntries[i];
            _lootCells[i].Set(new ItemSlot { ItemId = entry.ItemId, Count = 1 }, entry.Count);
        }
    }

    private void TakeLoot(int slot, int itemId)
    {
        if (_openBundleId < 0) return;
        Net.I.SendItemGet(_openBundleId, itemId, slot);
    }

    private void OnLootTaken(int bundleId, int bundleSlot, int itemId)
    {
        if (bundleId != _openBundleId) return;
        int taken = bundleSlot >= 0 ? bundleSlot : _lootEntries.FindIndex(entry => entry.ItemId == itemId);
        if (taken >= 0 && taken < _lootEntries.Count) _lootEntries.RemoveAt(taken);
        // The floater comes from ItemGainedEvent, which knows the real stack delta.
        Audio.PlayUi(itemId != Net.GoldItemId ? Sfx.GetItem : Sfx.CoinGet);
        if (_lootEntries.Count == 0) { DespawnBox(bundleId); return; }
        if (AutoLootNext()) return;
        RefreshLootWindow();
    }

    private void OnLootFail(int reason)
    {
        if (reason == LootFailRetry)
        {
            CombatNotice(ItemData.Text(LootRetryText, "You have failed to loot. Please try again."));
            CloseLoot();
            return;
        }
        if (reason == LootFailCaptcha)
        {
            CombatNotice(ItemData.Text(LootCaptchaText, "Loot/Trade/Menu Usage Fail. Requires Captcha verification"));
            if (_autoLooting) StopAutoLoot();
            return;
        }
        if (reason == LootFailNoRoom)
        {
            CombatNotice(ItemData.Text(LootNoRoomText, "You cannot trade or pick up items because you have either exceeded the possible quantity or the weight."));
            if (_autoLooting) StopAutoLoot();
            return;
        }
        if (_autoLooting) { StopAutoLoot(); return; }
        if (_openBundleId >= 0) Net.I.SendBundleOpen(_openBundleId);
    }

    private void CloseLoot()
    {
        _autoLooting = false;
        _openBundleId = -1;
        _lootEntries.Clear();
        if (_lootLayer != null && _lootLayer.Visible)
        {
            _lootLayer.Visible = false;
            HideItemTooltip();
        }
    }
}
