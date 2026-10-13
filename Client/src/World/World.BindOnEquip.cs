using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const int TextBindOnEquipTitle = 19003;
    private const int TextBindOnEquipAsk = 19004;

    private Notice? _bindAsk;
    private System.Action? _equipAfterBind;

    private bool AskBindBeforeEquip(int bagAbs, System.Action equip)
    {
        var item = Inv[bagAbs];
        if (ItemData.ShownFlag(item) != ItemFlag.NotBound) return false;
        if (_equipAfterBind != null || (_bindAsk != null && IsInstanceValid(_bindAsk))) return true;
        _bindAsk = Notice.Confirm(this,
            ItemData.Text(TextBindOnEquipAsk, "Once you equip the item, the item will be bound to you and will not be tradable. Would you like to continue?"),
            "Yes", "No",
            () =>
            {
                if (Inv[bagAbs].ItemId != item.ItemId) return;
                _equipAfterBind = equip;
                Net.I.SendItemBind(item.ItemId, (byte)(bagAbs - GridStart));
            },
            title: ItemData.Text(TextBindOnEquipTitle, "Item Restoration."));
        return true;
    }

    private bool FinishBindBeforeEquip(ItemSealResult result, int srcPos)
    {
        if (_equipAfterBind is not { } equip) return false;
        _equipAfterBind = null;
        if (result != ItemSealResult.Succeeded)
        {
            CombatLogAdd(SealRefusal(ItemSealType.Bind, result), CombatLogKind.Status, WhRefusalTint);
            return true;
        }
        if (srcPos >= 0) ApplySealFlag(GridStart + srcPos, ItemSealType.Bind);
        equip();
        return true;
    }
}
