using System;
using System.Collections.Generic;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;
using LibreKO.Plugins;

namespace LibreKO;

public partial class World
{
    private const string RankMarkArt = "res://assets/ui/daily_rank/marks/";
    private const string ClanPodiumArt = "res://assets/ui/daily_rank/medal_{0}.png";
    private const int UserInfoLayer = 78;
    private const int UserInfoWidth = 440;
    private const float UserInfoMarkSize = 66f;
    private const float RankRibbonWidth = 45f;
    private const float RankRibbonHeight = 17f;
    private const int RankRibbonFont = 9;
    private const int UserInfoSummaryColumns = 9;
    private const float UserInfoKeyWidth = 120f;
    private const float UserInfoPlaceWidth = 56f;
    private const int ClanPlaceFont = 24;

    private CanvasLayer _userInfoLayer = null!;
    private HudWindow _userInfoPanel = null!;
    private UserInfoReply? _userInfo;
    private GameUserCard? _userCard;
    private bool _userInfoShown;
    private readonly Dictionary<string, Texture2D?> _rankRibbons = new();
    private TextureRect _uiClanMark = null!;
    private TextureRect _uiClanPodium = null!;
    private Label _uiClanPlace = null!;
    private Label _uiClanName = null!, _uiClanGrade = null!, _uiClanChief = null!;
    private Label _uiName = null!, _uiLevel = null!, _uiJob = null!, _uiLoyalty = null!, _uiState = null!, _uiRelation = null!;
    private GridContainer _uiSummary = null!;
    private VBoxContainer _uiRows = null!;

    private void UserInfoInit()
    {
        _userInfoLayer = new CanvasLayer { Layer = UserInfoLayer };
        AddChild(_userInfoLayer);
        _userInfoPanel = new HudWindow("userinfo", "Character Info", new Vector2(320, 120), UserInfoWidth) { Visible = false };
        _userInfoPanel.Closed += CloseUserInfo;
        _userInfoLayer.AddChild(_userInfoPanel);
        BuildUserInfoBody(_userInfoPanel.Body);
        Net.I.UserInformationEvent += OnUserInformation;
        Net.I.UserInformationMissingEvent += OnUserInformationMissing;
        PluginHost.Game.AttachUserInfo(new PluginUserInfoBridge(this));
    }

    private void UserInfoDispose()
    {
        Net.I.UserInformationEvent -= OnUserInformation;
        Net.I.UserInformationMissingEvent -= OnUserInformationMissing;
    }

    private void BuildUserInfoBody(VBoxContainer root)
    {
        root.AddThemeConstantOverride("separation", 8);

        var clan = new HBoxContainer();
        clan.AddThemeConstantOverride("separation", 10);
        root.AddChild(clan);
        var podium = new Panel { CustomMinimumSize = new Vector2(UserInfoMarkSize, UserInfoMarkSize) };
        podium.AddThemeStyleboxOverride("panel", UiTheme.Inset());
        _uiClanPodium = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _uiClanPodium.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        podium.AddChild(_uiClanPodium);
        _uiClanPlace = UiTheme.Text("", ClanPlaceFont, UiTheme.GoldBright, HorizontalAlignment.Center);
        _uiClanPlace.VerticalAlignment = VerticalAlignment.Center;
        _uiClanPlace.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        podium.AddChild(_uiClanPlace);
        clan.AddChild(podium);
        var markFrame = new Panel { CustomMinimumSize = new Vector2(UserInfoMarkSize, UserInfoMarkSize) };
        markFrame.AddThemeStyleboxOverride("panel", UiTheme.Inset());
        _uiClanMark = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _uiClanMark.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        markFrame.AddChild(_uiClanMark);
        clan.AddChild(markFrame);
        var clanFacts = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        clanFacts.AddThemeConstantOverride("v_separation", 4);
        clan.AddChild(clanFacts);
        _uiClanName = UserInfoFact(clanFacts, "Clan:");
        _uiClanGrade = UserInfoFact(clanFacts, "Grade:");
        _uiClanChief = UserInfoFact(clanFacts, "Leader:");

        root.AddChild(UiTheme.Rule());

        var facts = new GridContainer { Columns = 2 };
        facts.AddThemeConstantOverride("v_separation", 5);
        root.AddChild(facts);
        _uiName = UserInfoFact(facts, "Character name");
        _uiLevel = UserInfoFact(facts, "Level");
        _uiJob = UserInfoFact(facts, "Job");
        _uiLoyalty = UserInfoFact(facts, "Cont/Ladder");
        _uiState = UserInfoFact(facts, "Status");
        _uiRelation = UserInfoFact(facts, "Relation");

        root.AddChild(UiTheme.Rule());

        _uiSummary = new GridContainer { Columns = UserInfoSummaryColumns };
        _uiSummary.AddThemeConstantOverride("h_separation", 0);
        _uiSummary.AddThemeConstantOverride("v_separation", 0);
        root.AddChild(_uiSummary);

        _uiRows = new VBoxContainer();
        _uiRows.AddThemeConstantOverride("separation", 3);
        root.AddChild(_uiRows);

        var footer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        root.AddChild(footer);
        var ok = new Button { Text = "Confirm", FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(120, 0) };
        ok.Pressed += CloseUserInfo;
        footer.AddChild(ok);
    }

    private static Label UserInfoFact(GridContainer grid, string caption)
    {
        var key = UiTheme.Text(caption, 12, UiTheme.Gold);
        key.CustomMinimumSize = new Vector2(UserInfoKeyWidth, 0);
        grid.AddChild(key);
        var value = UiTheme.Text("", 13, UiTheme.TextHi);
        value.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        grid.AddChild(value);
        return value;
    }

    private void RequestUserInformation(string name)
    {
        if (name.Length == 0) return;
        Net.I.SendUserInformationRequest(name);
    }

    private void CloseUserInfo()
    {
        _userInfoShown = false;
        _userInfoPanel.Visible = false;
        PluginNotifyUserInfo();
    }

    private void PluginNotifyUserInfo() => Callable.From(PluginHost.Game.RaiseUserInfo).CallDeferred();

    private void OnUserInformationMissing()
    {
        CombatNotice(ItemData.Text(UserInfoCard.NoUserText, "No available user"));
    }

    private void OnUserInformation(UserInfoReply info)
    {
        _userInfo = info;
        _userCard = BuildUserCard(info);
        FillUserInfo(_userCard);
        _userInfoPanel.Visible = true;
        _userInfoShown = true;
        PluginNotifyUserInfo();
    }

    private void RefreshUserInfoMark(int clanId)
    {
        if (_userInfo == null || _userInfo.ClanId != clanId) return;
        _userCard = BuildUserCard(_userInfo);
        _uiClanMark.Texture = _userCard.ClanMark;
        PluginNotifyUserInfo();
    }

    private GameUserCard BuildUserCard(UserInfoReply info)
    {
        Net.NearbyPlayer? nearby = null;
        foreach (var p in _nearbyListed)
            if (string.Equals(p.Name, info.Name, StringComparison.OrdinalIgnoreCase)) { nearby = p; break; }
        int nation = nearby?.Nation ?? NationInView(info.Name);
        string state = ItemData.Text(UserInfoCard.StateText(nearby?.State ?? 0), "Online");
        if (info.InParty) state = $"{state} / {ItemData.Text(UserInfoCard.PartyText, "Party")}";

        bool friend = _friends.Exists(f => string.Equals(f.Name, info.Name, StringComparison.OrdinalIgnoreCase));
        bool sameClan = info.ClanId != 0 && MyClan.InClan && info.ClanId == MyClan.ClanId;
        var (gradeText, gradeNumber) = UserInfoCard.ClanGrade(info.ClanFlag, info.ClanGrade);
        string grade = gradeText == 0
            ? info.ClanFlag == 0 ? "" : "Unknown Grade"
            : ItemData.Text(gradeText, "%d").Replace("%d", gradeNumber.ToString());
        int classText = UserInfoCard.ClassText(info.Class);

        var summary = new List<GameRankRibbon>();
        foreach (var cell in UserInfoCard.Summary(info.Marks))
            summary.Add(new GameRankRibbon(RankRibbon(UserInfoCard.Ribbon(cell.Type, cell.Tier, cell.Count)),
                cell.Count.ToString(), MarkTooltipText(cell.Type)));

        var rows = new List<GameUserRankRow>();
        var ranks = UserInfoCard.Rows(info.RankPlaces);
        for (int r = 0; r < ranks.Count; r++)
        {
            var tiers = new GameRankRibbon[UserInfoCard.Tiers];
            if (r < info.Marks.Count)
            {
                var mark = info.Marks[r];
                for (int t = 0; t < tiers.Length; t++)
                {
                    int count = mark.At(t);
                    bool special = mark.Type >= UserInfoCard.SpecialMarkFirst;
                    tiers[t] = special && t < UserInfoCard.Tiers - 1
                        ? default
                        : new GameRankRibbon(RankRibbon(UserInfoCard.Ribbon(mark.Type, t, count)),
                            count > 0 ? count.ToString() : "", MarkTooltipText(mark.Type));
                }
            }
            int place = ranks[r].Place;
            rows.Add(new GameUserRankRow(DailyRankTitle(ranks[r].Board), UserInfoCard.PlaceText(place),
                UserInfoCard.PlaceColour(place), tiers));
        }

        return new GameUserCard(
            info.Name,
            UserInfoCard.NameColour(nation),
            UserInfoCard.LevelLine(info.Level, info.RebirthLevel),
            classText > 0 ? ItemData.Text(classText, CharacterClassCatalog.DisplayName(info.Class)) : "",
            UserInfoCard.Thousands(info.Loyalty),
            UserInfoCard.Thousands(info.MonthlyLoyalty),
            state,
            UserInfoCard.Relation(friend, sameClan),
            info.ClanId != 0,
            info.ClanName,
            grade,
            info.ClanChief,
            info.ClanId != 0 ? ClanMark(info.ClanId, info.ClanMarkVersion) : null,
            info.ClanRanking,
            summary,
            rows);
    }

    private int NationInView(string name)
    {
        foreach (var (id, entity) in _ents)
            if (!entity.IsNpc && id != _myId && string.Equals(entity.Name, name, StringComparison.OrdinalIgnoreCase))
                return entity.Nation;
        return 0;
    }

    private string DailyRankTitle(int board) => _drBoards.Find(b => b.Id == board)?.Title ?? "";

    private static string MarkTooltipText(int type)
    {
        int text = UserInfoCard.MarkTooltip(type);
        return text > 0 ? ItemData.Text(text, "") : "";
    }

    private Texture2D? RankRibbon(string name)
    {
        if (_rankRibbons.TryGetValue(name, out var cached)) return cached;
        string path = RankMarkArt + name + ".png";
        var texture = ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
        _rankRibbons[name] = texture;
        return texture;
    }

    private static string RelationText(UserRelation relation) => relation switch
    {
        UserRelation.Friend => "Friend",
        UserRelation.Clan => "Guild",
        UserRelation.Both => "Friend / Guild",
        _ => "-",
    };

    private void FillUserInfo(GameUserCard card)
    {
        _uiClanName.Text = card.ClanName;
        _uiClanGrade.Text = card.ClanGrade;
        _uiClanChief.Text = card.ClanChief;
        _uiClanMark.Texture = card.ClanMark;
        bool podium = card.InClan && card.ClanRanking is >= 1 and <= UserInfoCard.PodiumPlaces;
        string podiumPath = string.Format(ClanPodiumArt, card.ClanRanking);
        _uiClanPodium.Texture = podium && ResourceLoader.Exists(podiumPath) ? ResourceLoader.Load<Texture2D>(podiumPath) : null;
        _uiClanPlace.Text = card.InClan && card.ClanRanking > UserInfoCard.PodiumPlaces ? card.ClanRanking.ToString() : "";

        _uiName.Text = card.Name;
        _uiName.AddThemeColorOverride("font_color", card.NameColour ?? UiTheme.TextHi);
        _uiLevel.Text = card.Level;
        _uiJob.Text = card.Job;
        _uiLoyalty.Text = $"{card.Loyalty} / {card.MonthlyLoyalty}";
        _uiState.Text = card.State;
        _uiRelation.Text = RelationText(card.Relation);

        foreach (var child in _uiSummary.GetChildren()) child.QueueFree();
        foreach (var cell in card.Summary) _uiSummary.AddChild(RibbonCell(cell));
        _uiSummary.Visible = card.Summary.Count > 0;

        foreach (var child in _uiRows.GetChildren()) child.QueueFree();
        foreach (var row in card.Rows)
        {
            var line = new HBoxContainer();
            line.AddThemeConstantOverride("separation", 6);
            var title = UiTheme.Text(row.Title, 12, UiTheme.TextHi);
            title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            line.AddChild(title);
            var place = UiTheme.Text(row.Place, 12, row.PlaceColour, HorizontalAlignment.Center);
            place.CustomMinimumSize = new Vector2(UserInfoPlaceWidth, 0);
            line.AddChild(place);
            foreach (var tier in row.Tiers) line.AddChild(RibbonCell(tier));
            _uiRows.AddChild(line);
        }
    }

    private static Control RibbonCell(GameRankRibbon ribbon)
    {
        var cell = new Control { CustomMinimumSize = new Vector2(RankRibbonWidth, RankRibbonHeight), TooltipText = ribbon.Tooltip };
        if (ribbon.Ribbon == null) return cell;
        var image = new TextureRect
        {
            Texture = ribbon.Ribbon,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        image.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        cell.AddChild(image);
        var count = UiTheme.Text(ribbon.Count, RankRibbonFont, UiTheme.GoldBright, HorizontalAlignment.Center);
        count.VerticalAlignment = VerticalAlignment.Center;
        count.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        count.MouseFilter = Control.MouseFilterEnum.Ignore;
        count.AddThemeConstantOverride("outline_size", 3);
        count.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.85f));
        cell.AddChild(count);
        return cell;
    }

    private static string NationName(int nation) => nation switch
    {
        Nations.Karus => "Karus",
        Nations.ElMorad => "El Morad",
        _ => "Neutral",
    };

    private static Color NationColor(int nation) => nation switch
    {
        Nations.Karus => new Color("d08a4a"),
        Nations.ElMorad => new Color("5a9ad0"),
        _ => UiTheme.TextLo,
    };

    private sealed class PluginUserInfoBridge : IGameUserInfo
    {
        private readonly World _w;

        public PluginUserInfoBridge(World w) => _w = w;

        public bool IsOpen => _w._userInfoShown;
        public GameUserCard? Card => _w._userCard;
        public void Close() => _w.CloseUserInfo();

        event Action? IGameUserInfo.Changed { add { } remove { } }
    }
}
