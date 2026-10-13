using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using LibreKO.Domain;
using LibreKO.Plugins;

namespace LibreKO;

public partial class World
{
    private const int GenieRunningText = 31044;

    private void PluginNotifyGenie() => Callable.From(PluginHost.Game.RaiseGenie).CallDeferred();

    private bool GenieCellsLocked()
    {
        if (!Net.I.GenieRunning) return false;
        CombatNotice(ItemData.Text(GenieRunningText, "It cannot be used while Genie is activated."));
        return true;
    }

    private int GenieCellNumber(GenieCellKind kind, int index) => kind switch
    {
        GenieCellKind.Attack => index,
        GenieCellKind.Recovery => GenieRecoveryFirstCell + index,
        _ => GenieSupportFirstCell + index,
    };

    private static int GeniePotionTargetOf(GeniePotionSlot slot) => slot switch
    {
        GeniePotionSlot.Hp => HealTarget.Hp,
        GeniePotionSlot.Mp => HealTarget.Mp,
        _ => GeniePetTarget,
    };

    private sealed class PluginGenieBridge : IGameGenie
    {
        private readonly World _w;

        public PluginGenieBridge(World w) => _w = w;

        public bool IsOpen => _w._genieShown;
        public bool Running => Net.I.GenieRunning;
        public bool Waiting => _w._genieRequestPending;
        public int Points => _w._geniePoints;
        public int PointsPerSpirit => GeniePointsPerSpirit;
        public string RunLine => _w._genieRunLabel.Text;
        public GenieSetup Setup => _w._genie.Clone();
        public IReadOnlyList<GenieTarget> Targets =>
            _w.GenieTargetsInOrder().Select(name => new GenieTarget(name, !_w._genieMonstersOff.Contains(name))).ToList();
        public int TargetRows => GenieSubRows;
        public bool HasMore => _w._genieMore != null;

        public GenieCellView Cell(GenieCellKind kind, int index)
        {
            int id = _w._genie.Cells(kind)[index];
            return SkillData.Get(id) is { } skill
                ? new GenieCellView(id, SkillData.Icon(id), SkillTooltip(skill))
                : new GenieCellView(0, null, "");
        }

        public GeniePotionView Potion(GeniePotionSlot slot)
        {
            int id = _w.GeniePotionInUse(GeniePotionTargetOf(slot));
            return id > 0
                ? new GeniePotionView(id, ItemData.Icon(id), _w.CountInBackpack(id), ItemData.DisplayName(id))
                : default;
        }

        public void Apply(GenieSetup setup) => _w.ApplyGenieSetup(setup.Clone());

        public bool SetCell(GenieCellKind kind, int index, int skillId)
        {
            if (_w.GenieCellsLocked()) return false;
            int cell = _w.GenieCellNumber(kind, index);
            if (skillId != 0)
            {
                if (SkillData.Get(skillId) is not { } skill) return false;
                if (!_w.GenieSkillAllowed(skill, cell)) { _w.GenieRefuseSkill(skill); return false; }
            }
            _w.SetGenieCell(cell, skillId);
            _w.RefreshGenieSlots();
            _w.GenieSetupEdited();
            return true;
        }

        public void MoveCell(GenieCellKind fromKind, int fromIndex, GenieCellKind toKind, int toIndex)
        {
            if (_w.GenieCellsLocked()) return;
            int from = _w.GenieCellNumber(fromKind, fromIndex), to = _w.GenieCellNumber(toKind, toIndex);
            int id = _w.GenieCell(from);
            if (id == 0 || SkillData.Get(id) is not { } skill) return;
            if (!_w.GenieSkillAllowed(skill, to)) { _w.GenieRefuseSkill(skill); return; }
            _w.MoveGenieSkill(id, from, to);
        }

        public void PickSkill(GenieCellKind kind, int index, Vector2 screenAt)
        {
            if (_w.GenieCellsLocked()) return;
            _w.OpenGenieSkillPicker(_w.GenieCellNumber(kind, index));
            _w._genieSkillPicker.Position = (Vector2I)screenAt;
        }

        public void PickPotion(GeniePotionSlot slot, Vector2 screenAt)
        {
            _w.OpenGeniePotionPicker(GeniePotionTargetOf(slot));
            _w._geniePotionPicker.Position = (Vector2I)screenAt;
        }

        public void ToggleRun() => _w.ToggleGenieFromSub();
        public void UseSpirit() => Net.I.SendGenieSystem(Net.GenieUseSpiritPotion);
        public void OpenMore() => _w.OpenGenieMore();
        public void ToggleWindow() => _w.ToggleGenie();

        public void AddTarget()
        {
            _w.AddGenieTarget();
            _w.PluginNotifyGenie();
        }

        public void RemoveTarget(int row)
        {
            var targets = _w.GenieTargetsInOrder();
            if (row >= 0 && row < targets.Count) _w.RemoveGenieTarget(targets[row]);
            _w.PluginNotifyGenie();
        }

        public void SetTargetOn(int row, bool on)
        {
            var targets = _w.GenieTargetsInOrder();
            if (row >= 0 && row < targets.Count) _w.SetGenieTargetOn(targets[row], on);
            _w.PluginNotifyGenie();
        }

        public void Close() => _w.CloseGenie();

        event Action? IGameGenie.Changed { add { } remove { } }
    }
}
