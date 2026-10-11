using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const string RankerPaperKarus = "res://assets/ui/ranker/paper_karus.png";
    private const string RankerPaperElMorad = "res://assets/ui/ranker/paper_elmorad.png";
    private const int RankerNameFont = 14;
    private const int RankerTextFont = 12;
    private static readonly Vector2 RankerPaperSize = new(466, 499);
    private static readonly Rect2 RankerNameRect = new(69, 118, 304, 21);
    private static readonly Rect2 RankerTextRect = new(72, 166, 294, 228);
    private static readonly Rect2 RankerCountRect = new(296, 396, 70, 22);
    private static readonly Rect2 RankerEditRect = new(80, 421, 91, 28);
    private static readonly Rect2 RankerSendRect = new(174, 421, 91, 28);
    private static readonly Rect2 RankerCloseRect = new(271, 422, 91, 28);
    private static readonly Color RankerRefusalColour = new(1f, 0f, 0f);

    private byte _rankerType;
    private string _rankerName = "";
    private string _rankerText = "";
    private string _rankerPending = "";
    private double _rankerWriteReadyAt;
    private CanvasLayer? _rankerLayer;
    private Control _rankerPaper = null!;
    private TextureRect _rankerArt = null!;
    private Label _rankerNameLabel = null!;
    private Label _rankerTextLabel = null!;
    private Label _rankerCount = null!;
    private TextEdit _rankerEdit = null!;
    private Button _rankerEditBtn = null!;
    private Button _rankerSendBtn = null!;

    private void RankerMessageInit()
    {
        Net.I.RankerPaperEvent += OnRankerPaper;
        Net.I.RankerResultEvent += OnRankerResult;
    }

    private void RankerMessageDispose()
    {
        Net.I.RankerPaperEvent -= OnRankerPaper;
        Net.I.RankerResultEvent -= OnRankerResult;
        _rankerLayer = null;
    }

    private bool TryOpenRankerStatue(Ent e)
    {
        if (!e.Statue) return false;
        if (FlatDistance(_self.Position, e.Body.Position) > RankerMessage.Reach) return true;
        _rankerType = (byte)e.NpcType;
        Net.I.SendRankerView(_rankerType);
        return true;
    }

    private bool RankerIsMe => _rankerName.Length > 0 && _rankerName == (Net.I.LastEnter.Name ?? "");

    private void OnRankerPaper(string name, string message)
    {
        _rankerName = name;
        _rankerText = message;
        if (RankerIsMe) ShowRankerMenu();
        else ShowRankerPaper();
    }

    private void ShowRankerMenu()
    {
        BeginNpcDialog(ItemData.Text(RankerMessage.TitleText, "[Ranker Statue]"),
            ItemData.Text(RankerMessage.MenuTipText, ""));
        AddNpcMenuButton($"1.   {ItemData.Text(RankerMessage.RedressText, "Ranker Statue Equipment Change (once per day)")}",
            () => { CloseNpcDialog(); Net.I.SendRankerRedress(_rankerType); });
        AddNpcMenuButton($"2.   {ItemData.Text(RankerMessage.WriteText, "Write Ranker Message")}",
            () => { CloseNpcDialog(); ShowRankerPaper(); });
        EndNpcDialog(2);
        ShowNpcDialog();
    }

    private void ShowRankerPaper()
    {
        if (_rankerLayer == null || !IsInstanceValid(_rankerLayer)) BuildRankerPaper();
        _rankerArt.Texture = GD.Load<Texture2D>(RankerMessage.Karus(_rankerType) ? RankerPaperKarus : RankerPaperElMorad);
        _rankerNameLabel.Text = _rankerName;
        _rankerTextLabel.Text = _rankerText;
        SetRankerEditing(false);
        if (GetViewport() is { } viewport)
            _rankerPaper.Position = (viewport.GetVisibleRect().Size - RankerPaperSize) / 2f;
        _rankerLayer!.Visible = true;
    }

    private void CloseRankerPaper()
    {
        if (_rankerLayer != null && IsInstanceValid(_rankerLayer)) _rankerLayer.Visible = false;
    }

    private void SetRankerEditing(bool editing)
    {
        _rankerTextLabel.Visible = !editing;
        _rankerEdit.Visible = editing;
        _rankerCount.Visible = editing;
        _rankerSendBtn.Visible = editing;
        _rankerEditBtn.Visible = !editing;
        if (!editing) return;
        _rankerEdit.Text = _rankerText;
        RefreshRankerCount();
        _rankerEdit.GrabFocus();
    }

    private void OnRankerEditPressed()
    {
        if (!RankerIsMe)
        {
            Notice.Show(this, ItemData.Text(RankerMessage.NotYoursText, ""));
            return;
        }
        SetRankerEditing(true);
    }

    private void OnRankerSendPressed()
    {
        if (Now() < _rankerWriteReadyAt)
        {
            Notice.Show(this, ItemData.Text(RankerMessage.CooldownText, ""), ItemData.Text(RankerMessage.SavedTitleText, "Notice"));
            return;
        }
        _rankerPending = _rankerEdit.Text.Trim();
        _rankerWriteReadyAt = Now() + RankerMessage.WriteCooldownSeconds;
        Net.I.SendRankerWrite(_rankerType, _rankerPending);
    }

    private void RefreshRankerCount()
    {
        if (_rankerEdit.Text.Length > RankerMessage.MessageMaxLength)
        {
            int caret = Mathf.Min(_rankerEdit.GetCaretColumn(), RankerMessage.MessageMaxLength);
            _rankerEdit.Text = _rankerEdit.Text[..RankerMessage.MessageMaxLength];
            _rankerEdit.SetCaretColumn(caret);
        }
        _rankerCount.Text = $"({RankerMessage.MessageMaxLength - _rankerEdit.Text.Length})";
    }

    private void OnRankerResult(byte sub, short result)
    {
        switch (sub)
        {
            case RankerMessage.SubView:
                RankerRefusal(RankerMessage.ViewTextId(result));
                break;
            case RankerMessage.SubWrite when result == RankerMessage.Done:
                _rankerText = _rankerPending;
                if (_rankerLayer != null && IsInstanceValid(_rankerLayer) && _rankerLayer.Visible)
                {
                    _rankerTextLabel.Text = _rankerText;
                    SetRankerEditing(false);
                }
                Notice.Show(this, ItemData.Text(RankerMessage.WriteTextId(result), ""),
                    ItemData.Text(RankerMessage.SavedTitleText, "Notice"));
                break;
            case RankerMessage.SubWrite:
                RankerRefusal(RankerMessage.WriteTextId(result));
                break;
            case RankerMessage.SubRedress:
                if (ItemData.Text(RankerMessage.RedressTextId(result), "") is { Length: > 0 } text)
                    Notice.Show(this, text);
                break;
        }
    }

    private void RankerRefusal(int textId)
    {
        if (ItemData.Text(textId, "") is { Length: > 0 } text)
            CombatLogAdd(text, CombatLogKind.Status, RankerRefusalColour);
    }

    private void BuildRankerPaper()
    {
        _rankerLayer = new CanvasLayer { Layer = 74, Visible = false };
        AddChild(_rankerLayer);
        _rankerPaper = new Control { Size = RankerPaperSize, MouseFilter = Control.MouseFilterEnum.Stop };
        _rankerLayer.AddChild(_rankerPaper);

        _rankerArt = new TextureRect
        {
            Size = RankerPaperSize,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _rankerPaper.AddChild(_rankerArt);

        _rankerNameLabel = RankerLabel(RankerNameRect, RankerNameFont, HorizontalAlignment.Center);
        _rankerNameLabel.AddThemeColorOverride("font_color", UiTheme.GoldBright);
        _rankerTextLabel = RankerLabel(RankerTextRect, RankerTextFont, HorizontalAlignment.Left);
        _rankerTextLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _rankerTextLabel.VerticalAlignment = VerticalAlignment.Top;
        _rankerCount = RankerLabel(RankerCountRect, RankerTextFont, HorizontalAlignment.Right);

        _rankerEdit = new TextEdit
        {
            Position = RankerTextRect.Position,
            Size = RankerTextRect.Size,
            WrapMode = TextEdit.LineWrappingMode.Boundary,
            Visible = false,
        };
        _rankerEdit.AddThemeFontSizeOverride("font_size", RankerTextFont);
        _rankerEdit.TextChanged += RefreshRankerCount;
        _rankerPaper.AddChild(_rankerEdit);

        _rankerEditBtn = RankerButton(RankerEditRect, "Edit", OnRankerEditPressed);
        _rankerSendBtn = RankerButton(RankerSendRect, "Register", OnRankerSendPressed);
        RankerButton(RankerCloseRect, "Close", CloseRankerPaper);
        HudLayout.Attach(_rankerPaper, "ranker_paper", _rankerArt, null, persist: false);
    }

    private Label RankerLabel(Rect2 rect, int fontSize, HorizontalAlignment align)
    {
        var label = new Label
        {
            Position = rect.Position,
            Size = rect.Size,
            HorizontalAlignment = align,
            ClipText = true,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", UiTheme.TextHi);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 3);
        _rankerPaper.AddChild(label);
        return label;
    }

    private Button RankerButton(Rect2 rect, string text, System.Action pressed)
    {
        var button = Ui.MenuButton(text, height: (int)rect.Size.Y, fontSize: 11);
        button.Position = rect.Position;
        button.Size = rect.Size;
        button.Pressed += pressed;
        _rankerPaper.AddChild(button);
        return button;
    }
}
