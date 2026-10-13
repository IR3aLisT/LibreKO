using Godot;

namespace LibreKO;

public sealed partial class WarScoreStrip : PanelContainer
{
    public static readonly Color KarusColour = new("d05a4a");
    public static readonly Color ElmoradColour = new("4a82d0");

    private const float StripWidth = 283f;
    private const float CentreWidth = 64f;
    private const float BarHeight = 4f;
    private const float EmptyShare = 1f;
    private const float SidePadding = 12f;
    private const float EdgePadding = 5f;
    private const float CloseSize = 18f;
    private const int CloseIconWidth = 10;

    private readonly string _leftName;
    private readonly string _rightName;
    private readonly Label _left;
    private readonly Label _right;
    private readonly Label _centre;
    private readonly Label? _status;
    private readonly Label? _clock;
    private readonly Button? _ranking;
    private readonly ColorRect _leftBar;
    private readonly ColorRect _rightBar;

    public event System.Action? RankingPressed;

    public Button? CloseButton { get; }

    public WarScoreStrip(bool withStatus)
        : this(withStatus, "Karus", KarusColour, "El Morad", ElmoradColour, closable: false)
    {
    }

    public WarScoreStrip(bool withStatus, string leftName, Color leftColour, string rightName, Color rightColour, bool closable)
    {
        _leftName = leftName;
        _rightName = rightName;
        CustomMinimumSize = new Vector2(StripWidth, 0);
        var style = UiTheme.Panel(6, true);
        style.ContentMarginLeft = style.ContentMarginRight = SidePadding;
        style.ContentMarginTop = style.ContentMarginBottom = EdgePadding;
        AddThemeStyleboxOverride("panel", style);

        var rows = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        rows.AddThemeConstantOverride("separation", 3);
        AddChild(rows);

        var scores = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        scores.AddThemeConstantOverride("separation", 6);
        rows.AddChild(scores);
        _left = Side(scores, leftColour, HorizontalAlignment.Left);
        _centre = UiTheme.Text("", 14, UiTheme.GoldBright, HorizontalAlignment.Center);
        _centre.CustomMinimumSize = new Vector2(CentreWidth, 0);
        _centre.AddThemeConstantOverride("outline_size", 3);
        _centre.MouseFilter = MouseFilterEnum.Ignore;
        scores.AddChild(_centre);
        _right = Side(scores, rightColour, HorizontalAlignment.Right);
        if (closable)
        {
            var close = UiTheme.IconButton(UiIcons.Get("system/close"), "Close");
            close.CustomMinimumSize = new Vector2(CloseSize, CloseSize);
            close.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            close.AddThemeConstantOverride("icon_max_width", CloseIconWidth);
            close.AddThemeColorOverride("icon_normal_color", new Color(UiTheme.TextHi, 0.7f));
            close.AddThemeColorOverride("icon_hover_color", UiTheme.TextHi);
            scores.AddChild(close);
            CloseButton = close;
        }

        var bar = new HBoxContainer { CustomMinimumSize = new Vector2(0, BarHeight), MouseFilter = MouseFilterEnum.Ignore };
        bar.AddThemeConstantOverride("separation", 0);
        rows.AddChild(bar);
        _leftBar = BarPart(bar, leftColour);
        _rightBar = BarPart(bar, rightColour);

        if (withStatus)
        {
            _status = UiTheme.Text("", 12, UiTheme.TextHi, HorizontalAlignment.Center);
            _status.AddThemeConstantOverride("outline_size", 1);
            _status.MouseFilter = MouseFilterEnum.Ignore;
            rows.AddChild(_status);

            var foot = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            foot.AddThemeConstantOverride("separation", 6);
            rows.AddChild(foot);
            _clock = UiTheme.Text("", 12, UiTheme.TextLo);
            _clock.AddThemeConstantOverride("outline_size", 1);
            _clock.MouseFilter = MouseFilterEnum.Ignore;
            _clock.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _clock.VerticalAlignment = VerticalAlignment.Center;
            foot.AddChild(_clock);
            _ranking = new Button { Text = "Ranking", FocusMode = FocusModeEnum.None, Visible = false };
            _ranking.AddThemeFontSizeOverride("font_size", 11);
            _ranking.Pressed += () => RankingPressed?.Invoke();
            foot.AddChild(_ranking);
        }
        SetScores(0, 0);
    }

    public bool RankingShown { set { if (_ranking != null) _ranking.Visible = value; } }

    public void SetClock(string text)
    {
        if (_clock == null) return;
        _clock.Text = text;
        _clock.GetParent<Control>().Visible = text.Length > 0 || _ranking is { Visible: true };
    }

    public string Centre { set => _centre.Text = value; }

    public void SetScores(int left, int right)
    {
        _left.Text = $"{_leftName}  {left}";
        _right.Text = $"{right}  {_rightName}";
        bool empty = left <= 0 && right <= 0;
        _leftBar.SizeFlagsStretchRatio = empty ? EmptyShare : Mathf.Max(0, left);
        _rightBar.SizeFlagsStretchRatio = empty ? EmptyShare : Mathf.Max(0, right);
    }

    public void SetStatus(string text, Color colour)
    {
        if (_status == null) return;
        _status.Text = text;
        _status.AddThemeColorOverride("font_color", colour);
    }

    private static Label Side(HBoxContainer row, Color colour, HorizontalAlignment align)
    {
        var label = UiTheme.Text("", 14, colour, align);
        label.AddThemeConstantOverride("outline_size", 2);
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        label.MouseFilter = MouseFilterEnum.Ignore;
        row.AddChild(label);
        return label;
    }

    private static ColorRect BarPart(HBoxContainer bar, Color colour)
    {
        var part = new ColorRect
        {
            Color = colour,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        bar.AddChild(part);
        return part;
    }
}
