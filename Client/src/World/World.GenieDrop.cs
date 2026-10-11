using Godot;

namespace LibreKO;

public partial class World
{
    private const string DragIdKey = "id";
    private const string GenieSkillSlotKey = "genieSkillSlot";
    private const string GenieScrollSlotKey = "genieScrollSlot";

    private bool GenieSkillAllowed(SkillData.Skill s, int slot)
    {
        if (s.IsPotion || s.IsDeadFriend || s.IsBlink || s.IsGroundArea || !SkillRequirementMet(s)) return false;
        return slot < GenieSkillGroupSlots ? s.IsEnemy
            : slot < GenieSkillGroupSlots * 2
                ? s.Moral is SkillTarget.Self or SkillTarget.FriendWithMe or SkillTarget.Party or SkillTarget.PartyAll
                : s.IsFriendly;
    }

    private void WireGenieSkillSlot(Button button, int slot)
    {
        button.SetDragForwarding(
            Callable.From<Vector2, Variant>(_ => GenieSlotDrag(button, GenieSkillSlotKey, slot, _genieSkills[slot], SkillData.Icon(_genieSkills[slot]))),
            Callable.From<Vector2, Variant, bool>((_, data) => GenieSkillDrop(data, slot) != 0),
            Callable.From<Vector2, Variant>((_, data) =>
            {
                int id = GenieSkillDrop(data, slot);
                if (id == 0) return;
                if (TryDragSlot(data, GenieSkillSlotKey, out int from) && from != slot)
                {
                    int displaced = _genieSkills[slot];
                    _genieSkills[from] = displaced != 0 && SkillData.Get(displaced) is { } back && GenieSkillAllowed(back, from) ? displaced : 0;
                }
                _genieSkills[slot] = id;
                RefreshGenieSlots();
            }));
        button.GuiInput += ev =>
        {
            if (ev is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }) return;
            _genieSkills[slot] = 0;
            RefreshGenieSlots();
            button.AcceptEvent();
        };
    }

    private void WireGenieScrollSlot(Button button, int slot)
    {
        button.SetDragForwarding(
            Callable.From<Vector2, Variant>(_ => GenieSlotDrag(button, GenieScrollSlotKey, slot, _genieScrollItems[slot], ItemData.Icon(_genieScrollItems[slot]))),
            Callable.From<Vector2, Variant, bool>((_, data) => GenieScrollDrop(data) != 0),
            Callable.From<Vector2, Variant>((_, data) =>
            {
                int id = GenieScrollDrop(data);
                if (id == 0) return;
                if (TryDragSlot(data, GenieScrollSlotKey, out int from) && from != slot)
                    (_genieScrollItems[from], _genieScrollItems[slot]) = (_genieScrollItems[slot], id);
                else
                    _genieScrollItems[slot] = id;
                _genieScrollRetryAt.Clear();
                RefreshGenieScrolls();
            }));
        button.GuiInput += ev =>
        {
            if (ev is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }) return;
            _genieScrollItems[slot] = 0;
            RefreshGenieScrolls();
            button.AcceptEvent();
        };
    }

    private static Variant GenieSlotDrag(Control source, string key, int slot, int id, Texture2D? icon)
    {
        if (id == 0 || Net.I.GenieRunning) return default;
        var preview = new TextureRect
        {
            Texture = icon,
            CustomMinimumSize = new Vector2(GenieDragPreviewSize, GenieDragPreviewSize),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        source.SetDragPreview(preview);
        return new Godot.Collections.Dictionary { { DragIdKey, id }, { key, slot } };
    }

    private const float GenieDragPreviewSize = 36f;

    private int GenieSkillDrop(Variant data, int slot)
    {
        if (Net.I.GenieRunning || !DragId(data, out int id) || SkillData.Get(id) is not { } skill) return 0;
        return GenieSkillAllowed(skill, slot) ? id : 0;
    }

    private int GenieScrollDrop(Variant data)
    {
        if (Net.I.GenieRunning || !DragId(data, out int id)) return 0;
        return GenieScrollSkill(id) != null ? id : 0;
    }

    private static bool DragId(Variant data, out int id)
    {
        id = 0;
        if (data.VariantType != Variant.Type.Dictionary) return false;
        var d = data.AsGodotDictionary();
        if (!d.ContainsKey(DragIdKey)) return false;
        id = d[DragIdKey].AsInt32();
        return id > 0;
    }

    private static bool TryDragSlot(Variant data, string key, out int slot)
    {
        slot = -1;
        var d = data.AsGodotDictionary();
        if (!d.ContainsKey(key)) return false;
        slot = d[key].AsInt32();
        return slot >= 0;
    }
}
