using System.Collections.Generic;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const int RoyaleBoardFontSize = 13;
    private const int RoyaleScoreFontSize = 12;
    private const float RoyalePingRadius = 6f;

    private CanvasLayer? _royaleLayer;
    private PanelContainer? _royaleBoard;
    private Control? _royaleBoardBody;
    private Label? _royaleBoardTitle, _royaleBoardTime, _royaleBoardCount;
    private Button? _royaleApply, _royaleCancel;
    private HudWindow? _royaleScore;
    private Label? _royaleSurvivors, _royaleNextGas, _royaleGasTime;
    private double _royaleBoardEndsAt;
    private bool _royaleBoardOpen, _royaleBoardCollapsed;
    private readonly List<(float X, float Z, double Until, bool Delegate)> _royalePings = new();
    private double _royaleReturnBoxAt = double.PositiveInfinity;

    private bool InRoyaleZone => KnightRoyale.IsZone(_zone);

    private void KnightRoyaleInit()
    {
        Net.I.RoyaleSignUpEvent += OnRoyaleSignUp;
        Net.I.RoyaleEntryEvent += OnRoyaleEntry;
        Net.I.RoyaleAbandonedEvent += OnRoyaleAbandoned;
        Net.I.RoyaleGasEvent += OnRoyaleGas;
        Net.I.RoyalePingEvent += OnRoyalePing;
        Net.I.RoyaleRewardEvent += OnRoyaleReward;
        Net.I.RoyaleWinnerEvent += OnRoyaleWinner;
        Net.I.RoyaleRestoreEvent += OnRoyaleRestore;
        if (Chat != null)
            Chat.SendRefusal = () => InRoyaleZone ? ItemData.Text(KnightRoyale.ChatBlockedText, "Cannot be used while in chat restriction status.") : null;
    }

    private void KnightRoyaleDispose()
    {
        Net.I.RoyaleSignUpEvent -= OnRoyaleSignUp;
        Net.I.RoyaleEntryEvent -= OnRoyaleEntry;
        Net.I.RoyaleAbandonedEvent -= OnRoyaleAbandoned;
        Net.I.RoyaleGasEvent -= OnRoyaleGas;
        Net.I.RoyalePingEvent -= OnRoyalePing;
        Net.I.RoyaleRewardEvent -= OnRoyaleReward;
        Net.I.RoyaleWinnerEvent -= OnRoyaleWinner;
        Net.I.RoyaleRestoreEvent -= OnRoyaleRestore;
    }

    private void OnRoyaleSignUp(RoyaleSignUp signUp)
    {
        switch (signUp.Result)
        {
            case KnightRoyale.SignUpOpen:
                OpenRoyaleBoard(signUp.Mode, signUp.Participants, signUp.Seconds);
                return;
            case KnightRoyale.SignUpCount:
                if (_royaleBoardCount != null) _royaleBoardCount.Text = signUp.Participants.ToString();
                if (_royaleBoardOpen && _royaleBoard is { Visible: false }) _royaleBoard.Visible = true;
                return;
        }
        int text = KnightRoyale.SignUpText(signUp.Result);
        if (text > 0) CombatNotice(ItemData.Text(text, ""), KnightRoyale.Yellow);
        SetRoyaleApplied(KnightRoyale.ShowsCancel(signUp.Result));
    }

    private void OpenRoyaleBoard(int mode, int participants, int seconds)
    {
        EnsureRoyaleBoard();
        _royaleBoardTitle!.Text = ItemData.Text(KnightRoyale.TitleFor(mode), "Knight Royale");
        _royaleBoardCount!.Text = participants.ToString();
        _royaleBoardEndsAt = Now() + seconds;
        _royaleBoardOpen = true;
        SetRoyaleApplied(false);
        _royaleBoard!.Visible = true;
        Audio.PlayUi(Sfx.InventoryOpen);
    }

    private void SetRoyaleApplied(bool applied)
    {
        if (_royaleApply == null || _royaleCancel == null) return;
        _royaleApply.Visible = !applied;
        _royaleCancel.Visible = applied;
    }

    private void EnsureRoyaleBoard()
    {
        if (_royaleBoard != null && IsInstanceValid(_royaleBoard)) return;
        EnsureRoyaleLayer();
        var anchor = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        anchor.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        _royaleLayer!.AddChild(anchor);
        _royaleBoard = new PanelContainer { Visible = false };
        _royaleBoard.AddThemeStyleboxOverride("panel", QuestToastStyle());
        anchor.AddChild(_royaleBoard);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 6);
        _royaleBoard.AddChild(column);

        var strip = new Button { Flat = true, FocusMode = Control.FocusModeEnum.None };
        strip.Pressed += () =>
        {
            _royaleBoardCollapsed = !_royaleBoardCollapsed;
            _royaleBoardBody!.Visible = !_royaleBoardCollapsed;
            _royaleBoard.ResetSize();
        };
        var head = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        head.AddThemeConstantOverride("separation", 24);
        head.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _royaleBoardTitle = UiTheme.Text("", RoyaleBoardFontSize, UiTheme.GoldBright);
        _royaleBoardTitle.MouseFilter = Control.MouseFilterEnum.Ignore;
        _royaleBoardTime = UiTheme.Text("", RoyaleBoardFontSize, UiTheme.TextHi);
        _royaleBoardTime.MouseFilter = Control.MouseFilterEnum.Ignore;
        head.AddChild(_royaleBoardTitle);
        head.AddChild(_royaleBoardTime);
        strip.CustomMinimumSize = new Vector2(280, 30);
        strip.AddChild(head);
        column.AddChild(strip);

        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 6);
        _royaleBoardBody = body;
        var count = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        count.AddThemeConstantOverride("separation", 20);
        count.AddChild(UiTheme.Text("Participants", RoyaleBoardFontSize, UiTheme.TextLo));
        _royaleBoardCount = UiTheme.Text("0", RoyaleBoardFontSize, UiTheme.Gold);
        count.AddChild(_royaleBoardCount);
        body.AddChild(count);
        _royaleApply = LookActionButton("Apply", () =>
        {
            SetRoyaleApplied(true);
            Net.I.SendRoyaleSignUp(apply: true);
        });
        _royaleCancel = LookActionButton("Cancel", () =>
        {
            SetRoyaleApplied(false);
            Net.I.SendRoyaleSignUp(apply: false);
        });
        body.AddChild(_royaleApply);
        body.AddChild(_royaleCancel);
        column.AddChild(body);
    }

    private void EnsureRoyaleLayer()
    {
        if (_royaleLayer != null && IsInstanceValid(_royaleLayer)) return;
        _royaleLayer = new CanvasLayer { Layer = 77 };
        AddChild(_royaleLayer);
    }

    private void EnsureRoyaleScore()
    {
        if (_royaleScore != null && IsInstanceValid(_royaleScore)) return;
        EnsureRoyaleLayer();
        _royaleScore = new HudWindow("royale_score", ItemData.Text(KnightRoyale.TitleText, "Knight Royale"), closable: false) { Visible = false };
        _royaleLayer!.AddChild(_royaleScore);
        _royaleSurvivors = UiTheme.Text("", RoyaleScoreFontSize, UiTheme.TextHi, HorizontalAlignment.Center);
        _royaleNextGas = UiTheme.Text("", RoyaleScoreFontSize, UiTheme.TextLo, HorizontalAlignment.Center);
        _royaleNextGas.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _royaleNextGas.CustomMinimumSize = new Vector2(140, 0);
        _royaleGasTime = UiTheme.Text("", RoyaleScoreFontSize + 2, UiTheme.GoldBright, HorizontalAlignment.Center);
        _royaleScore.Body.AddChild(_royaleSurvivors);
        _royaleScore.Body.AddChild(_royaleNextGas);
        _royaleScore.Body.AddChild(_royaleGasTime);
    }

    private void OnRoyaleEntry(RoyaleEntry entry)
    {
        _royaleBoardOpen = false;
        if (_royaleBoard != null && IsInstanceValid(_royaleBoard)) _royaleBoard.Visible = false;
        EnsureRoyaleScore();
        _royaleScore!.Visible = true;
    }

    private void OnRoyaleAbandoned(string name, int survivors)
    {
        if (name.Length > 0)
            CombatNotice(TextTemplate.Fill(ItemData.Text(KnightRoyale.AbandonedText, "%s has abandoned the Battlegrounds"), name), KnightRoyale.Yellow);
    }

    private void OnRoyaleGas(int released, int seconds)
    {
        if (released != 0)
            CombatNotice(TextTemplate.Fill(ItemData.Text(KnightRoyale.GasReleasedText, "Poison Gas has been released in area %d"), released), KnightRoyale.Red);
        int next = released + 1;
        if (!KnightRoyale.GasEnds(next))
            CombatNotice(TextTemplate.Fill(ItemData.Text(KnightRoyale.GasCountdownText, "In %d seconds, Poison Gas will be released in area %d"), seconds, next), KnightRoyale.Yellow);
    }

    private void OnRoyalePing(int x, int z, bool isDelegate)
    {
        int text = isDelegate ? KnightRoyale.DelegateText : KnightRoyale.ChestText;
        string fallback = isDelegate ? "Current Delegate's Position:(%d, %d)" : "Treasure Chest has appeared. Location: (%d, %d)";
        CombatNotice(TextTemplate.Fill(ItemData.Text(text, fallback), x, z), isDelegate ? KnightRoyale.Crimson : KnightRoyale.GreenYellow);
        _royalePings.Add((x, z, Now() + KnightRoyale.PingSeconds, isDelegate));
    }

    private void OnRoyaleReward(KnightRoyale.Reward reward)
    {
        foreach (string line in KnightRoyale.RewardLines(reward, ItemData.Text))
            CombatNotice(line, KnightRoyale.GreenYellow);
    }

    private void OnRoyaleWinner(string name, int area)
    {
        string text = TextTemplate.Fill(ItemData.Text(KnightRoyale.WinnerText, "%s has won in Knight Royale area number %d."), name, area + 1);
        Chat?.ShowNoticePreview(text);
        CombatNotice(text, KnightRoyale.GreenYellow);
    }

    private void OnRoyaleRestore()
    {
        if (_royaleScore != null && IsInstanceValid(_royaleScore)) _royaleScore.Visible = false;
    }

    private void KnightRoyaleTick()
    {
        if (_royaleBoardOpen && _royaleBoardTime != null)
        {
            int left = (int)Mathf.Max(0, (float)(_royaleBoardEndsAt - Now()));
            _royaleBoardTime.Text = KnightRoyale.Clock(left);
            if (left <= 0)
            {
                _royaleBoardOpen = false;
                _royaleBoard!.Visible = false;
            }
        }
        if (_royaleScore is { Visible: true })
        {
            if (Net.I.RoyaleSurvivors > 0)
                _royaleSurvivors!.Text = TextTemplate.Fill(ItemData.Text(KnightRoyale.SurvivorsText, "Survivor(s): %d"), Net.I.RoyaleSurvivors);
            bool gas = Net.I.RoyaleCountdown && !KnightRoyale.GasEnds(Net.I.RoyaleNextArea);
            _royaleNextGas!.Text = gas ? TextTemplate.Fill(ItemData.Text(KnightRoyale.NextGasText, "Remaining time until # %d Poison Gas:"), Net.I.RoyaleNextArea) : "";
            _royaleGasTime!.Text = gas ? KnightRoyale.Clock(Net.I.RoyaleNextGasSeconds) : "";
        }
        if (Now() >= _royaleReturnBoxAt)
        {
            _royaleReturnBoxAt = double.PositiveInfinity;
            ShowRoyaleReturnBox();
        }
        _royalePings.RemoveAll(p => Now() >= p.Until);
    }

    private bool RoyaleDeath()
    {
        if (!InRoyaleZone) return false;
        _royaleReturnBoxAt = Now() + KnightRoyale.ReturnBoxDelaySeconds;
        return true;
    }

    private void ShowRoyaleReturnBox()
    {
        int left = KnightRoyale.ReturnBoxSeconds;
        string Line(int s) => TextTemplate.Fill(ItemData.Text(KnightRoyale.ReturnText, "you will be teleported back in %d seconds"), s);
        Notice.Show(this, Line(left));
        for (int s = 1; s < KnightRoyale.ReturnBoxSeconds; s++)
        {
            int shown = left - s;
            GetTree().CreateTimer(s).Timeout += () => { if (IsInstanceValid(this)) CombatNotice(Line(shown), KnightRoyale.Yellow); };
        }
    }

    private bool ShowRoyaleAreaCaption(int region)
    {
        if (!InRoyaleZone) return false;
        int area = KnightRoyale.AreaOf(region);
        if (area == 0) return false;
        int text = KnightRoyale.AreaCaption(area, Net.I.RoyaleReleasedArea);
        string caption = text == KnightRoyale.AreaText
            ? TextTemplate.Fill(ItemData.Text(text, "Area %d"), area)
            : ItemData.Text(text, "This area is filled with poison gas and will cause damage.");
        ShowAreaBanner(caption, KnightRoyale.Red, AreaNameHold, AreaNameFadeIn, AreaBannerTop);
        return true;
    }

    private void AddRoyalePings()
    {
        foreach (var ping in _royalePings)
            _blipScratch.Add(new MiniMap.Blip(ping.X, ping.Z, ping.Delegate ? KnightRoyale.Crimson : KnightRoyale.GreenYellow, RoyalePingRadius, true));
    }
}
