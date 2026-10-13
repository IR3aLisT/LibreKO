using System.Collections.Generic;
using Godot;
using LibreKO.Network;
using LibreKO.Plugins;

namespace LibreKO;

public partial class World
{
    private const float TargetDropIconSize = 20f;
    private const int TargetDropFont = 12;

    private VBoxContainer _targetDropRows = null!;
    private int _targetDropsFor = -1;
    private IReadOnlyList<TargetQuestDrop> _targetDrops = new List<TargetQuestDrop>();

    private void BuildTargetQuestDrops(VBoxContainer box)
    {
        _targetDropRows = new VBoxContainer { Visible = false, CustomMinimumSize = new Vector2(TargetHudWidth, 0) };
        _targetDropRows.AddThemeConstantOverride("separation", 2);
        box.AddChild(_targetDropRows);
        Net.I.TargetQuestDropsEvent += OnTargetQuestDrops;
    }

    private void TargetQuestDropsDispose() => Net.I.TargetQuestDropsEvent -= OnTargetQuestDrops;

    private void OnTargetQuestDrops(int id, IReadOnlyList<TargetQuestDrop> drops)
    {
        if (id != _selectedId) return;
        if (id == _targetDropsFor && SameDrops(drops)) return;
        _targetDropsFor = id;
        _targetDrops = drops;
        RebuildTargetDropRows();
        PluginHost.Game.RaiseTarget();
    }

    private bool SameDrops(IReadOnlyList<TargetQuestDrop> drops)
    {
        if (drops.Count != _targetDrops.Count) return false;
        for (int i = 0; i < drops.Count; i++)
            if (drops[i] != _targetDrops[i]) return false;
        return true;
    }

    private void SyncTargetQuestDrops(int shown)
    {
        if (shown == _targetDropsFor || _targetDropsFor < 0) return;
        _targetDropsFor = -1;
        _targetDrops = new List<TargetQuestDrop>();
        RebuildTargetDropRows();
        PluginHost.Game.RaiseTarget();
    }

    private void RebuildTargetDropRows()
    {
        foreach (var child in _targetDropRows.GetChildren()) child.QueueFree();
        foreach (var drop in _targetDrops)
        {
            var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            row.AddThemeConstantOverride("separation", 6);
            row.AddChild(new TextureRect
            {
                Texture = ItemData.Icon(drop.ItemId),
                CustomMinimumSize = new Vector2(TargetDropIconSize, TargetDropIconSize),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            });
            var line = HudStyle.Label(TargetDropFont);
            line.Text = drop.Line(ItemData.DisplayName(drop.ItemId));
            row.AddChild(line);
            _targetDropRows.AddChild(row);
        }
        _targetDropRows.Visible = _targetDrops.Count > 0;
    }

    private IReadOnlyList<GameTargetDrop> TargetQuestDropsForPlugin()
    {
        var rows = new List<GameTargetDrop>(_targetDrops.Count);
        foreach (var drop in _targetDrops)
            rows.Add(new GameTargetDrop(ItemData.Icon(drop.ItemId), drop.Line(ItemData.DisplayName(drop.ItemId))));
        return rows;
    }
}
