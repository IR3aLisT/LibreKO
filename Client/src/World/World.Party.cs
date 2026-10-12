using System.Collections.Generic;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private CanvasLayer _partyLayer = null!;
    private int _partyPick = -1;
    private int _partyPickSelection = -1;
    private int _partyHudHighlight = -1;

    private int PartyHighlight => PartyHud.Highlight(_selectedId, IsPartyMember(_selectedId), _partyPick);

    private ConfirmationDialog _inviteAskDialog = null!;
    private CanvasLayer _partyDialogLayer = null!;
    private bool _invitePending;

    private const int PartyMaxMembers = 8;

    private static IReadOnlyList<PartyMember> PartyMembers => Net.I.Party;
    private readonly PartyOrder _partyOrder = new();
    private List<PartyMember> PartyRows => _partyOrder.Arrange(PartyMembers, member => member.CharId);

    private void MovePartyRow(int memberId, int step)
    {
        if (_partyOrder.Move(memberId, step)) RefreshPartyHud();
    }
    private bool InParty => Net.I.InParty;
    private bool AmLeader => PartyMembers.Count > 0 && PartyMembers[0].CharId == _myId;

    private void PartyInit()
    {
        BuildPartyPanel();
        BuildSeekPartyPanel();
        BuildPartyHud();
        if (!PluginHost.Ui.HudHidden(LibreKO.Plugins.HudPart.Party)) KeepRowMeterOff();

        Net.I.PartyMemberEvent += OnPartyMember;
        Net.I.PartyRejoinForgetEvent += _partyRejoin.Forget;
        Net.I.PartyErrorEvent += OnPartyError;
        Net.I.PartyInviteEvent += OnPartyInvite;
        Net.I.PartyRemovedEvent += OnPartyRemoved;
        Net.I.PartyDisbandEvent += OnPartyDisband;
        Net.I.PartyMemberStatsEvent += OnPartyMemberStats;
        Net.I.PartyMemberLevelEvent += OnPartyMemberLevel;
        Net.I.PartyMemberClassEvent += OnPartyMemberClass;
        Net.I.PartyMemberStatusEvent += OnPartyMemberStatus;
        Net.I.PartyMemberBuffsEvent += OnPartyMemberBuffs;
        Net.I.PartySignalEvent += RefreshPartySignals;
        Net.I.PartyLevelProgressEvent += OnPartyLevelProgress;
        Net.I.PartyBbsRegisterEvent += OnBbsRegister;
        Net.I.PartyBbsDeleteEvent += OnBbsDelete;
        Net.I.PartyBbsListEvent += OnBbsList;
        Net.I.PartyBbsWantedFailEvent += OnBbsWantedFail;
        Net.I.PartyBbsInviteEvent += OnBbsInvite;
        Net.I.SelfHpEvent += OnPartySelfHp;
        Net.I.SelfMpEvent += OnPartySelfMp;
    }

    private void PartyDispose()
    {
        Net.I.PartyMemberEvent -= OnPartyMember;
        Net.I.PartyRejoinForgetEvent -= _partyRejoin.Forget;
        Net.I.PartyErrorEvent -= OnPartyError;
        Net.I.PartyInviteEvent -= OnPartyInvite;
        Net.I.PartyRemovedEvent -= OnPartyRemoved;
        Net.I.PartyDisbandEvent -= OnPartyDisband;
        Net.I.PartyMemberStatsEvent -= OnPartyMemberStats;
        Net.I.PartyMemberLevelEvent -= OnPartyMemberLevel;
        Net.I.PartyMemberClassEvent -= OnPartyMemberClass;
        Net.I.PartyMemberStatusEvent -= OnPartyMemberStatus;
        Net.I.PartyMemberBuffsEvent -= OnPartyMemberBuffs;
        Net.I.PartySignalEvent -= RefreshPartySignals;
        Net.I.PartyLevelProgressEvent -= OnPartyLevelProgress;
        Net.I.PartyBbsRegisterEvent -= OnBbsRegister;
        Net.I.PartyBbsDeleteEvent -= OnBbsDelete;
        Net.I.PartyBbsListEvent -= OnBbsList;
        Net.I.PartyBbsWantedFailEvent -= OnBbsWantedFail;
        Net.I.PartyBbsInviteEvent -= OnBbsInvite;
        Net.I.SelfHpEvent -= OnPartySelfHp;
        Net.I.SelfMpEvent -= OnPartySelfMp;
    }

    private void BuildPartyPanel()
    {
        _partyLayer = new CanvasLayer { Layer = 70 };
        AddChild(_partyLayer);
        BuildInvitePrompts();
    }

    private static Button SmallButton(string text, Container parent)
    {
        var b = new Button { Text = text, FocusMode = Control.FocusModeEnum.None };
        b.AddThemeFontSizeOverride("font_size", 12);
        parent.AddChild(b);
        return b;
    }

    private void RefreshPartyUI() => RefreshPartyHud();

    private void ToggleParty() => ToggleSeekParty();

    private void BuildInvitePrompts()
    {
        _partyDialogLayer = new CanvasLayer { Layer = 72 };
        AddChild(_partyDialogLayer);
        _inviteAskDialog = new ConfirmationDialog
        {
            Title = "Party Invite",
            OkButtonText = "Join",
            Exclusive = false,
        };
        _inviteAskDialog.GetCancelButton().Text = "Decline";
        _inviteAskDialog.Confirmed += () => AnswerInvite(true);
        _inviteAskDialog.Canceled += () => AnswerInvite(false);
        _partyDialogLayer.AddChild(_inviteAskDialog);
    }

    private void OpenInvitePrompt() =>
        Notice.AskText(this, InviteByNamePrompt, ConfirmInvite, InviteByNameTitle, placeholder: InviteNamePlaceholder,
            maxLength: CharacterNameMax, confirmText: InviteSendText);

    private const string InviteByNameTitle = "Party Invite";
    private const string InviteByNamePrompt = "Invite player by name:";
    private const string InviteNamePlaceholder = "Character name";
    private const string InviteSendText = "Send";
    private const int CharacterNameMax = 20;

    private void ConfirmInvite(string typed)
    {
        string name = typed.Trim();
        if (name.Length == 0) return;
        if (InParty) Net.I.SendPartyInvite(name);
        else Net.I.SendPartyCreate(name);
        CombatNotice($"{name} {ItemData.Text(PartyInvitedText, "was invited into the party. Waiting for a response.")}");
    }

    private const int PartyInvitedText = 3411;
    private const int PartyInviteAskText = 3415;
    private const int PartyQuitText = 3414;
    private const int PartyDisbandedText = 3404;

    private void OnPartyInvite(int inviterId, string inviterName)
    {
        if (_partyRequestsBlocked)
        {
            CommandRefusal(CommandCatalog.PartyRequestsRefusedText, "Has blocked party requests.");
            Net.I.SendPartyAnswer(false);
            return;
        }
        _geniePendingInviterId = inviterId;
        _geniePendingInviterName = inviterName;
        if (GenieAcceptInvite(inviterId, inviterName))
        {
            _invitePending = false;
            _inviteAskDialog.Hide();
            Net.I.SendPartyAnswer(true);
            return;
        }
        _invitePending = true;
        string ask = $"{inviterName} {ItemData.Text(PartyInviteAskText, "has invited you to join the party. Will you join?")}";
        _inviteAskDialog.DialogText = ask;
        _inviteAskDialog.PopupCentered();
        CombatNotice(ask);
    }

    private void OnBbsInvite(PartyBoardInvite invite)
    {
        if (_partyRequestsBlocked || InParty) return;
        byte board = Net.I.PartyBoardMode;
        string text = invite.Prompt(ItemData.Text(PartyBoardInvite.PromptText, PartyBoardInvite.PromptFallback), ClassName(invite.Class));
        Notice.Confirm(this, text, "Yes", "No", () => Net.I.SendPartyBbsAccept(invite.Inviter, board), title: "Party");
    }

    private void AnswerInvite(bool accept)
    {
        if (!_invitePending) return;
        _invitePending = false;
        _geniePendingInviterId = -1;
        _geniePendingInviterName = "";
        Net.I.SendPartyAnswer(accept);
    }

    private readonly PartyRejoin _partyRejoin = new();

    private void OnPartyMember(PartyMember m)
    {
        if (m.CharId != _myId) _partyRejoin.Remember(m.CharId, m.Name);
        RefreshPartyUI();
        RefreshPartyPlates();
    }

    private void TryPartyRejoin(string name)
    {
        bool member = false;
        foreach (var m in PartyMembers)
            if (string.Equals(m.Name, name, System.StringComparison.OrdinalIgnoreCase)) member = true;
        if (_partyRejoin.Recall(name, AmLeader, PartyMembers.Count, PartyMaxMembers, member))
            InvitePlayerToParty(name);
    }

    private string PartyRejoinTip => _partyRejoin.On
        ? ItemData.Text(PartyRejoin.TipOnText, PartyRejoin.TipOnFallback)
        : ItemData.Text(PartyRejoin.TipOffText, PartyRejoin.TipOffFallback);

    private void TogglePartyRejoin()
    {
        _partyRejoin.Toggle();
        CombatNotice(_partyRejoin.On
            ? ItemData.Text(PartyRejoin.TurnedOnText, PartyRejoin.TurnedOnFallback)
            : ItemData.Text(PartyRejoin.TurnedOffText, PartyRejoin.TurnedOffFallback));
        RefreshPartyConfig();
        PluginNotifyParty();
    }

    private void OnPartyError(int code)
    {
        CombatNotice(ItemData.Text(PartyInviteRefusal.TextId(code), PartyInviteRefusal.Fallback(code)));
    }

    private void OnPartyRemoved(int memberId, string who)
    {
        if (memberId == _myId) _partyRejoin.Clear();
        if (memberId == _myId) CombatNotice(ItemData.Text(PartyQuitText, "You've quit the party."));
        else if (who.Length > 0) CombatNotice($"{who} left the party.");
        RefreshPartyUI();
        RefreshPartyPlates();
    }

    private void OnPartyDisband()
    {
        _partyRejoin.Clear();
        CombatNotice(ItemData.Text(PartyDisbandedText, "The party has been disbanded."));
        RefreshPartyUI();
        RefreshPartyPlates();
    }

    private void OnPartySelfHp(int hp, int maxHp, int attackerId) => RefreshPartyRows();
    private void OnPartySelfMp(int mp, int maxMp) => RefreshPartyRows();

    private void OnPartyMemberStats(int cid, int maxHp, int hp, int maxMp, int mp) => RefreshPartyRows();

    private void OnPartyMemberLevel(int cid, int level) => RefreshPartyRows();

    private void OnPartyMemberClass(int cid, int cls) => RefreshPartyRows();

    private void OnPartyMemberStatus(int cid, byte statusType, bool applied) => RefreshPartyRows();

    private void OnPartyMemberBuffs(int cid, IReadOnlyList<int> skillIds) => RefreshPartyRows();

    private void OnPartyLevelProgress(int cid, int permille) => RefreshPartyRows();

    private void RefreshPartyRows() => RefreshPartyHud();

    private static string StatusName(byte type) => type switch
    {
        1 => "dot", 2 => "poison", 3 => "disease", 4 => "blind", 5 => "low hp", _ => "",
    };
}
