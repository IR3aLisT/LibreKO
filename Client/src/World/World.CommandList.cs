using System.Collections.Generic;
using Godot;
using LibreKO.Domain;

namespace LibreKO;

public partial class World
{
    private const int CommandListWidth = 220;
    private const int CommandGroupRowsShown = 6;
    private const int CommandRowsShown = 12;
    private const float CommandRowHeight = 22f;
    private const float CommandListPadding = 16f;
    private const int CommandTextInset = 14;
    private static readonly Color CommandGroupColour = new("ffff00");
    private static readonly Color CommandEntryColour = new("c6c6fb");
    private static readonly Color CommandUnavailableColour = new("808080");
    private static readonly Color CommandRefusalColour = new("ff3b3b");
    private static readonly HashSet<CommandId> CommandsNotBuilt =
        [CommandId.Recommend, CommandId.EditMerchant, CommandId.ClanCommand, CommandId.ClanBattle, CommandId.ClanKick, CommandId.BanKnights, CommandId.Report];

    private CanvasLayer _cmdLayer = null!;
    private HudWindow _cmdWindow = null!;
    private ItemList _cmdGroups = null!, _cmdEntries = null!;
    private readonly List<CommandEntry> _cmdShown = new();
    private bool _tradeRequestsBlocked, _partyRequestsBlocked;

    private void CommandListInit()
    {
        _cmdLayer = new CanvasLayer { Layer = 66 };
        AddChild(_cmdLayer);
        BuildCommandList();
    }

    private void CommandListDispose() { }

    private void ToggleCommandList()
    {
        if (_cmdWindow.Visible)
        {
            _cmdWindow.Visible = false;
            return;
        }
        if (_cmdGroups.GetSelectedItems().Length == 0) _cmdGroups.Select(0);
        FillCommandEntries(SelectedCommandGroup());
        _cmdWindow.Visible = true;
    }

    private CommandGroup SelectedCommandGroup()
    {
        int[] picked = _cmdGroups.GetSelectedItems();
        return CommandCatalog.Groups[picked.Length > 0 ? picked[0] : 0];
    }

    private void BuildCommandList()
    {
        _cmdWindow = new HudWindow("command_list", "Command", CommandListWidth) { Visible = false };
        _cmdWindow.Closed += () => _cmdWindow.Visible = false;
        _cmdLayer.AddChild(_cmdWindow);
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 10);
        _cmdWindow.Body.AddChild(body);

        _cmdGroups = CommandItemList(CommandGroupRowsShown);
        foreach (var group in CommandCatalog.Groups)
        {
            int row = _cmdGroups.AddItem(ItemData.Text(CommandCatalog.GroupText(group), CommandCatalog.GroupFallback(group)));
            _cmdGroups.SetItemCustomFgColor(row, CommandGroupColour);
        }
        _cmdGroups.ItemSelected += index => FillCommandEntries(CommandCatalog.Groups[(int)index]);
        body.AddChild(_cmdGroups);

        _cmdEntries = CommandItemList(CommandRowsShown);
        _cmdEntries.ItemActivated += index => RunListedCommand((int)index);
        body.AddChild(_cmdEntries);
    }

    private static ItemList CommandItemList(int rows)
    {
        var list = new ItemList
        {
            CustomMinimumSize = new Vector2(0, rows * CommandRowHeight + CommandListPadding),
            FocusMode = Control.FocusModeEnum.None,
            SelectMode = ItemList.SelectModeEnum.Single,
            AllowReselect = true,
        };
        list.AddThemeStyleboxOverride("panel", UiTheme.Inset());
        list.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        list.AddThemeStyleboxOverride("selected", UiTheme.ListRow(true));
        list.AddThemeStyleboxOverride("selected_focus", UiTheme.ListRow(true));
        list.AddThemeStyleboxOverride("hovered", UiTheme.ListRow(false));
        list.AddThemeStyleboxOverride("cursor", new StyleBoxEmpty());
        list.AddThemeStyleboxOverride("cursor_unfocused", new StyleBoxEmpty());
        list.AddThemeFontSizeOverride("font_size", 13);
        list.AddThemeColorOverride("font_selected_color", UiTheme.GoldBright);
        list.AddThemeColorOverride("font_hovered_color", UiTheme.TextHi);
        list.AddThemeConstantOverride("v_separation", 4);
        list.AddThemeConstantOverride("h_separation", CommandTextInset);
        return list;
    }

    private void FillCommandEntries(CommandGroup group)
    {
        _cmdEntries.Clear();
        _cmdShown.Clear();
        foreach (var entry in CommandCatalog.Entries(group))
        {
            int row = _cmdEntries.AddItem(ItemData.Text(entry.NameText, entry.Fallback));
            bool built = !CommandsNotBuilt.Contains(entry.Id);
            string hint = CommandCatalog.Hint(entry, ItemData.Text);
            _cmdEntries.SetItemTooltip(row, built ? hint : $"{hint}\n(not available yet)".TrimStart('\n'));
            _cmdEntries.SetItemCustomFgColor(row, built ? CommandEntryColour : CommandUnavailableColour);
            _cmdShown.Add(entry);
        }
    }

    private void RunListedCommand(int index)
    {
        if (index < 0 || index >= _cmdShown.Count) return;
        var entry = _cmdShown[index];
        if (CommandsNotBuilt.Contains(entry.Id)) return;
        string word = ItemData.Text(entry.NameText, entry.Fallback);
        if (entry.Prefill) Chat?.OpenWith($"/{word} ");
        else Chat?.RunCommand(word);
    }

    private bool TargetPlayer(out int id, out string name)
    {
        id = -1;
        name = "";
        if (_selectedId < 0 || _selectedId == _myId || !_ents.TryGetValue(_selectedId, out var e) || e.IsNpc) return false;
        id = _selectedId;
        name = e.Name;
        return true;
    }

    private void CommandRefusal(int textId, string fallback) =>
        CombatLogAdd(ItemData.Text(textId, fallback), CombatLogKind.Status, CommandRefusalColour);

    private bool RunCatalogCommand(CommandEntry entry, string args)
    {
        if (CommandsNotBuilt.Contains(entry.Id)) return false;
        switch (entry.Id)
        {
            case CommandId.Pm:
            {
                int gap = args.IndexOf(' ');
                string name = gap < 0 ? args : args[..gap];
                string message = gap < 0 ? "" : args[(gap + 1)..].Trim();
                if (name.Length == 0) Chat?.OpenWith($"/{ItemData.Text(entry.NameText, entry.Fallback)} ");
                else if (message.Length == 0) OpenWhisperWith(name);
                else Chat?.SendWhisper(name, message);
                return true;
            }
            case CommandId.Town:
                TownRecallPress();
                return true;
            case CommandId.Exit:
                RequestExit(ExitKind.Quit);
                return true;
            case CommandId.IndividualBattle:
                if (args.Length > 0) ChallengeByName(args);
                else if (TargetPlayer(out _, out string duelName)) ChallengeByName(duelName);
                else CommandRefusal(CommandCatalog.NoTargetText, "No target selected.");
                return true;
            case CommandId.SitStand:
                ToggleSitting();
                return true;
            case CommandId.WalkRun:
                ToggleRunMode();
                return true;
            case CommandId.Location:
                CombatLogAdd($"{ZoneCatalog.Name(_zone)} ({_myKoX:0}, {_myKoZ:0})", CombatLogKind.Status);
                return true;
            case CommandId.AddFriend:
                if (args.Length > 0) RequestFriend(args);
                else if (TargetPlayer(out _, out string friendName)) RequestFriend(friendName);
                else Chat?.OpenWith($"/{ItemData.Text(entry.NameText, entry.Fallback)} ");
                return true;
            case CommandId.ChatBlock:
                ToggleWhisperBlock();
                return true;
            case CommandId.Vault:
                OpenVipVaultByCommand();
                return true;
            case CommandId.Achieve:
                ToggleAchievements();
                return true;
            case CommandId.Trade:
                if (TargetPlayer(out int tradeId, out string tradeName)) RequestTradeWith(tradeId, tradeName);
                else CommandRefusal(CommandCatalog.NoTargetText, "No target selected.");
                return true;
            case CommandId.BlockTrade or CommandId.AllowTrade:
                _tradeRequestsBlocked = entry.Id == CommandId.BlockTrade;
                CommandRefusal(_tradeRequestsBlocked ? CommandCatalog.TradeBlockedText : CommandCatalog.TradeAllowedText,
                    _tradeRequestsBlocked ? "Block all Trade Requests." : "Allow all Trade Requests.");
                return true;
            case CommandId.Merchant:
                if (!_merchantMenuShown) ToggleMerchantMenu();
                return true;
            case CommandId.Price:
                OpenMarketPrice();
                return true;
            case CommandId.Party:
                if (args.Length > 0) InvitePlayerToParty(args);
                else if (TargetPlayer(out _, out string partyName)) InvitePlayerToParty(partyName);
                else CommandRefusal(CommandCatalog.NoTargetText, "No target selected.");
                return true;
            case CommandId.QuitParty:
                if (InParty) AskLeaveParty();
                else CombatNotice("You are not in a party.");
                return true;
            case CommandId.SeekingParty:
                ToggleSeekParty();
                return true;
            case CommandId.BlockParty or CommandId.AllowParty:
                _partyRequestsBlocked = entry.Id == CommandId.BlockParty;
                CommandRefusal(_partyRequestsBlocked ? CommandCatalog.PartyBlockedText : CommandCatalog.PartyAllowedText,
                    _partyRequestsBlocked ? "Block all Party Requests." : "Allow all Party Requests.");
                return true;
            case CommandId.JoinClan:
                if (MyClan.CanInvite && TargetPlayer(out int recruitId, out string recruitName))
                {
                    Net.I.SendClanInvite(recruitId);
                    CombatNotice($"Inviting {recruitName} to the clan…");
                }
                return true;
            case CommandId.QuitClan:
                if (MyClan.InClan) _clanLeaveAsk.PopupCentered();
                return true;
            case CommandId.BanFromClan:
                if (args.Length == 0) Chat?.OpenWith($"/{ItemData.Text(entry.NameText, entry.Fallback)} ");
                else if (MyClan.IsChief)
                    Notice.Confirm(this, TextTemplate.Fill(ItemData.Text(CommandCatalog.BanFromClanAskText, "Do you want to ban %s from clan?"), args),
                        "OK", "Cancel", () => Net.I.SendClanKick(args), title: "Clan");
                return true;
            case CommandId.ClanWar:
                if ((MyClan.IsChief || MyClan.Fame == ClanRanks.CommandCaptain) && TargetPlayer(out int rivalId, out _))
                    Net.I.SendClanBattleChallenge(rivalId);
                return true;
            case CommandId.Surrender:
                if (MyClan.IsChief || MyClan.Fame == ClanRanks.CommandCaptain) Net.I.SendClanBattleSurrender();
                return true;
            case CommandId.AppointVice:
                if (args.Length == 0) Chat?.OpenWith($"/{ItemData.Text(entry.NameText, entry.Fallback)} ");
                else if (MyClan.IsChief) Net.I.SendClanPromoteVice(args);
                return true;
            case CommandId.ClanChat:
                if (args.Length == 0) Chat?.OpenWith($"/{ItemData.Text(entry.NameText, entry.Fallback)} ");
                else if (MyClan.InClan) Net.I.SendChat(args, Domain.ChatType.Clan);
                return true;
            case CommandId.ClanNotice:
                if (args.Length == 0) Chat?.OpenWith($"/{ItemData.Text(entry.NameText, entry.Fallback)} ");
                else if (MyClan.IsChief || MyClan.IsViceChief) Net.I.SendClanNotice(args);
                return true;
            case CommandId.Confederacy:
                if (MyClan.IsChief && TargetPlayer(out int allyId, out _))
                {
                    if (MyClan.AllianceId == MyClan.ClanId) Net.I.SendAllianceInsert(allyId);
                    else Net.I.SendAllianceCreate(allyId);
                }
                return true;
            case CommandId.QuitKnights:
                if (MyClan.IsChief && MyClan.AllianceId > 0) Net.I.SendAllianceLeave();
                return true;
            default:
                return false;
        }
    }
}
