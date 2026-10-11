using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private static readonly string[] RebirthStatLabels = ["STR", "HP", "DEX", "INT", "MP"];

    private CanvasLayer _rebirthLayer = null!;
    private HudWindow _rebirthPanel = null!;
    private Label _rebirthLevelLbl = null!, _rebirthPointsLbl = null!, _rebirthStatus = null!;
    private readonly Label[] _rebirthBonusLbls = new Label[RebirthPick.StatCount];
    private readonly Label[] _rebirthPickLbls = new Label[RebirthPick.StatCount];
    private readonly Button[] _rebirthAddBtns = new Button[RebirthPick.StatCount];
    private readonly Button[] _rebirthRemoveBtns = new Button[RebirthPick.StatCount];
    private Button _rebirthBtn = null!;
    private readonly RebirthPick _rebirthPick = new();
    private byte[] _rebirthSent = [];
    private bool _rebirthShown;
    private bool _rebirthInFlight;
    private bool _rebirthResetMode;

    private void RebirthInit()
    {
        BuildRebirthPanel();
        Net.I.RebStatChangeEvent += OnRebirthStatResult;
        Net.I.RebirthResetEvent += ResetRebirthPicker;
    }

    private void RebirthDispose()
    {
        Net.I.RebStatChangeEvent -= OnRebirthStatResult;
        Net.I.RebirthResetEvent -= ResetRebirthPicker;
    }

    private void BuildRebirthPanel()
    {
        _rebirthLayer = new CanvasLayer { Layer = 74 };
        AddChild(_rebirthLayer);

        _rebirthPanel = new HudWindow("rebirth", "Rebirth", bodyMinWidth: 320) { Visible = false };
        _rebirthPanel.Closed += CloseRebirth;
        _rebirthLayer.AddChild(_rebirthPanel);

        var root = _rebirthPanel.Body;
        root.AddThemeConstantOverride("separation", 8);

        _rebirthLevelLbl = UiTheme.Text("", 16, UiTheme.GoldBright, HorizontalAlignment.Center);
        root.AddChild(_rebirthLevelLbl);

        root.AddChild(new HSeparator());
        root.AddChild(UiTheme.SectionTitle("Bonus points"));
        _rebirthPointsLbl = HudStyle.Label(13);
        root.AddChild(_rebirthPointsLbl);

        for (int row = 0; row < RebirthPick.StatCount; row++)
        {
            int index = row;
            var line = new HBoxContainer();
            line.AddThemeConstantOverride("separation", 6);
            root.AddChild(line);

            var name = HudStyle.Label(13);
            name.Text = RebirthStatLabels[row];
            name.CustomMinimumSize = new Vector2(48, 0);
            line.AddChild(name);

            _rebirthBonusLbls[row] = UiTheme.Text("", 12, UiTheme.TextDim);
            _rebirthBonusLbls[row].CustomMinimumSize = new Vector2(64, 0);
            line.AddChild(_rebirthBonusLbls[row]);

            _rebirthRemoveBtns[row] = RebirthStepButton("-", () => EditRebirthPoint(index, false));
            line.AddChild(_rebirthRemoveBtns[row]);

            _rebirthPickLbls[row] = HudStyle.Label(13, HorizontalAlignment.Center);
            _rebirthPickLbls[row].CustomMinimumSize = new Vector2(28, 0);
            line.AddChild(_rebirthPickLbls[row]);

            _rebirthAddBtns[row] = RebirthStepButton("+", () => EditRebirthPoint(index, true));
            line.AddChild(_rebirthAddBtns[row]);
        }

        root.AddChild(new HSeparator());
        var actionRow = new HBoxContainer();
        actionRow.AddThemeConstantOverride("separation", 8);
        root.AddChild(actionRow);
        _rebirthBtn = new Button { Text = "Rebirth", FocusMode = Control.FocusModeEnum.None };
        _rebirthBtn.AddThemeFontSizeOverride("font_size", 13);
        _rebirthBtn.Pressed += OnRebirthPressed;
        actionRow.AddChild(_rebirthBtn);
        var cancel = new Button { Text = "Not yet", FocusMode = Control.FocusModeEnum.None };
        cancel.AddThemeFontSizeOverride("font_size", 13);
        cancel.Pressed += CloseRebirth;
        actionRow.AddChild(cancel);
        _rebirthStatus = HudStyle.Label(13, HorizontalAlignment.Right);
        _rebirthStatus.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        actionRow.AddChild(_rebirthStatus);
    }

    private static Button RebirthStepButton(string text, System.Action pressed)
    {
        var btn = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(30, 24),
            FocusMode = Control.FocusModeEnum.None,
        };
        HoldRepeat.Attach(btn, pressed);
        return btn;
    }

    private void OpenRebirthPicker()
    {
        if (!_rebirthInFlight)
        {
            _rebirthResetMode = false;
            _rebirthPick.Clear();
        }
        OpenRebirthPanel();
    }

    private void OpenRebirthReset()
    {
        if (Sheet.RebirthLevel <= 0) return;
        if (!_rebirthInFlight)
        {
            _rebirthResetMode = true;
            _rebirthPick.Reset(Sheet.RebirthLevel * RebirthPick.PointsPerRebirth);
        }
        OpenRebirthPanel();
    }

    private void OpenRebirthPanel()
    {
        if (!_rebirthInFlight) SetRebirthStatus("", false);
        RefreshRebirthUI();
        _rebirthPanel.Visible = true;
        _rebirthShown = true;
    }

    private void CloseRebirth()
    {
        if (!_rebirthShown) return;
        _rebirthShown = false;
        _rebirthPanel.Visible = false;
    }

    private void RefreshRebirthUI()
    {
        int level = Sheet.RebirthLevel;
        bool locked = _rebirthInFlight || _selfDead || (!_rebirthResetMode && !RebirthPick.Available(Sheet.Level, level));
        _rebirthLevelLbl.Text = _rebirthResetMode ? $"Rebirth Lv {level}" : $"Rebirth Lv {level}  →  Lv {level + 1}";
        _rebirthPointsLbl.Text = $"Place {_rebirthPick.Capacity} points  ({_rebirthPick.Remaining} left)";
        _rebirthBtn.Text = _rebirthResetMode ? "Redistribute" : "Rebirth";
        for (int row = 0; row < RebirthPick.StatCount; row++)
        {
            _rebirthBonusLbls[row].Text = $"now +{Sheet.RebirthBonusAtRow(row)}";
            int picked = _rebirthPick.PickedAt(row);
            _rebirthPickLbls[row].Text = picked > 0 ? $"+{picked}" : "";
            _rebirthAddBtns[row].Disabled = locked || !_rebirthPick.CanAdd(row);
            _rebirthRemoveBtns[row].Disabled = locked || !_rebirthPick.CanRemove(row);
        }
        _rebirthBtn.Disabled = locked || !_rebirthPick.Complete;
    }

    private void SetRebirthStatus(string text, bool warn)
    {
        _rebirthStatus.Text = text;
        _rebirthStatus.AddThemeColorOverride("font_color", warn ? UiTheme.Bad : Colors.White);
    }

    private void OnRebirthPressed()
    {
        if (!_rebirthShown || _rebirthInFlight || _selfDead
            || !RebirthPick.Available(Sheet.Level, Sheet.RebirthLevel)) return;
        if (!_rebirthPick.Complete)
        {
            SetRebirthStatus($"Place all {_rebirthPick.Capacity} points first.", true);
            return;
        }
        _rebirthInFlight = true;
        _rebirthSent = _rebirthPick.Payload();
        RefreshRebirthUI();
        if (_rebirthResetMode)
        {
            SetRebirthStatus("Redistributing…", false);
            Net.I.SendRebirthStatReset(_rebirthSent);
            return;
        }
        SetRebirthStatus("Reincarnating…", false);
        if (!Net.I.SendRebirthStatChange(_rebirthSent))
            OnRebirthStatResult(Net.ClassChangeRebirthStat, RebirthWire.Busy);
    }

    private void EditRebirthPoint(int row, bool add)
    {
        if (_rebirthInFlight) return;
        if (add) _rebirthPick.Add(row); else _rebirthPick.Remove(row);
        RefreshRebirthUI();
    }

    private void ResetRebirthPicker()
    {
        _rebirthInFlight = false;
        _rebirthSent = [];
        _rebirthPick.Clear();
        CloseRebirth();
    }

    private void OnRebirthStatResult(int sub, int code)
    {
        if (sub == Net.ClassChangeRebirthReset)
        {
            _rebirthInFlight = false;
            if (code == 1)
            {
                if (_rebirthResetMode) Sheet.ApplyRebirthReset(_rebirthSent);
                UpdateRebirthEmblem();
                CombatNotice("Rebirth bonus points redistributed.");
                CloseRebirth();
                return;
            }
            SetRebirthStatus("The rebirth bonus points were not changed.", true);
            if (_rebirthShown) RefreshRebirthUI();
            return;
        }
        if (!_rebirthInFlight) return;
        _rebirthInFlight = false;
        byte[] sent = _rebirthSent;
        _rebirthSent = [];
        short result = (short)code;
        if (result == RebirthWire.Accepted)
        {
            Sheet.ApplyRebirth(sent);
            UpdateRebirthEmblem();
            CombatNotice(ItemData.Text(RebirthWire.AcceptedText, $"Rebirth Lv {Sheet.RebirthLevel}"));
            CloseRebirth();
            return;
        }
        string refusal = ItemData.Text(RebirthWire.ResultText(result), "Mekin refused the rebirth.");
        SetRebirthStatus(refusal, true);
        if (_rebirthShown) RefreshRebirthUI();
        else CombatNotice(refusal);
    }
}
