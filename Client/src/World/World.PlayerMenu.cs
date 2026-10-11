using Godot;

namespace LibreKO;

public partial class World
{
    private enum PlayerMenuAction
    {
        RequestParty = 1,
        Report,
        UserTrade,
        Whisper,
        AddFriend,
        UserInformation,
        Duel,
        EquipmentView,
        ClanInvite,
        CopyName,
        ClanBattle,
        ClanSurrender,
    }

    private static readonly System.Collections.Generic.Dictionary<PlayerMenuAction, (string Key, string Caption)> PlayerMenuRows = new()
    {
        [PlayerMenuAction.RequestParty] = ("party", "Request a party"),
        [PlayerMenuAction.Report] = ("report", "Report"),
        [PlayerMenuAction.UserTrade] = ("trade", "User trade"),
        [PlayerMenuAction.Whisper] = ("whisper", "Whisper"),
        [PlayerMenuAction.AddFriend] = ("friend", "Add friend"),
        [PlayerMenuAction.UserInformation] = ("info", "User Information"),
        [PlayerMenuAction.Duel] = ("duel", "Duel"),
        [PlayerMenuAction.EquipmentView] = ("equipment", "Equipment View"),
        [PlayerMenuAction.ClanInvite] = ("clan_invite", "Clan invite"),
        [PlayerMenuAction.CopyName] = ("copy_name", "Copy name"),
        [PlayerMenuAction.ClanBattle] = ("clan_battle", "Clan battle"),
        [PlayerMenuAction.ClanSurrender] = ("clan_surrender", "Surrender the clan battle"),
    };

    private PopupMenu _playerMenu = null!;
    private int _playerMenuId = -1;
    private string _playerMenuName = "";
    private CanvasLayer? _playerMenuSkinLayer;

    private void PlayerMenuInit()
    {
        _playerMenu = new PopupMenu();
        _playerMenu.IdPressed += OnPlayerMenuAction;
        AddChild(_playerMenu);
    }

    private void PlayerMenuDispose()
    {
        if (GodotObject.IsInstanceValid(_playerMenu))
            _playerMenu.IdPressed -= OnPlayerMenuAction;
    }

    private bool TryOpenPlayerMenu(Vector2 mouse)
    {
        var target = PickEntityAt(mouse, ClickPickRadius, out int id, out _);
        if (target == null || target.IsNpc || target.Dead || id == _myId) return false;

        ShowPlayerMenu(id, target, mouse);
        return true;
    }

    private void ShowPlayerMenu(int id, Ent target, Vector2 at)
    {
        Select(id, target);
        _playerMenuId = id;
        _playerMenuName = target.Name;

        var rows = new System.Collections.Generic.List<PlayerMenuAction>
        {
            PlayerMenuAction.RequestParty, PlayerMenuAction.Report, PlayerMenuAction.UserTrade, PlayerMenuAction.Whisper,
            PlayerMenuAction.AddFriend, PlayerMenuAction.UserInformation, PlayerMenuAction.Duel, PlayerMenuAction.EquipmentView,
        };
        if (MyClan.CanInvite && target.KnightsId == 0) rows.Add(PlayerMenuAction.ClanInvite);
        if (CanChallengeClan(target)) rows.Add(PlayerMenuAction.ClanBattle);
        if (MyClan.IsChief && Net.I.EnemyClanId > 0 && target.KnightsId == Net.I.EnemyClanId)
            rows.Add(PlayerMenuAction.ClanSurrender);
        rows.Add(PlayerMenuAction.CopyName);
        PresentPlayerMenu(rows, at);
    }

    private void PresentPlayerMenu(System.Collections.Generic.List<PlayerMenuAction> rows, Vector2 at)
    {
        ClosePlayerMenuSkin();
        if (PluginHost.Ui.PlayerMenuBuilder is { } build)
        {
            var request = new LibreKO.Plugins.PlayerMenuRequest(_playerMenuName, at,
                rows.ConvertAll(action => new LibreKO.Plugins.PlayerMenuRow(PlayerMenuRows[action].Key, PlayerMenuRows[action].Caption,
                    () => { ClosePlayerMenuSkin(); OnPlayerMenuAction((long)action); })),
                ClosePlayerMenuSkin);
            _playerMenuSkinLayer = new CanvasLayer { Layer = PlayerMenuSkinLayer };
            AddChild(_playerMenuSkinLayer);
            _playerMenuSkinLayer.AddChild(build(request));
            return;
        }
        _playerMenu.Clear();
        foreach (var action in rows) _playerMenu.AddItem(PlayerMenuRows[action].Caption, (int)action);
        PopupAtCursor(_playerMenu, at);
    }

    private const int PlayerMenuSkinLayer = 90;
    private const float MenuCursorGap = 2f;

    private void PopupAtCursor(Popup menu, Vector2 cursor)
    {
        menu.ResetSize();
        var screen = GetViewport().GetVisibleRect().Size;
        menu.Position = (Vector2I)TooltipPlacement.AroundCursor(cursor, menu.Size, screen, MenuCursorGap, 0f);
        menu.Popup();
    }

    private void ClosePlayerMenuSkin()
    {
        if (_playerMenuSkinLayer != null && IsInstanceValid(_playerMenuSkinLayer)) _playerMenuSkinLayer.QueueFree();
        _playerMenuSkinLayer = null;
    }

    private void ShowPlayerMenuByName(string name, Vector2 at)
    {
        foreach (var (id, e) in _ents)
        {
            if (e.IsNpc || e.Dead || id == _myId) continue;
            if (!string.Equals(e.Name, name, System.StringComparison.OrdinalIgnoreCase)) continue;
            ShowPlayerMenu(id, e, at);
            return;
        }

        _playerMenuId = -1;
        _playerMenuName = name;
        PresentPlayerMenu(new System.Collections.Generic.List<PlayerMenuAction>
        {
            PlayerMenuAction.Whisper, PlayerMenuAction.AddFriend, PlayerMenuAction.RequestParty,
            PlayerMenuAction.UserInformation, PlayerMenuAction.EquipmentView, PlayerMenuAction.Report, PlayerMenuAction.CopyName,
        }, at);
    }

    private bool HasNearbyPlayerForUserInfo()
    {
        if (!_worldReady || _self == null || _selfDead) return false;
        foreach (var (id, e) in _ents)
        {
            if (id == _myId || e.IsNpc || e.Dead) continue;
            if (FlatDistance(_self.Position, e.Body.Position) <= TradeRange)
                return true;
        }
        return false;
    }

    private bool TryFindInteractionPlayer(bool merchantOnly, out int bestId, out Ent? best)
    {
        bestId = -1;
        best = null;
        if (!_worldReady || _self == null || _selfDead) return false;
        bool Eligible(int id, Ent e) => id != _myId && !e.IsNpc && !e.Dead
            && (!merchantOnly || _stalls.ContainsKey(id))
            && FlatDistance(_self.Position, e.Body.Position) <= TradeRange;
        if (_ents.TryGetValue(_selectedId, out var selected) && Eligible(_selectedId, selected))
        {
            bestId = _selectedId;
            best = selected;
            return true;
        }
        float distance = TradeRange;
        foreach (var (id, e) in _ents)
        {
            if (!Eligible(id, e)) continue;
            float d = FlatDistance(_self.Position, e.Body.Position);
            if (d > distance) continue;
            distance = d;
            bestId = id;
            best = e;
        }
        return best != null;
    }

    private void OpenNearestPlayerMenu(Vector2 at)
    {
        if (TryFindInteractionPlayer(false, out int id, out var target) && target != null)
            ShowPlayerMenu(id, target, at);
    }

    private void OnPlayerMenuAction(long actionId)
    {
        if (_playerMenuName.Length == 0) return;
        string name = _playerMenuName;
        int id = _playerMenuId;

        switch ((PlayerMenuAction)actionId)
        {
            case PlayerMenuAction.RequestParty:
                InvitePlayerToParty(name);
                break;

            case PlayerMenuAction.Report:
                OpenReportAgainst(name);
                break;

            case PlayerMenuAction.UserTrade:
                RequestTradeWith(id, name);
                break;

            case PlayerMenuAction.Whisper:
                OpenWhisperWith(name);
                break;

            case PlayerMenuAction.AddFriend:
                RequestFriend(name);
                break;

            case PlayerMenuAction.UserInformation:
                RequestUserInformation(name);
                break;

            case PlayerMenuAction.Duel:
                ChallengeByName(name);
                break;

            case PlayerMenuAction.EquipmentView:
                RequestEquipmentView(name);
                break;

            case PlayerMenuAction.ClanInvite:
                Net.I.SendClanInvite(id);
                CombatNotice($"Inviting {name} to the clan…");
                break;

            case PlayerMenuAction.CopyName:
                DisplayServer.ClipboardSet(name);
                CombatNotice($"Copied {name}.");
                break;

            case PlayerMenuAction.ClanBattle:
                Net.I.SendClanBattleChallenge(id);
                break;

            case PlayerMenuAction.ClanSurrender:
                Notice.Confirm(this, "Surrender the clan battle?", "Surrender", "Cancel", Net.I.SendClanBattleSurrender);
                break;
        }
    }

    private void InvitePlayerToParty(string name)
    {
        if (InParty && !AmLeader)
        {
            CombatNotice("Only the party leader can invite.");
            return;
        }
        if (InParty && PartyMembers.Count >= PartyMaxMembers)
        {
            CombatNotice("Your party is full.");
            return;
        }

        if (InParty) Net.I.SendPartyInvite(name);
        else Net.I.SendPartyCreate(name);
        CombatNotice($"Inviting {name} to your party…");
    }

    private void RequestTradeWith(int charId, string name)
    {
        if (_exShown || _exWaiting) { CombatNotice("You are already trading."); return; }
        if (_selfDead) return;

        if (!_ents.TryGetValue(charId, out var e) || e.Dead)
        {
            CombatNotice($"{name} is no longer nearby.");
            return;
        }

        if (e.Body.Position.DistanceTo(_self.Position) > TradeRange)
        {
            CombatNotice($"{name} is too far away to trade.");
            return;
        }

        BeginTradeRequest(charId, name);
    }

    private void OpenReportAgainst(string name)
    {
        if (!_reportShown) ToggleReport();
        _reportTargetEdit.Text = name;
        _reportReasonEdit.GrabFocus();
    }
}
