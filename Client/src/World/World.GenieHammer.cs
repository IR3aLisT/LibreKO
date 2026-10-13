using Godot;
using LibreKO.Domain;

namespace LibreKO;

public partial class World
{
    private static readonly int[] GenieHammerItems = [810227000, 810935000, 900819000];
    private static readonly int[] GenieWeaponSlots = [InventoryConstants.RightHand, InventoryConstants.LeftHand];
    private static readonly int[] GenieArmourSlots =
        [InventoryConstants.Head, InventoryConstants.Breast, InventoryConstants.Leg, InventoryConstants.Glove, InventoryConstants.Foot];
    private const double GenieHammerCheckInterval = 10;

    private SpinBox _genieHammerThreshold = null!;
    private Label _genieHammerStatus = null!;
    private double _genieHammerNextAt;

    private void BuildGenieHammerOptions(VBoxContainer parent)
    {
        parent.AddChild(UiTheme.SectionTitle("Genie Hammer"));
        _genieHammerThreshold = GeniePercent(parent, "Repair below durability (%)", GenieSetup.MinHammerPercent,
            GenieSetup.MaxHammerPercent, v => _genie.HammerPercent = v);
        _genieHammerStatus = UiTheme.Text("Uses one hammer charge to repair worn equipment. Turn it on for weapons or armour under Set Others.",
            11, UiTheme.TextLo);
        _genieHammerStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        parent.AddChild(_genieHammerStatus);
        Net.I.GenieHammerResult += OnGenieHammerResult;
    }

    private void SyncGenieHammerControls() => _genieHammerThreshold.SetValueNoSignal(_genie.HammerPercent);

    private static void CarryGenieHammerSettings(ConfigFile config, GenieSetup setup)
    {
        bool auto = config.GetValue("genie", "auto_hammer", false).AsBool();
        setup.Set(GenieOther.HammerWeapons, auto);
        setup.Set(GenieOther.HammerArmour, auto);
        int threshold = config.GetValue("genie", "hammer_threshold", GenieSetup.DefaultHammerPercent).AsInt32();
        setup.HammerPercent = threshold is >= GenieSetup.MinHammerPercent and <= GenieSetup.MaxHammerPercent
            ? threshold : GenieSetup.DefaultHammerPercent;
    }

    private void OnGenieHammerResult(bool ok)
    {
        _genieHammerStatus.Text = ok ? "Equipment repaired. One hammer charge used."
            : "Repair was not needed or no usable Genie Hammer was available.";
    }

    private void GenieHammerTick(double now)
    {
        bool weapons = _genie.Has(GenieOther.HammerWeapons), armour = _genie.Has(GenieOther.HammerArmour);
        if ((!weapons && !armour) || now < _genieHammerNextAt) return;
        _genieHammerNextAt = now + GenieHammerCheckInterval;
        if (!(weapons && GenieWorn(GenieWeaponSlots)) && !(armour && GenieWorn(GenieArmourSlots))) return;
        bool hammer = false;
        for (int i = GridStart; i < Inv.Length; i++)
        {
            if (i >= InventoryConstants.CospreStart && i < InventoryConstants.MagicBagStart) continue;
            if ((System.Array.IndexOf(GenieHammerItems, Inv[i].ItemId) >= 0) && Inv[i].Durability > 0)
            { hammer = true; break; }
        }
        if (!hammer) { _genieHammerStatus.Text = "No Genie Hammer charges in inventory."; return; }
        _genieHammerStatus.Text = "Repairing equipment…";
        Net.I.SendGenieHammer((byte)_genie.HammerPercent);
    }

    private bool GenieWorn(int[] slots)
    {
        foreach (int i in slots)
        {
            var slot = Inv[i];
            if (slot.ItemId > 0 && ItemData.Get(slot.ItemId) is { Duration: > 1 } item
                && slot.Durability * 100.0 <= item.Duration * _genie.HammerPercent)
                return true;
        }
        return false;
    }
}
