using System.Collections.Generic;
using Godot;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private CanvasLayer _partyLayer = null!;
    private int _partyPick = -1;

    private ConfirmationDialog _inviteAskDialog = null!;
    private CanvasLayer _partyDialogLayer = null!;
    private bool _invitePending;

    private const int PartyMaxMembers = 8;

    private static IReadOnlyList<PartyMember> PartyMembers => Net.I.Party;
    private bool InParty => Net.I.InParty;
    private bool AmLeader => PartyMembers.Count > 0 && PartyMembers[0].CharId == _myId;

    private void PartyInit()
    {
        BuildPartyPanel();
        BuildSeekPartyPanel();
        BuildPartyHud();

        Net.I.PartyMemberEvent += OnPartyMember;
        Net.I.PartyErrorEvent += OnPartyError;
        Net.I.PartyInviteEvent += OnPartyInvite;
        Net.I.PartyRemovedEvent += OnPartyRemoved;
        Net.I.PartyDisbandEvent += OnPartyDisband;
        Net.I.PartyMemberStatsEvent += OnPartyMemberStats;
        Net.I.PartyMemberLevelEvent += OnPartyMemberLevel;
        Net.I.PartyMemberClassEvent += OnPartyMemberClass;
        Net.I.PartyMemberStatusEvent += OnPartyMemberStatus;
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
        Net.I.PartyErrorEvent -= OnPartyError;
        Net.I.PartyInviteEvent -= OnPartyInvite;
        Net.I.PartyRemovedEvent -= OnPartyRemoved;
        Net.I.PartyDisbandEvent -= OnPartyDisband;
        Net.I.PartyMemberStatsEvent -= OnPartyMemberStats;
        Net.I.PartyMemberLevelEvent -= OnPartyMemberLevel;
        Net.I.PartyMemberClassEvent -= OnPartyMemberClass;
        Net.I.PartyMemberStatusEvent -= OnPartyMemberStatus;
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

    private void OnPartyMember(PartyMember m) => RefreshPartyUI();

    private void OnPartyError(int code)
    {
        CombatNotice(ItemData.Text(PartyInviteRefusal.TextId(code), PartyInviteRefusal.Fallback(code)));
    }

    private void OnPartyRemoved(int memberId, string who)
    {
        if (memberId == _myId) CombatNotice(ItemData.Text(PartyQuitText, "You've quit the party."));
        else if (who.Length > 0) CombatNotice($"{who} left the party.");
        RefreshPartyUI();
    }

    private void OnPartyDisband()
    {
        CombatNotice(ItemData.Text(PartyDisbandedText, "The party has been disbanded."));
        RefreshPartyUI();
    }

    private void OnPartySelfHp(int hp, int maxHp, int attackerId) => RefreshPartyRows();
    private void OnPartySelfMp(int mp, int maxMp) => RefreshPartyRows();

    private void OnPartyMemberStats(int cid, int maxHp, int hp, int maxMp, int mp) => RefreshPartyRows();

    private void OnPartyMemberLevel(int cid, int level) => RefreshPartyRows();

    private void OnPartyMemberClass(int cid, int cls) => RefreshPartyRows();

    private void OnPartyMemberStatus(int cid, byte statusType, bool applied) => RefreshPartyRows();

    private void RefreshPartyRows() => RefreshPartyHud();

    private static string StatusName(byte type) => type switch
    {
        1 => "dot", 2 => "poison", 3 => "disease", 4 => "blind", 5 => "low hp", _ => "",
    };
}
