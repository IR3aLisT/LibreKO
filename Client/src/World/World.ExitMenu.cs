using System;
using System.Collections.Generic;
using Godot;
using LibreKO.Domain;

namespace LibreKO;

public partial class World
{
    private enum ExitKind { None, CharacterSelect, Quit }

    private const int ExitCharacterSelectText = 44641;
    private const int ExitEndGameText = 44642;
    private const int ExitOptionText = 44643;
    private const int ExitCancelText = 44430;
    private const int ExitCountdownText = 7632;
    private const int ExitCanceledText = 7633;
    private const int ExitInBattleText = 7634;
    private const double BattleIdle = -1.0;
    private const int ExitAfterBattleSeconds = 10;
    private const int ExitMenuSkinLayer = 111;
    private const int EscTimedRowsShown = 3;
    private const float EscTimedRowHeight = 50f;
    private const int EscTimedRowGap = 4;
    private const float EscTimedIconSize = 40f;
    private static readonly Color ExitNoticeColour = new(1f, 0f, 0f);

    private Control _escCore = null!;
    private VBoxContainer _escTimed = null!, _escTimedList = null!;
    private CanvasLayer? _exitSkinLayer;
    private ExitKind _exitArmed;
    private double _battleTimer = BattleIdle;
    private int _exitShownSeconds = ExitAfterBattleSeconds;

    private readonly record struct ExitRow(string Key, string Caption, Action Run, string Tooltip);

    private void ExitMenuInit() => Net.I.AttackEvent += OnExitBattleAttack;

    private void ExitMenuDispose() => Net.I.AttackEvent -= OnExitBattleAttack;

    private List<ExitRow> ExitMenuRows() =>
    [
        new("character_select", SystemText(ExitCharacterSelectText, "Character Select"), () => RequestExit(ExitKind.CharacterSelect),
            "Return to character selection"),
        new("option", SystemText(ExitOptionText, "Option"), () => { ToggleEsc(false); SettingsPanel.Open(this); },
            "Open graphics and game settings"),
        new("end_game", SystemText(ExitEndGameText, "End Game"), () => RequestExit(ExitKind.Quit), "Close the game"),
        new("cancel", SystemText(ExitCancelText, "Cancel"), () => ToggleEsc(false), "Return to the world"),
    ];

    private VBoxContainer BuildEscTimedItems()
    {
        var box = new VBoxContainer { Visible = false };
        box.AddThemeConstantOverride("separation", 6);
        var notice = UiTheme.Text(SystemText(TimedItems.TitleText, "Show all temporary items in the inventory and Magic Bag"),
            12, UiTheme.TextHi, HorizontalAlignment.Center);
        notice.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(notice);
        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, EscTimedRowsShown * (EscTimedRowHeight + EscTimedRowGap) - EscTimedRowGap),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        _escTimedList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _escTimedList.AddThemeConstantOverride("separation", EscTimedRowGap);
        scroll.AddChild(_escTimedList);
        box.AddChild(scroll);
        box.AddChild(UiTheme.Rule());
        return box;
    }

    private List<ExitMenuEntry> ExitTimedEntries()
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var entries = new List<ExitMenuEntry>();
        foreach (var item in CollectTimedItems())
        {
            var left = TimedItems.Remaining(item.Slot.ExpiresAt, now);
            entries.Add(new ExitMenuEntry(item.Slot,
                ItemData.DisplayName(item.Slot.ItemId),
                TimedItems.PlaceText(item.Place) is { } place ? SystemText(place, "") : "",
                SystemText(TimedItems.TextId(left.Unit), "%d").Replace("%d", left.Amount.ToString()),
                left.Urgent));
        }
        return entries;
    }

    private readonly record struct ExitMenuEntry(ItemSlot Slot, string Name, string Place, string TimeLeft, bool Urgent);

    private void RefreshExitMenu()
    {
        CloseExitMenuSkin();
        var entries = ExitTimedEntries();
        if (PluginHost.Ui.ExitMenuBuilder is { } build)
        {
            var rows = ExitMenuRows();
            var request = new LibreKO.Plugins.ExitMenuRequest(
                entries.Count > 0 ? SystemText(TimedItems.TitleText, "Show all temporary items in the inventory and Magic Bag") : "",
                entries.ConvertAll(e => new LibreKO.Plugins.ExitMenuItem(e.Slot.ItemId, ItemData.Icon(e.Slot.ItemId), e.Name, e.Place, e.TimeLeft, e.Urgent)),
                rows.ConvertAll(r => new LibreKO.Plugins.ExitMenuRow(r.Key, r.Caption, r.Run)),
                () => ToggleEsc(false));
            _escCore.Visible = false;
            _exitSkinLayer = new CanvasLayer { Layer = ExitMenuSkinLayer };
            AddChild(_exitSkinLayer);
            _exitSkinLayer.AddChild(build(request));
            return;
        }
        _escCore.Visible = true;
        foreach (var child in _escTimedList.GetChildren()) child.QueueFree();
        foreach (var entry in entries) _escTimedList.AddChild(EscTimedRow(entry));
        _escTimed.Visible = entries.Count > 0;
    }

    private Control EscTimedRow(ExitMenuEntry entry)
    {
        var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, EscTimedRowHeight) };
        row.AddThemeConstantOverride("separation", 10);
        var slot = new ItemSlotView(EscTimedIconSize) { Veiled = true, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        slot.Set(entry.Slot);
        slot.Hovered += view => ShowItemTooltip(-1, view.Item);
        slot.Unhovered += _ => HideItemTooltip();
        row.AddChild(slot);
        var text = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        text.AddThemeConstantOverride("separation", 0);
        var name = UiTheme.Text(entry.Name, 13, ItemGrade.Tint(entry.Slot.ItemId));
        name.ClipText = true;
        text.AddChild(name);
        text.AddChild(UiTheme.Text($"{entry.Place}  ·  {entry.TimeLeft}", 11, entry.Urgent ? TimedItemsUrgentColor : TimedItemsPlaceColor));
        row.AddChild(text);
        return row;
    }

    private void CloseExitMenuSkin()
    {
        if (_exitSkinLayer != null && IsInstanceValid(_exitSkinLayer)) _exitSkinLayer.QueueFree();
        _exitSkinLayer = null;
    }

    private void ExitNotice(string text) => CombatLogAdd(text, CombatLogKind.Status, ExitNoticeColour);

    private void RequestExit(ExitKind kind)
    {
        ToggleEsc(false);
        if (_battleTimer < 0)
        {
            RunExit(kind);
            return;
        }
        ExitNotice(SystemText(ExitInBattleText, "You cannot exit from the client during a battle."));
        _exitArmed = kind;
        _exitShownSeconds = ExitAfterBattleSeconds;
    }

    private void RunExit(ExitKind kind)
    {
        if (kind == ExitKind.Quit)
        {
            OnExitGame();
            return;
        }
        Net.I.ReturnToCharSelect();
        GetTree().ChangeSceneToFile("res://scenes/CharSelect.tscn");
    }

    private void OnExitBattleAttack(int attackType, int result, int attackerId, int targetId, bool chained)
    {
        if (attackerId != _myId && targetId != _myId) return;
        _battleTimer = 0;
        if (_exitArmed == ExitKind.None) return;
        _exitArmed = ExitKind.None;
        _exitShownSeconds = ExitAfterBattleSeconds;
        ExitNotice(SystemText(ExitCanceledText, "Exiting game canceled."));
    }

    private void ExitTick(double delta)
    {
        if (_battleTimer >= 0)
        {
            if (_battleTimer >= ExitAfterBattleSeconds) _battleTimer = BattleIdle;
            else _battleTimer += delta;
        }
        if (_exitArmed == ExitKind.None) return;
        int left = _battleTimer < 0 ? 0 : Math.Max(0, (int)(ExitAfterBattleSeconds - _battleTimer));
        if (_exitShownSeconds > left)
        {
            _exitShownSeconds = left;
            ExitNotice(TextTemplate.Fill(SystemText(ExitCountdownText, "Exiting game in %d seconds."), left));
        }
        if (_exitShownSeconds != 0) return;
        var kind = _exitArmed;
        _exitArmed = ExitKind.None;
        RunExit(kind);
    }
}
