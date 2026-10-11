using System.Collections.Generic;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const int TextHandoverNoAuthority = 15718;
    private const int TextHandoverNoVice = 15719;
    private const int TextHandoverInfo = 15720;
    private const int TextHandoverDone = 15722;
    private const int TextHandoverFailed = 15723;
    private const int TextHandoverRefused = 15726;
    private const byte HandoverListOk = 1;
    private const byte HandoverListNoAuthority = 4;
    private const int HandoverChoices = 3;
    private const double HandoverResendSeconds = 5.0;
    private const float HandoverInfoWidth = 260f;

    private HudWindow? _handoverWindow;
    private Label _handoverInfo = null!;
    private readonly Button[] _handoverChoices = new Button[HandoverChoices];
    private Button _handoverConfirm = null!;
    private string _handoverPreselect = "";
    private double _handoverSentAt = double.NegativeInfinity;

    private void RequestClanHandover(string preselect)
    {
        _handoverPreselect = preselect;
        Net.I.SendClanHandoverList();
    }

    private void BuildHandoverWindow()
    {
        _handoverWindow = new HudWindow("clanhandover", "Leader Transfer") { Visible = false };
        var layer = new CanvasLayer { Layer = 78 };
        AddChild(layer);
        layer.AddChild(_handoverWindow);
        var root = _handoverWindow.Body;
        root.AddThemeConstantOverride("separation", 8);
        _handoverInfo = UiTheme.Text("", 12, UiTheme.TextHi);
        _handoverInfo.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _handoverInfo.CustomMinimumSize = new Vector2(HandoverInfoWidth, 0);
        root.AddChild(_handoverInfo);
        var group = new ButtonGroup();
        for (int i = 0; i < HandoverChoices; i++)
        {
            var choice = new Button { ToggleMode = true, ButtonGroup = group, FocusMode = Control.FocusModeEnum.None };
            choice.AddThemeStyleboxOverride("pressed", HandoverPickedStyle());
            choice.AddThemeColorOverride("font_pressed_color", UiTheme.GoldBright);
            choice.Toggled += _ => RefreshHandoverConfirm();
            _handoverChoices[i] = choice;
            root.AddChild(choice);
        }
        var footer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        footer.AddThemeConstantOverride("separation", 8);
        root.AddChild(footer);
        _handoverConfirm = UiTheme.SmallButton("Confirm", "Hand the clan over to the chosen vice-chief");
        _handoverConfirm.Pressed += ConfirmHandover;
        footer.AddChild(_handoverConfirm);
        var close = UiTheme.SmallButton("Close", "");
        close.Pressed += () => _handoverWindow!.Visible = false;
        footer.AddChild(close);
    }

    private void ShowHandoverWindow(byte state, List<string> names)
    {
        if (state == HandoverListNoAuthority)
        {
            CombatNotice(SystemText(TextHandoverNoAuthority, "You don't have the authority to change leadership."));
            return;
        }
        if (state != HandoverListOk)
        {
            CombatNotice(SystemText(TextHandoverRefused, "You can't change leadership."));
            return;
        }
        if (_handoverWindow == null) BuildHandoverWindow();
        _handoverInfo.Text = names.Count == 0
            ? SystemText(TextHandoverNoVice, "There is no Co-leader authorized to change leadership.")
            : TextTemplate.Fill(SystemText(TextHandoverInfo,
                "%s, the leader of %s, refuses to be the leader of the clan and passes  leadership to the next clan member."),
                Net.I.LastEnter.Name, MyClan.Name);
        for (int i = 0; i < HandoverChoices; i++)
        {
            bool shown = i < names.Count;
            _handoverChoices[i].Visible = shown;
            _handoverChoices[i].Text = shown ? names[i] : "";
            _handoverChoices[i].SetPressedNoSignal(shown && (names.Count == 1
                || string.Equals(names[i], _handoverPreselect, System.StringComparison.OrdinalIgnoreCase)));
        }
        _handoverPreselect = "";
        RefreshHandoverConfirm();
        _handoverWindow!.Visible = true;
        _handoverWindow.ResetSize();
    }

    private static StyleBoxFlat HandoverPickedStyle()
    {
        var style = new StyleBoxFlat { BgColor = new Color(UiTheme.Gold, 0.18f), BorderColor = UiTheme.GoldBright };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(3);
        return style;
    }

    private string? HandoverChoice()
    {
        foreach (var choice in _handoverChoices)
            if (choice.Visible && choice.ButtonPressed) return choice.Text;
        return null;
    }

    private void RefreshHandoverConfirm() => _handoverConfirm.Disabled = HandoverChoice() == null;

    private void ConfirmHandover()
    {
        if (HandoverChoice() is not { } heir || Now() - _handoverSentAt < HandoverResendSeconds) return;
        _handoverSentAt = Now();
        Net.I.SendClanHandover(heir);
        _handoverWindow!.Visible = false;
    }

    private string HandoverDoneText(string oldChief, string newChief) =>
        string.Equals(oldChief, Net.I.LastEnter.Name, System.StringComparison.OrdinalIgnoreCase)
            ? TextTemplate.Fill(SystemText(TextHandoverDone, "You have handed over leadership to %s."), newChief)
            : $"{oldChief}, the leader of {MyClan.Name}, passes leadership to {newChief}.";

    private string HandoverFailedText => SystemText(TextHandoverFailed, "You failed to pass on the leadership.");
}
