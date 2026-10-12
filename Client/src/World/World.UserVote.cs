using System;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const int PartyMenuUserVote = 4;
    private const int UserVoteWidth = 300;
    private const int UserVoteLayer = 76;
    private CanvasLayer _userVoteLayer = null!;
    private HudWindow? _userVoteSelect;
    private HudWindow? _userVoteNotice;
    private int _userVoteMember = -1;
    private string _userVoteMemberName = "";
    private UserVoteAsk? _userVoteAskShown;
    private string[] _userVoteNoticeLines = Array.Empty<string>();
    private HudWindow? _userVoteAsk;
    private ProgressBar? _userVoteAskBar;
    private double _userVoteSentAt = double.NegativeInfinity;
    private double _userVoteAskUntil;

    private string VoteText(int id, string fallback) => ItemData.Text(id, fallback);

    private void UserVoteInit()
    {
        _userVoteLayer = new CanvasLayer { Layer = UserVoteLayer };
        AddChild(_userVoteLayer);
        Net.I.UserVoteAskEvent += OnUserVoteAsk;
        Net.I.UserVoteRefusedEvent += OnUserVoteRefused;
        Net.I.UserVoteResultEvent += OnUserVoteResult;
        Net.I.JuraidWarningEvent += OnJuraidWarning;
        PluginHost.Game.AttachUserVote(new PluginUserVoteBridge(this));
        if (_zone != UserVote.JuraidZone && Net.I.PendingVoteNotice is { } pending)
        {
            Net.I.PendingVoteNotice = null;
            Callable.From(() => ShowVoteNotice(pending.Warnings, pending.Limit)).CallDeferred();
        }
    }

    private void UserVoteDispose()
    {
        Net.I.UserVoteAskEvent -= OnUserVoteAsk;
        Net.I.UserVoteRefusedEvent -= OnUserVoteRefused;
        Net.I.UserVoteResultEvent -= OnUserVoteResult;
        Net.I.JuraidWarningEvent -= OnJuraidWarning;
    }

    private void OpenUserVote(int memberId)
    {
        if (memberId == _myId) return;
        if (_zone != UserVote.JuraidZone)
        {
            CombatNotice(VoteText(UserVote.NotHereText, "You cannot use that here."));
            return;
        }
        double left = UserVote.CooldownLeft(_userVoteSentAt, Now());
        if (left > 0)
        {
            CombatNotice(VoteText(UserVote.AvailableInText, "It is available in %d seconds")
                .Replace("%d", Math.Ceiling(left).ToString(System.Globalization.CultureInfo.InvariantCulture)));
            return;
        }
        _userVoteSelect?.QueueFree();
        _userVoteMember = memberId;
        _userVoteMemberName = PartyMemberName(memberId);
        _userVoteSelect = BuildUserVoteSelect(memberId, _userVoteMemberName);
        _userVoteLayer.AddChild(_userVoteSelect);
        PluginNotifyUserVote();
    }

    private void PluginNotifyUserVote() => Callable.From(PluginHost.Game.RaiseUserVote).CallDeferred();

    private string UserVotePrompt(string name) =>
        VoteText(UserVote.ChooseReasonText, "Please select the reason for %s warning").Replace("%s", name);

    private string UserVoteCostLine() => VoteText(UserVote.CostText, "It will cost 4 million Noah to apply for a vote.");

    private bool SubmitUserVote(int reason, string text)
    {
        if (_userVoteMember < 0 || reason is < 1 or > UserVote.Reasons) return false;
        text = reason == UserVote.ReasonOther ? text.Trim() : "";
        if (!UserVote.TextFits(text))
        {
            CombatNotice(VoteText(UserVote.TextTooLongText, "You have exceeded the maximum characters allowed."));
            return false;
        }
        Net.I.SendUserVote(_userVoteMember, (byte)reason, text);
        _userVoteSentAt = Now();
        CloseUserVoteSelect();
        return true;
    }

    private void CloseUserVoteSelect()
    {
        _userVoteSelect?.QueueFree();
        _userVoteSelect = null;
        _userVoteMember = -1;
        PluginNotifyUserVote();
    }

    private HudWindow BuildUserVoteSelect(int memberId, string name)
    {
        var window = new HudWindow("user_vote", VoteText(UserVote.TitleText, "User Vote"), new Vector2(420, 180), UserVoteWidth);
        var body = window.Body;
        body.AddThemeConstantOverride("separation", 6);
        var prompt = UiTheme.Text(UserVotePrompt(name), 12, UiTheme.TextHi);
        prompt.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        body.AddChild(prompt);
        var group = new ButtonGroup();
        var options = new CheckBox[UserVote.Reasons];
        var other = new LineEdit { MaxLength = UserVote.OtherTextMaxLetters };
        int chosen = 1;
        for (int reason = 1; reason <= UserVote.Reasons; reason++)
        {
            int which = reason;
            var option = UiTheme.FlatCheck(VoteText(UserVote.ReasonText(reason), $"Reason {reason}"), reason == 1);
            option.ButtonGroup = group;
            option.Pressed += () => chosen = which;
            options[reason - 1] = option;
            body.AddChild(option);
        }
        other.TextChanged += _ =>
        {
            chosen = UserVote.ReasonOther;
            options[UserVote.ReasonOther - 1].SetPressedNoSignal(true);
        };
        body.AddChild(other);
        var cost = UiTheme.Text(UserVoteCostLine(), 11, UiTheme.Gold);
        cost.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        body.AddChild(cost);
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 8);
        body.AddChild(buttons);
        var ok = new Button { Text = "OK", FocusMode = Control.FocusModeEnum.None };
        ok.Pressed += () => SubmitUserVote(chosen, other.Text);
        buttons.AddChild(ok);
        var cancel = new Button { Text = "Cancel", FocusMode = Control.FocusModeEnum.None };
        cancel.Pressed += CloseUserVoteSelect;
        buttons.AddChild(cancel);
        window.Visible = true;
        return window;
    }

    private void OnUserVoteAsk(UserVoteAsk ask)
    {
        _userVoteAsk?.QueueFree();
        _userVoteAskShown = ask;
        _userVoteAsk = BuildUserVoteAsk(ask);
        _userVoteLayer.AddChild(_userVoteAsk);
        _userVoteAskUntil = Now() + UserVote.AnswerSeconds;
        PluginNotifyUserVote();
    }

    private string UserVoteQuestion(UserVoteAsk ask) =>
        VoteText(UserVote.AskText, "Would you like to vote for warning of %s?").Replace("%s", ask.Target);

    private string UserVoteReasonLine(UserVoteAsk ask) =>
        ask.Reason == UserVote.ReasonOther && ask.Text.Length > 0
            ? ask.Text
            : ask.Reason is >= 1 and <= UserVote.Reasons ? VoteText(UserVote.ReasonText(ask.Reason), "") : "";

    private HudWindow BuildUserVoteAsk(UserVoteAsk ask)
    {
        var window = new HudWindow("user_vote_ask", VoteText(UserVote.TitleText, "User Vote"), new Vector2(420, 220), UserVoteWidth, closable: false);
        var body = window.Body;
        body.AddThemeConstantOverride("separation", 6);
        var question = UiTheme.Text(UserVoteQuestion(ask), 12, UiTheme.TextHi);
        question.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        body.AddChild(question);
        string reason = UserVoteReasonLine(ask);
        if (reason.Length > 0) body.AddChild(UiTheme.Text(reason, 11, UiTheme.Gold));
        _userVoteAskBar = new ProgressBar { MaxValue = UserVote.AnswerSeconds, Value = UserVote.AnswerSeconds, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 8) };
        body.AddChild(_userVoteAskBar);
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 8);
        body.AddChild(buttons);
        foreach (var (caption, answer) in new[] { ("Yes", UserVote.AnswerYes), ("Draw", UserVote.AnswerDraw), ("No", UserVote.AnswerNo) })
        {
            var button = new Button { Text = caption, FocusMode = Control.FocusModeEnum.None };
            button.Pressed += () => AnswerUserVote(answer);
            buttons.AddChild(button);
        }
        window.Visible = true;
        return window;
    }

    private void AnswerUserVote(byte answer)
    {
        if (_userVoteAsk == null) return;
        Net.I.SendUserVoteAnswer(answer);
        _userVoteAsk.QueueFree();
        _userVoteAsk = null;
        _userVoteAskBar = null;
        _userVoteAskShown = null;
        PluginNotifyUserVote();
    }

    private void UserVoteTick(double now)
    {
        if (_userVoteAsk == null || _userVoteAskBar == null || !IsInstanceValid(_userVoteAsk)) return;
        double left = _userVoteAskUntil - now;
        _userVoteAskBar.Value = Math.Max(0, left);
        if (left <= 0) AnswerUserVote(UserVote.AnswerDraw);
    }

    private void OnUserVoteRefused(short result) =>
        CombatNotice(VoteText(UserVote.RefusalText(result), "The vote could not start."));

    private void OnUserVoteResult(UserVoteOutcome outcome)
    {
        if (outcome.Counted) return;
        CombatNotice(outcome.Warned
            ? VoteText(UserVote.WarnedText, "Voting Result: 1 warning has been given to the target user.")
            : VoteText(UserVote.VoidedText, "Voting Result: The vote was voided."));
    }

    private void ShowVoteNotice(int warnings, int limit)
    {
        string juraid = VoteText(UserVote.JuraidNameText, "Juraid");
        _userVoteNoticeLines = new[]
        {
            VoteText(UserVote.NoticeReceivedText, "You have received  %d warnings as a result of user voting of  %s.")
                .Replace("%d", warnings.ToString()).Replace("%s", juraid),
            Fill(VoteText(UserVote.NoticeCumulativeText, "Cumulative Warning:  %d / %d"), warnings, limit),
            VoteText(UserVote.NoticeLimitText, "If you get %d warnings, you will not be enter %s.")
                .Replace("%d", limit.ToString()).Replace("%s", juraid),
        };
        _userVoteNotice?.QueueFree();
        var window = new HudWindow("user_vote_notice", VoteText(UserVote.NoticeTitleText, "Party member evaluation"),
            new Vector2(420, 200), UserVoteWidth, closable: false);
        _userVoteNotice = window;
        _userVoteLayer.AddChild(window);
        window.Body.AddThemeConstantOverride("separation", 6);
        foreach (var line in _userVoteNoticeLines)
        {
            var label = UiTheme.Text(line, 12, UiTheme.TextHi);
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            window.Body.AddChild(label);
        }
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        window.Body.AddChild(row);
        var ok = new Button { Text = "OK", FocusMode = Control.FocusModeEnum.None };
        ok.Pressed += CloseVoteNotice;
        row.AddChild(ok);
        window.Visible = true;
        PluginNotifyUserVote();
    }

    private void CloseVoteNotice()
    {
        _userVoteNotice?.QueueFree();
        _userVoteNotice = null;
        PluginNotifyUserVote();
    }

    private sealed class PluginUserVoteBridge : LibreKO.Plugins.IGameUserVote
    {
        private readonly World _w;

        public PluginUserVoteBridge(World w) => _w = w;

        public string Title => _w.VoteText(UserVote.TitleText, "User Vote");
        public string SelectPrompt => _w.UserVotePrompt(_w._userVoteMemberName);

        public IReadOnlyList<string> Reasons
        {
            get
            {
                var reasons = new string[UserVote.Reasons];
                for (int i = 0; i < reasons.Length; i++) reasons[i] = _w.VoteText(UserVote.ReasonText(i + 1), $"Reason {i + 1}");
                return reasons;
            }
        }

        public string CostLine => _w.UserVoteCostLine();
        public string AskQuestion => _w._userVoteAskShown is { } ask ? _w.UserVoteQuestion(ask) : "";
        public string AskReason => _w._userVoteAskShown is { } ask ? _w.UserVoteReasonLine(ask) : "";
        public double AskSecondsLeft => _w._userVoteAskShown != null ? Math.Max(0, _w._userVoteAskUntil - Now()) : 0;
        public double AskSeconds => UserVote.AnswerSeconds;
        public string NoticeTitle => _w.VoteText(UserVote.NoticeTitleText, "Party member evaluation");
        public IReadOnlyList<string> NoticeLines => _w._userVoteNoticeLines;
        public void Send(int reason, string text) => _w.SubmitUserVote(reason, text);
        public void CancelSelect() => _w.CloseUserVoteSelect();
        public void Answer(byte answer) => _w.AnswerUserVote(answer);
        public void CloseNotice() => _w.CloseVoteNotice();

        event Action? LibreKO.Plugins.IGameUserVote.Changed { add { } remove { } }
    }

    private void OnJuraidWarning(int warnings, int limit)
    {
        string text = Fill(VoteText(UserVote.EntryBarredText,
            "Currently, the cumulative warning is  %d times. If  %d times are accumulated, you will not be able to enter %s"),
            warnings, limit).Replace("%s", VoteText(UserVote.JuraidNameText, "Juraid"));
        Notice.Show(this, text, VoteText(UserVote.NoticeTitleText, "Party member evaluation"));
    }

    private static string Fill(string format, int first, int second)
    {
        int at = format.IndexOf("%d", StringComparison.Ordinal);
        if (at >= 0) format = format[..at] + first + format[(at + 2)..];
        at = format.IndexOf("%d", StringComparison.Ordinal);
        if (at >= 0) format = format[..at] + second + format[(at + 2)..];
        return format;
    }
}
