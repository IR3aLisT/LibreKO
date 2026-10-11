using System.Collections.Generic;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const string FixedWeaponMeta = "fixed_weapon";
    private static readonly Color PathoistColour = new(1f, 0f, 0f);
    private readonly HashSet<int> _devilForms = new();

    private int _heldSelfTransform;

    private int ZoneLookSkill(int charId)
    {
        if (charId == _myId) return ZoneLooks.SelfLook(_zone, Net.I.Nation);
        int nation = _ents.TryGetValue(charId, out var e) ? e.Nation : 0;
        return ZoneLooks.OtherLook(_zone, Net.I.Nation, nation);
    }

    private bool ZoneLookHolds(int charId) => ZoneLookSkill(charId) > 0;

    private void HoldSelfTransform(int skillId)
    {
        if (skillId <= 0) DropBuffChip(_heldSelfTransform);
        _heldSelfTransform = skillId;
    }

    private void ApplyZoneLook(int charId)
    {
        bool self = charId == _myId;
        int nation = self ? Net.I.Nation : _ents.TryGetValue(charId, out var e) ? e.Nation : 0;
        int skill = ZoneLookSkill(charId);
        if (skill <= 0) return;
        float scale = ZoneLooks.LookScale(_zone, Net.I.Nation, skill);
        int weapon = self ? ZoneLooks.SelfWeapon(_zone, Net.I.Nation) : ZoneLooks.OtherWeapon(_zone, Net.I.Nation, nation);
        WearMonsterLook(charId, skill, scale > 0f ? scale : null, weapon);
    }

    private void RestoreLookOrZoneLook(int charId)
    {
        RestoreOwnLook(charId);
        ApplyZoneLook(charId);
    }

    private void ApplyDevilForm(int charId, bool on)
    {
        if (!on)
        {
            if (!_devilForms.Remove(charId)) return;
            RestoreOwnLook(charId, endingTransform: false);
            ApplyZoneLook(charId);
            return;
        }
        int race = charId == _myId ? _selfRace : _ents.TryGetValue(charId, out var e) ? e.Race : 0;
        int model = ZoneLooks.DevilModel(race);
        if (model <= 0 || WearLook(charId, model, ZoneLooks.DevilScale, 0, 0, null) is not { } worn) return;
        _devilForms.Add(charId);
        if (worn.GetNodeOrNull<Node3D>(ModelNodeName) is not { } visual) return;
        visual.Scale = Vector3.One * ZoneLooks.DevilStartScale;
        visual.CreateTween().TweenProperty(visual, "scale", Vector3.One * ZoneLooks.DevilScale, ZoneLooks.DevilGrowSeconds);
    }

    private int ZoneNameText(EntitySnapshot info)
    {
        int text = ZoneLooks.OtherNameText(_zone, Net.I.Nation, Net.I.IsGm, Net.I.InParty, InMyParty(info.Id));
        return text > 0 ? text : AnonymousNameText(info);
    }

    private static bool InMyParty(int charId)
    {
        foreach (var member in Net.I.Party)
            if (member.CharId == charId) return true;
        return false;
    }

    private int ShownNation(EntitySnapshot info) =>
        !info.IsNpc && ZoneLooks.IsDeityZone(_zone) ? (Net.I.Nation == Nations.ElMorad ? Nations.Karus : Nations.ElMorad) : info.Nation;

    private void ApplySelfZoneName()
    {
        int text = ZoneLooks.SelfNameText(_zone, Net.I.Nation);
        if (text > 0) SelfPlate()?.SetNameText(ItemData.Text(text, Net.I.LastEnter.Name ?? ""));
    }

    private int[] WornGear(Node3D worn, int[]? gear)
    {
        int fixedWeapon = worn.GetMeta(FixedWeaponMeta, 0).AsInt32();
        if (fixedWeapon <= 0) return gear ?? System.Array.Empty<int>();
        var armed = gear != null && gear.Length > InventoryConstants.VisLeftHand
            ? (int[])gear.Clone()
            : new int[InventoryConstants.VisualSlots.Length];
        armed[InventoryConstants.VisRightHand] = fixedWeapon;
        armed[InventoryConstants.VisLeftHand] = 0;
        return armed;
    }
}
