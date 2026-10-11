using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const int LookPreviewWidth = 220;
    private const int LookPreviewHeight = 300;
    private const float LookTurnDegrees = 30f;

    private CanvasLayer _genderLayer = null!;
    private HudWindow _genderPanel = null!;
    private LookPreview _genderPreview = null!;
    private LookEditor _genderEditor = null!;
    private Label _genderStatus = null!;
    private Button _genderConfirm = null!;
    private bool _genderShown, _genderArmed, _genderInFlight;

    private void GenderChangeInit()
    {
        BuildGenderPanel();
        Net.I.GenderChangeRefusedEvent += OnGenderChangeRefused;
        Net.I.GenderChangedEvent += OnGenderChanged;
    }

    private void GenderChangeDispose()
    {
        Net.I.GenderChangeRefusedEvent -= OnGenderChangeRefused;
        Net.I.GenderChangedEvent -= OnGenderChanged;
    }

    private void BuildGenderPanel()
    {
        _genderLayer = new CanvasLayer { Layer = 74 };
        AddChild(_genderLayer);

        _genderPanel = new HudWindow("genderchange", "Gender Change") { Visible = false };
        _genderPanel.Closed += CloseGenderChange;
        _genderLayer.AddChild(_genderPanel);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        _genderPanel.Body.AddChild(row);

        _genderPreview = new LookPreview(LookPreviewWidth, LookPreviewHeight);
        row.AddChild(LookPreviewColumn(_genderPreview));

        var form = new VBoxContainer();
        form.AddThemeConstantOverride("separation", 8);
        row.AddChild(form);
        _genderEditor = new LookEditor();
        _genderEditor.Changed += OnGenderLookChanged;
        form.AddChild(_genderEditor);

        _genderStatus = HudStyle.Label(12);
        _genderStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _genderStatus.CustomMinimumSize = new Vector2(230, 0);
        form.AddChild(_genderStatus);

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 8);
        form.AddChild(actions);
        _genderConfirm = LookActionButton("Change", OnGenderConfirmPressed);
        actions.AddChild(_genderConfirm);
        actions.AddChild(LookActionButton("Cancel", CloseGenderChange));
    }

    private static Control LookPreviewColumn(LookPreview preview, bool zoom = false)
    {
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 6);
        column.AddChild(preview);
        var turnRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        turnRow.AddThemeConstantOverride("separation", 8);
        turnRow.AddChild(LookEditor.StepButton("◀", () => preview.Turn(-LookTurnDegrees)));
        turnRow.AddChild(LookEditor.StepButton("▶", () => preview.Turn(LookTurnDegrees)));
        if (zoom) turnRow.AddChild(LookActionButton("Zoom", preview.ToggleZoom));
        column.AddChild(turnRow);
        return column;
    }

    private static Button LookActionButton(string text, System.Action pressed)
    {
        var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.None };
        button.AddThemeFontSizeOverride("font_size", 13);
        button.Pressed += pressed;
        return button;
    }

    private void OpenGenderChange()
    {
        if (_genderInFlight) return;
        var me = Net.I.LastEnter;
        if (!GenderChange.CanChange(me.Class))
        {
            CombatNotice(ItemData.Text(GenderChange.NotForClassText, "Your class cannot change gender."));
            return;
        }
        _genderEditor.SetLocked(false);
        _genderEditor.Load(GenderChange.AllowedRaces(me.Class), me.Race, me.Face, me.Hair);
        DisarmGenderChange();
        ShowGenderLook();
        _genderPanel.Visible = true;
        _genderShown = true;
        _genderPreview.AlwaysRender = _genderPanel.PluginReplaced;
        PluginHost.Game.RaiseGenderChange();
    }

    private void CloseGenderChange()
    {
        if (!_genderShown) return;
        _genderShown = false;
        _genderPanel.Visible = false;
        _genderPreview.AlwaysRender = false;
        _genderEditor.CloseColourPicker();
        _genderPreview.Clear();
        PluginHost.Game.RaiseGenderChange();
    }

    private void OnGenderLookChanged()
    {
        if (_genderInFlight) return;
        DisarmGenderChange();
        ShowGenderLook();
    }

    private void ShowGenderLook()
    {
        _genderEditor.Refresh();
        _genderPreview.Show(_genderEditor.Race, _genderEditor.Face, _genderEditor.HairStyle, _genderEditor.HairColour);
        _genderConfirm.Disabled = _genderInFlight;
        PluginHost.Game.RaiseGenderChange();
    }

    private void DisarmGenderChange()
    {
        _genderArmed = false;
        _genderConfirm.Text = "Change";
        _genderStatus.Text = "";
    }

    private void OnGenderConfirmPressed()
    {
        if (!_genderShown || _genderInFlight || _selfDead) return;
        if (!_genderArmed)
        {
            _genderArmed = true;
            _genderConfirm.Text = "Confirm";
            _genderStatus.Text = $"Your {ItemData.DisplayName(GenderChange.Item).Trim()} will be used. Press Confirm to change.";
            return;
        }
        SubmitGenderChange();
    }

    private void SubmitGenderChange()
    {
        if (_genderInFlight || _selfDead) return;
        _genderInFlight = true;
        _genderEditor.SetLocked(true);
        _genderConfirm.Disabled = true;
        _genderStatus.Text = "Changing…";
        Net.I.SendGenderChange(_genderEditor.Race, _genderEditor.Face, _genderEditor.Hair);
        PluginHost.Game.RaiseGenderChange();
    }

    private void OnGenderChangeRefused(int result)
    {
        if (!_genderInFlight) return;
        _genderInFlight = false;
        _genderEditor.SetLocked(false);
        DisarmGenderChange();
        _genderConfirm.Disabled = false;
        CloseGenderChange();
        Notice.Show(this, result == Net.GenderChangeNoItem
            ? ItemData.Text(GenderChange.NoItemText, "It does not have items.")
            : ItemData.Text(GenderChange.FailedText, "Gender change failed."));
    }

    private void OnGenderChanged()
    {
        if (!_genderInFlight) return;
        _genderInFlight = false;
        _genderEditor.SetLocked(false);
        CloseGenderChange();
        Notice.Show(GetTree().Root, ItemData.Text(GenderChange.ChangedText, "Change current character"));
    }
}
