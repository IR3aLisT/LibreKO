using System;
using System.Collections.Generic;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const string TimedItemsButtonTip = "Open Temporary Item List.";
    private const string TimedItemsTitle = "Temporary Item List";
    private const double TimedItemsRefreshSeconds = 30.0;
    private static readonly Vector2 TimedItemsOrigin = new(50, 76);
    private static readonly Vector2 TimedItemsCanvas = new(456, 424);
    private static readonly Color TimedItemsUrgentColor = new("dc143c");
    private static readonly Color TimedItemsPlaceColor = new("c0c0c0");
    private static readonly Color TimedItemsNoticeColor = new("ffffff");
    private static readonly int[] TimedItemsRowTops = { 119, 230, 341 };
    private const int TimedItemsRowHeight = 103;

    private CanvasLayer _tiLayer = null!;
    private HudWindow? _tiWindow;
    private Button _tiIcon = null!;
    private TextureRect _tiIconImage = null!;
    private readonly List<TimedItem> _tiEntries = new();
    private readonly PanelContainer[] _tiRows = new PanelContainer[TimedItems.RowsPerPage];
    private readonly ItemSlotView[] _tiSlots = new ItemSlotView[TimedItems.RowsPerPage];
    private readonly Label[] _tiNames = new Label[TimedItems.RowsPerPage];
    private readonly Label[] _tiPlaces = new Label[TimedItems.RowsPerPage];
    private readonly Label[] _tiTimes = new Label[TimedItems.RowsPerPage];
    private VScrollBar _tiScroll = null!;
    private CheckBox _tiHideToday = null!;
    private StyleBoxFlat _tiRowNormal = null!, _tiRowUrgent = null!;
    private double _tiRefreshIn;

    private void TimedItemsInit()
    {
        _tiLayer = new CanvasLayer { Layer = 63 };
        AddChild(_tiLayer);
        _tiIcon = TopIconButton(_topIconLayer, "system/clock", TimedItemsButtonTip, ToggleTimedItems, out _tiIconImage);
        _tiIcon.Visible = false;
        _tiIcon.Resized += PlaceTimedItemsIcon;
        _tiIcon.VisibilityChanged += PlaceTimedItemsIcon;
        Callable.From(OfferTimedItems).CallDeferred();
    }

    private void PlaceTimedItemsIcon() => PlaceTopIcon(_tiIcon, _tiIconImage, HudPlacement.TimedItemsIcon);

    private void OfferTimedItems()
    {
        RefreshTimedItemsIcon();
        if (Net.I.TimedItemsOffered) return;
        Net.I.TimedItemsOffered = true;
        if (TimedItems.HiddenToday(Config.TimedItemsHiddenDay, DateTime.Now)) return;
        QueueEntryPopup(EntryPopup.TimedItems, () =>
        {
            OpenTimedItems();
            return _tiWindow;
        });
    }

    private void RefreshTimedItemsIcon()
    {
        if (!IsInstanceValid(_tiIcon)) return;
        _tiIcon.Visible = CollectTimedItems().Count > 0;
    }

    private List<TimedItem> CollectTimedItems() => TimedItems.Collect(Inv.Length, abs => Inv[abs], Net.I.Pet?.Items);

    private void ToggleTimedItems()
    {
        if (_tiWindow is { Visible: true })
        {
            _tiWindow.Visible = false;
            return;
        }
        OpenTimedItems();
    }

    private void OpenTimedItems()
    {
        _tiEntries.Clear();
        _tiEntries.AddRange(CollectTimedItems());
        if (_tiEntries.Count == 0) return;
        _tiWindow ??= BuildTimedItems();
        _tiHideToday.SetPressedNoSignal(TimedItems.HiddenToday(Config.TimedItemsHiddenDay, DateTime.Now));
        _tiScroll.MaxValue = TimedItems.PageCount(_tiEntries.Count);
        _tiScroll.Visible = _tiEntries.Count > TimedItems.RowsPerPage;
        _tiScroll.SetValueNoSignal(0);
        RenderTimedItems();
        ShowOnTop(_tiWindow);
    }

    private HudWindow BuildTimedItems()
    {
        _tiRowNormal = UiTheme.Panel();
        _tiRowUrgent = UiTheme.Panel();
        _tiRowUrgent.BgColor = new Color(TimedItemsUrgentColor, 0.18f);
        _tiRowUrgent.BorderColor = new Color(TimedItemsUrgentColor, 0.6f);

        var window = new HudWindow("timed_items", TimedItemsTitle, new Vector2(320, 110)) { Visible = false };
        window.Closed += () => window.Visible = false;
        _tiLayer.AddChild(window);
        var canvas = new Control { CustomMinimumSize = TimedItemsCanvas, MouseFilter = Control.MouseFilterEnum.Pass };
        window.Body.AddChild(canvas);

        var notice = UiTheme.Text(SystemText(TimedItems.TitleText, "Show all temporary items in the inventory and Magic Bag"),
            12, TimedItemsNoticeColor, HorizontalAlignment.Center);
        notice.VerticalAlignment = VerticalAlignment.Center;
        TimedItemsPlace(canvas, notice, new Rect2(56, 82, 442, 34));

        for (int i = 0; i < TimedItems.RowsPerPage; i++)
        {
            int top = TimedItemsRowTops[i];
            var row = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
            row.AddThemeStyleboxOverride("panel", _tiRowNormal);
            row.GuiInput += OnTimedItemsWheel;
            TimedItemsPlace(canvas, row, new Rect2(55, top, 417, TimedItemsRowHeight));
            _tiRows[i] = row;

            var slot = new ItemSlotView(45) { Veiled = true };
            slot.Hovered += view => ShowItemTooltip(-1, view.Item);
            slot.Unhovered += _ => HideItemTooltip();
            TimedItemsPlace(canvas, slot, new Rect2(84, top + 29, 45, 45));
            _tiSlots[i] = slot;

            _tiNames[i] = TimedItemsLabel(canvas, new Rect2(164, top + 9, 300, 29), 13, UiTheme.TextHi);
            _tiPlaces[i] = TimedItemsLabel(canvas, new Rect2(164, top + 38, 300, 28), 11, TimedItemsPlaceColor);
            _tiTimes[i] = TimedItemsLabel(canvas, new Rect2(164, top + 66, 300, 27), 13, UiTheme.TextHi);
        }

        _tiScroll = new VScrollBar { MinValue = 0, Step = 1, Page = 1, MouseFilter = Control.MouseFilterEnum.Stop };
        _tiScroll.ValueChanged += _ => RenderTimedItems();
        TimedItemsPlace(canvas, _tiScroll, new Rect2(479, 119, 18, 325));

        var hideLabel = UiTheme.Text(SystemText(TimedItems.HideTodayText, "Do not show today."), 12, TimedItemsNoticeColor, HorizontalAlignment.Right);
        hideLabel.VerticalAlignment = VerticalAlignment.Center;
        TimedItemsPlace(canvas, hideLabel, new Rect2(56, 457, 392, 38));
        _tiHideToday = UiTheme.FlatCheck("", false);
        _tiHideToday.Toggled += OnTimedItemsHideToday;
        TimedItemsPlace(canvas, _tiHideToday, new Rect2(457, 459, 36, 33));
        return window;
    }

    private static Label TimedItemsLabel(Control canvas, Rect2 reference, int size, Color colour)
    {
        var label = UiTheme.Text("", size, colour);
        label.VerticalAlignment = VerticalAlignment.Center;
        label.ClipText = true;
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        TimedItemsPlace(canvas, label, reference);
        return label;
    }

    private static void TimedItemsPlace(Control canvas, Control child, Rect2 reference)
    {
        canvas.AddChild(child);
        child.Position = reference.Position - TimedItemsOrigin;
        child.Size = reference.Size;
        child.CustomMinimumSize = reference.Size;
    }

    private void OnTimedItemsWheel(InputEvent e)
    {
        if (e is not InputEventMouseButton { Pressed: true } wheel) return;
        if (wheel.ButtonIndex == MouseButton.WheelUp) _tiScroll.Value -= 1;
        else if (wheel.ButtonIndex == MouseButton.WheelDown) _tiScroll.Value += 1;
    }

    private void OnTimedItemsHideToday(bool on)
    {
        Config.SetTimedItemsHiddenDay(on ? TimedItems.DayKey(DateTime.Now) : 0);
        if (on && _tiWindow != null) _tiWindow.Visible = false;
    }

    private void RenderTimedItems()
    {
        if (_tiWindow == null) return;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        int first = (int)_tiScroll.Value * TimedItems.RowsPerPage;
        for (int i = 0; i < TimedItems.RowsPerPage; i++)
        {
            int index = first + i;
            bool shown = index < _tiEntries.Count;
            _tiRows[i].Visible = _tiSlots[i].Visible = _tiNames[i].Visible = _tiPlaces[i].Visible = _tiTimes[i].Visible = shown;
            if (!shown) continue;
            var entry = _tiEntries[index];
            _tiSlots[i].Set(entry.Slot);
            _tiNames[i].Text = ItemData.DisplayName(entry.Slot.ItemId);
            _tiNames[i].AddThemeColorOverride("font_color", ItemGrade.Tint(entry.Slot.ItemId));
            _tiPlaces[i].Text = TimedItems.PlaceText(entry.Place) is { } placeText ? SystemText(placeText, "") : "";
            var left = TimedItems.Remaining(entry.Slot.ExpiresAt, now);
            _tiTimes[i].Text = SystemText(TimedItems.TextId(left.Unit), "%d").Replace("%d", left.Amount.ToString());
            _tiTimes[i].AddThemeColorOverride("font_color", left.Urgent ? TimedItemsUrgentColor : UiTheme.TextHi);
            _tiRows[i].AddThemeStyleboxOverride("panel", left.Urgent ? _tiRowUrgent : _tiRowNormal);
        }
    }

    private void TimedItemsTick(double delta)
    {
        if (_tiWindow is not { Visible: true }) return;
        _tiRefreshIn -= delta;
        if (_tiRefreshIn > 0) return;
        _tiRefreshIn = TimedItemsRefreshSeconds;
        RenderTimedItems();
    }
}
