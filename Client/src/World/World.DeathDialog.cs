using System.Collections.Generic;
using Godot;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const double RespawnRetrySeconds = 3.0;
    private const string RespawnHint = "Press OK to teleport back to the re-spawn point.";
    private const string RespawnSentHint = "Returning to the re-spawn point…";
    private const string RespawnRetryHint = "No answer from the server — press OK to try again.";
    private const string ScrollExplanation = "EXP recovery item exists in the inventory.";
    private const string ScrollConfirmHint = "Please confirm the availability.";
    private const int TextReturnToTown = 7653;
    private const int TextRespawnWith = 7652;
    private const int TextTownWithoutScroll = 42160;
    private const int TextAutoRespawn = 30610;
    private const double LifeCrystalCountdownSeconds = 15.0;
    private const string SpecialTypeKey = "Type";
    private const string ExpRecoverKey = "ExpRecover";
    private const float DeathPanelWidth = 286f;
    private const float DeathRowHeight = 25f;
    private const int DeathRowFont = 13;
    private const int DeathRowsMax = ResurrectionScrolls.MaxRows + 1;
    private const int TownRow = 0;

    private static readonly Color DeathRowPicked = new(1f, 1f, 0f);
    private static readonly Color DeathRowIdle = new(1f, 1f, 1f);

    private CanvasLayer _deathLayer = null!;
    private PanelContainer _deathPanel = null!;
    private Label _deathExpLbl = null!;
    private Label _deathHintLbl = null!;
    private VBoxContainer _deathRowBox = null!;
    private readonly List<Button> _deathRows = new();
    private Button _deathOkBtn = null!;
    private long _deathExpLost;
    private int _respawnToken;
    private bool _respawnPending;
    private List<ScrollSkill> _deathScrolls = new();
    private int _deathPick;
    private double _deathAutoRespawnAt = double.PositiveInfinity;

    private void DeathDialogInit()
    {
        BuildDeathDialog();
        Net.I.DeathExpLossEvent += OnDeathExpLoss;
    }

    private void DeathDialogDispose()
    {
        Net.I.DeathExpLossEvent -= OnDeathExpLoss;
    }

    private void BuildDeathDialog()
    {
        _deathLayer = new CanvasLayer { Layer = 90, Visible = false };
        AddChild(_deathLayer);

        var dim = new ColorRect { Color = new Color(0f, 0f, 0f, 0.45f) };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        dim.MouseFilter = Control.MouseFilterEnum.Stop;
        _deathLayer.AddChild(dim);

        _deathPanel = new PanelContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0.5f, AnchorBottom = 0.5f,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both,
        };
        _deathPanel.AddThemeStyleboxOverride("panel", UiTheme.WindowPanel());
        _deathLayer.AddChild(_deathPanel);

        var margin = new MarginContainer();
        UiTheme.Margins(margin, 8, 22, 8, 20);
        _deathPanel.AddChild(margin);

        var col = new VBoxContainer { CustomMinimumSize = new Vector2(DeathPanelWidth, 0) };
        col.AddThemeConstantOverride("separation", 8);
        margin.AddChild(col);

        _deathExpLbl = UiTheme.Text("", 14, UiTheme.Bad, HorizontalAlignment.Center);
        _deathExpLbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        col.AddChild(_deathExpLbl);

        _deathHintLbl = UiTheme.Text(RespawnHint, 13, UiTheme.TextLo, HorizontalAlignment.Center);
        _deathHintLbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        col.AddChild(_deathHintLbl);

        _deathRowBox = new VBoxContainer { Visible = false };
        _deathRowBox.AddThemeConstantOverride("separation", 6);
        col.AddChild(_deathRowBox);
        for (int i = 0; i < DeathRowsMax; i++)
        {
            int row = i;
            var button = new Button
            {
                FocusMode = Control.FocusModeEnum.None,
                Flat = true,
                Alignment = HorizontalAlignment.Left,
                CustomMinimumSize = new Vector2(DeathPanelWidth, DeathRowHeight),
            };
            button.AddThemeFontSizeOverride("font_size", DeathRowFont);
            button.GuiInput += e => OnDeathRowInput(row, e);
            _deathRows.Add(button);
            _deathRowBox.AddChild(button);
        }

        col.AddChild(new Control { CustomMinimumSize = new Vector2(0, 10) });

        _deathOkBtn = new Button
        {
            Text = "OK",
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(120, 30),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
        };
        _deathOkBtn.Pressed += ConfirmDeathChoice;
        col.AddChild(_deathOkBtn);
    }

    private void OnDeathExpLoss(long lost)
    {
        _deathExpLost = lost;
        RefreshDeathDialogText();
    }

    private bool DeathOffersScrolls => _deathScrolls.Count > 0;

    private void RefreshDeathDialogText()
    {
        if (DeathOffersScrolls)
        {
            _deathExpLbl.Text = ScrollExplanation;
            return;
        }
        _deathExpLbl.Text = _deathExpLost > 0
            ? $"Lost {_deathExpLost:n0} experience points."
            : "You have been defeated.";
    }

    private void ShowDeathDialog()
    {
        _deathScrolls = ResurrectionScrolls.Offer(CarriedScrollSkills(), _zone, Net.I.Sheet.Level);
        int best = ResurrectionScrolls.BestIndex(_deathScrolls);
        _deathPick = best >= 0 ? best + 1 : TownRow;
        _deathAutoRespawnAt = DeathOffersScrolls && _zone == ResurrectionScrolls.LifeCrystalZone
            ? Now() + LifeCrystalCountdownSeconds
            : double.PositiveInfinity;
        RefreshDeathRows();
        RefreshDeathDialogText();
        ArmRespawnButton(DeathOffersScrolls ? ScrollConfirmHint : RespawnHint);
        _deathLayer.Visible = true;
    }

    private IEnumerable<ScrollSkill> CarriedScrollSkills()
    {
        foreach (int abs in ScrollSearchSlots())
        {
            if (abs >= Inv.Length || Inv[abs].IsEmpty) continue;
            if (ItemData.Get(Inv[abs].ItemId) is not { Effect1: not 0 } item || SkillData.Get(item.Effect1) is not { } s) continue;
            yield return new ScrollSkill(s.Id, s.Name, s.Moral, EffectInt(s, SpecialTypeKey), EffectInt(s, ExpRecoverKey));
        }
    }

    private static IEnumerable<int> ScrollSearchSlots()
    {
        for (int i = 0; i < InventoryConstants.HaveMax; i++) yield return InventoryConstants.InventoryStart + i;
        for (int i = 0; i < InventoryConstants.MagicBagTotal; i++) yield return InventoryConstants.MagicBagStart + i;
    }

    private static int EffectInt(SkillData.Skill s, string key) =>
        s.Effect.TryGetValue(key, out var value) ? value.AsInt32() : 0;

    private void RefreshDeathRows()
    {
        _deathRowBox.Visible = DeathOffersScrolls;
        for (int i = 0; i < _deathRows.Count; i++)
        {
            var button = _deathRows[i];
            button.Visible = DeathOffersScrolls && i <= _deathScrolls.Count;
            if (!button.Visible) continue;
            button.Text = i == TownRow
                ? SystemText(TextReturnToTown, "Returning to Town or a designated re-spawn point.")
                : SystemText(TextRespawnWith, "Re-spawning with %s.", _deathScrolls[i - 1].Name);
            var colour = i == _deathPick ? DeathRowPicked : DeathRowIdle;
            button.AddThemeColorOverride("font_color", colour);
            button.AddThemeColorOverride("font_hover_color", colour);
            button.AddThemeColorOverride("font_pressed_color", colour);
        }
    }

    private void OnDeathRowInput(int row, InputEvent e)
    {
        if (e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click) return;
        _deathPick = row;
        RefreshDeathRows();
        if (click.DoubleClick) ConfirmDeathChoice();
    }

    private void DeathDialogTick()
    {
        if (!_deathLayer.Visible || double.IsPositiveInfinity(_deathAutoRespawnAt)) return;
        double left = _deathAutoRespawnAt - Now();
        _deathHintLbl.Text = SystemText(TextAutoRespawn, "You will be resurrected in the village %0.0f seconds after.")
            .Replace("%0.0f", Mathf.Max(0, Mathf.CeilToInt((float)left)).ToString());
        if (left > 0) return;
        _deathAutoRespawnAt = double.PositiveInfinity;
        ConfirmRespawn();
    }

    private void HideDeathDialog()
    {
        _deathExpLost = 0;
        _respawnPending = false;
        _respawnToken++;
        _deathScrolls.Clear();
        _deathAutoRespawnAt = double.PositiveInfinity;
        _deathLayer.Visible = false;
    }

    private void ArmRespawnButton(string hint)
    {
        _respawnPending = false;
        _deathOkBtn.Disabled = false;
        _deathHintLbl.Text = hint;
    }

    private void MarkRespawnPending()
    {
        _respawnPending = true;
        _deathOkBtn.Disabled = true;
        _deathHintLbl.Text = RespawnSentHint;
    }

    private void ConfirmDeathChoice()
    {
        if (!_selfDead) { HideDeathDialog(); return; }
        if (_respawnPending) return;
        if (!DeathOffersScrolls)
        {
            ConfirmRespawn();
            return;
        }
        if (_deathPick == TownRow)
        {
            Notice.Confirm(this, SystemText(TextTownWithoutScroll, "Do you wish to respawn at the town without using resurrection scroll?"),
                "Yes", "No", ConfirmRespawn);
            return;
        }
        UseResurrectionScroll(_deathScrolls[_deathPick - 1]);
    }

    private void UseResurrectionScroll(ScrollSkill scroll)
    {
        MarkRespawnPending();
        int token = ++_respawnToken;
        Net.I.SendMagic(MagicSub.Effecting, scroll.SkillId, Net.I.MyCharId);
        GetTree().CreateTimer(RespawnRetrySeconds).Timeout += () =>
        {
            if (token != _respawnToken || !_selfDead) return;
            ArmRespawnButton(RespawnRetryHint);
        };
    }

    private void ConfirmRespawn()
    {
        if (!_selfDead) { HideDeathDialog(); return; }
        if (_respawnPending) return;

        MarkRespawnPending();

        int token = ++_respawnToken;
        Net.I.SendRegene();

        GetTree().CreateTimer(RespawnRetrySeconds).Timeout += () =>
        {
            if (token != _respawnToken || !_selfDead) return;
            ArmRespawnButton(RespawnRetryHint);
        };
    }
}
