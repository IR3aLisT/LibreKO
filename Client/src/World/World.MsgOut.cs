using Godot;

namespace LibreKO;

public partial class World
{
    private const int MsgOutFontSize = 28;
    private const float MsgOutHeightShare = 0.4f;
    private const float MsgOutStartScale = 1.5f;
    private const double MsgOutGrowSeconds = 0.25;
    private const double MsgOutSeconds = 3.0;
    private static readonly Color MsgOutColour = new(1f, 1f, 0f);

    private Label? _msgOut;
    private double _msgOutUntil;

    private void ShowSkillMsgOut(string? text)
    {
        if (string.IsNullOrEmpty(text) || Now() < _msgOutUntil) return;
        if (_msgOut == null || !IsInstanceValid(_msgOut))
        {
            var layer = new CanvasLayer { Layer = 70 };
            AddChild(layer);
            _msgOut = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            _msgOut.AddThemeFontSizeOverride("font_size", MsgOutFontSize);
            _msgOut.AddThemeColorOverride("font_color", MsgOutColour);
            _msgOut.AddThemeColorOverride("font_outline_color", Colors.Black);
            _msgOut.AddThemeConstantOverride("outline_size", 6);
            layer.AddChild(_msgOut);
        }

        var label = _msgOut;
        label.Text = text;
        label.ResetSize();
        var view = GetViewport().GetVisibleRect().Size;
        label.Position = new Vector2((view.X - label.Size.X) * 0.5f, view.Y * MsgOutHeightShare - label.Size.Y * 0.5f);
        label.PivotOffset = label.Size * 0.5f;
        label.Scale = Vector2.One * MsgOutStartScale;
        label.Visible = true;
        CreateTween().TweenProperty(label, "scale", Vector2.One, MsgOutGrowSeconds);

        _msgOutUntil = Now() + MsgOutSeconds;
        double until = _msgOutUntil;
        GetTree().CreateTimer(MsgOutSeconds).Timeout += () =>
        {
            if (IsInstanceValid(label) && _msgOutUntil <= until) label.Visible = false;
        };
    }
}
