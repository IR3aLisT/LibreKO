using System.Collections.Generic;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const int WishMessageWidth = 320;
    private const int WishRequestListWidth = 400;
    private const string WishSettingsPath = "user://clanboards.cfg";
    private const string WishSettingsSection = "wish";
    private const string WishIgnoreKey = "ignore_notice";
    private static readonly Color WishOnlineColour = new(0.45f, 0.85f, 0.45f);

    private sealed class WishBoardRow
    {
        public PanelContainer Panel = null!;
        public Label Online = null!;
        public Label Name = null!;
        public Label Text = null!;
        public Button Details = null!;
    }

    private HudWindow? _wbWindow;
    private readonly WishBoardRow[] _wbRows = new WishBoardRow[ClanBoards.RowsPerPage];
    private List<WishPost> _wbPosts = new();
    private WishStatus _wbStatus = new(ClanBoards.RoleSeeker, "", 0);
    private int _wbPage = 1;
    private bool _wbIgnoreNotice;
    private Label _wbPageLabel = null!, _wbStatusText = null!;
    private Button _wbPrev = null!, _wbNext = null!, _wbCancelRequest = null!, _wbRequestList = null!;
    private CheckButton _wbIgnore = null!;
    private HBoxContainer _wbSeekerRow = null!;

    private HudWindow? _wmWindow;
    private Label _wmName = null!, _wmDetails = null!, _wmText = null!, _wmCounter = null!, _wmNote = null!;
    private LineEdit _wmAge = null!;
    private TextEdit _wmEdit = null!;
    private HBoxContainer _wmButtons = null!;

    private HudWindow? _wrWindow;
    private VBoxContainer _wrRows = null!;
    private List<WishRequest> _wrRequests = new();

    private void WishBoardInit()
    {
        Net.I.WishPostsEvent += OnWishPosts;
        Net.I.WishStatusEvent += OnWishStatus;
        Net.I.WishTooSoonEvent += OnWishTooSoon;
        Net.I.WishRequestsEvent += OnWishRequests;
        Net.I.WishIntroResultEvent += OnWishIntroResult;
        Net.I.WishRequestResultEvent += OnWishRequestResult;
        Net.I.WishPromoteEvent += OnWishPromote;
        var cfg = new ConfigFile();
        if (cfg.Load(WishSettingsPath) == Error.Ok)
            _wbIgnoreNotice = cfg.GetValue(WishSettingsSection, WishIgnoreKey, false).AsBool();
    }

    private void WishBoardDispose()
    {
        Net.I.WishPostsEvent -= OnWishPosts;
        Net.I.WishStatusEvent -= OnWishStatus;
        Net.I.WishTooSoonEvent -= OnWishTooSoon;
        Net.I.WishRequestsEvent -= OnWishRequests;
        Net.I.WishIntroResultEvent -= OnWishIntroResult;
        Net.I.WishRequestResultEvent -= OnWishRequestResult;
        Net.I.WishPromoteEvent -= OnWishPromote;
    }

    private void OpenWishBoard()
    {
        if (_cbWindow != null) _cbWindow.Visible = false;
        _wbPage = 1;
        Net.I.SendWishPage(_wbPage, refresh: false);
    }

    private void CloseWishBoard()
    {
        if (_wbWindow != null) _wbWindow.Visible = false;
        if (_wmWindow != null) _wmWindow.Visible = false;
        if (_wrWindow != null) _wrWindow.Visible = false;
    }

    private void OnWishStatus(WishStatus status)
    {
        _wbStatus = status;
        _wbPage = 1;
        _wbPosts = new List<WishPost>();
        ShowWishBoard();
    }

    private void OnWishPosts(List<WishPost> posts)
    {
        _wbPosts = posts;
        ShowWishBoard();
    }

    private void ShowWishBoard()
    {
        _wbWindow ??= BuildWishBoard();
        RefreshWishBoard();
        ShowOnTop(_wbWindow);
    }

    private void OnWishTooSoon() =>
        Notice.Show(this, ItemData.Text(ClanBoards.TextRefreshTooSoon, "You can only refresh once every 30 seconds."));

    private HudWindow BuildWishBoard()
    {
        var window = new HudWindow("wish_board", "Bulletin Board for Join Request", new Vector2(250, 125), bodyMinWidth: ClanBoardWidth) { Visible = false };
        window.Closed += CloseWishBoard;
        _cbLayer.AddChild(window);
        var body = window.Body;

        var head = new HBoxContainer();
        body.AddChild(head);
        var intro = UiTheme.Text("You can request to join a clan as well as introduce yourself through this Bulletin Board. "
            + "Those who have authorities to accept new clan members can send the accept request.", 12, UiTheme.TextLo);
        intro.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        intro.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        head.AddChild(intro);
        head.AddChild(ClanBoardButton("Clan Promotional Bulletin Board", () => { CloseWishBoard(); OpenClanBoard(); }));

        var tools = new HBoxContainer();
        body.AddChild(tools);
        _wbIgnore = new CheckButton { Text = "Rejection of Applicant", ButtonPressed = _wbIgnoreNotice, FocusMode = Control.FocusModeEnum.None };
        _wbIgnore.AddThemeFontSizeOverride("font_size", 12);
        _wbIgnore.Toggled += on => SaveWishIgnore(on);
        tools.AddChild(_wbIgnore);
        tools.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        tools.AddChild(ClanBoardButton("Refresh", () => Net.I.SendWishPage(_wbPage, refresh: true)));

        var captions = new HBoxContainer();
        body.AddChild(captions);
        captions.AddChild(new Control { CustomMinimumSize = new Vector2(ClanBoardMarkSize + 8, 0) });
        var idCaption = UiTheme.Text("ID", 12, UiTheme.Gold);
        idCaption.CustomMinimumSize = new Vector2(ClanBoardNameWidth, 0);
        captions.AddChild(idCaption);
        captions.AddChild(UiTheme.Text("Introduction", 12, UiTheme.Gold));

        var rows = new VBoxContainer();
        rows.AddThemeConstantOverride("separation", 2);
        body.AddChild(rows);
        for (int i = 0; i < ClanBoards.RowsPerPage; i++)
        {
            _wbRows[i] = BuildWishRow(i);
            rows.AddChild(_wbRows[i].Panel);
        }

        var footer = new HBoxContainer();
        body.AddChild(footer);
        _wbSeekerRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _wbSeekerRow.AddThemeConstantOverride("separation", 6);
        footer.AddChild(_wbSeekerRow);
        _wbSeekerRow.AddChild(ClanBoardButton("Promote Self Introduction", Net.I.SendWishPromote));
        _wbSeekerRow.AddChild(ClanBoardButton("Write Self Introduction", OpenWishWrite));
        _wbSeekerRow.AddChild(ClanBoardButton("Delete Notes", () => { if (_wrWindow != null) _wrWindow.Visible = false; Net.I.SendWishIntroDelete(); }));
        footer.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        _wbPrev = ClanBoardButton("<", () => { if (_wbPage > 1) Net.I.SendWishPage(--_wbPage, refresh: false); });
        footer.AddChild(_wbPrev);
        _wbPageLabel = UiTheme.Text("1", 12, UiTheme.TextHi, HorizontalAlignment.Center);
        _wbPageLabel.CustomMinimumSize = new Vector2(32, 0);
        footer.AddChild(_wbPageLabel);
        _wbNext = ClanBoardButton(">", () => Net.I.SendWishPage(++_wbPage, refresh: false));
        footer.AddChild(_wbNext);

        body.AddChild(new HSeparator());
        var status = new HBoxContainer();
        status.AddThemeConstantOverride("separation", 8);
        body.AddChild(status);
        _wbStatusText = UiTheme.Text("", 12, UiTheme.TextLo);
        _wbStatusText.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        status.AddChild(_wbStatusText);
        _wbCancelRequest = ClanBoardButton("Cancel Request", () => Net.I.SendWishRequestCancel(MyClan.ClanId));
        status.AddChild(_wbCancelRequest);
        _wbRequestList = ClanBoardButton("List of Requested Applicant", Net.I.SendWishRequestList);
        status.AddChild(_wbRequestList);
        return window;
    }

    private WishBoardRow BuildWishRow(int index)
    {
        var row = new WishBoardRow
        {
            Panel = new PanelContainer { CustomMinimumSize = new Vector2(0, ClanBoardRowHeight) },
            Online = UiTheme.Text("●", 12, UiTheme.TextDim, HorizontalAlignment.Center),
            Name = UiTheme.Text("", 12, UiTheme.TextHi),
            Text = UiTheme.Text("", 12, UiTheme.TextLo),
        };
        row.Panel.AddThemeStyleboxOverride("panel", UiTheme.Row());
        var line = new HBoxContainer();
        line.AddThemeConstantOverride("separation", 8);
        row.Panel.AddChild(line);
        row.Online.CustomMinimumSize = new Vector2(ClanBoardMarkSize, 0);
        line.AddChild(row.Online);
        row.Name.CustomMinimumSize = new Vector2(ClanBoardNameWidth, 0);
        row.Name.ClipText = true;
        line.AddChild(row.Name);
        row.Text.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.Text.ClipText = true;
        row.Text.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        line.AddChild(row.Text);
        row.Details = ClanBoardButton("Details", () => { if (index < _wbPosts.Count) OpenWishView(_wbPosts[index]); });
        line.AddChild(row.Details);
        return row;
    }

    private void RefreshWishBoard()
    {
        for (int i = 0; i < ClanBoards.RowsPerPage; i++)
        {
            var row = _wbRows[i];
            bool used = i < _wbPosts.Count;
            row.Online.Visible = row.Details.Visible = used;
            row.Online.AddThemeColorOverride("font_color", used && _wbPosts[i].Online ? WishOnlineColour : UiTheme.TextDim);
            row.Name.Text = used ? _wbPosts[i].Name : "";
            row.Text.Text = used ? _wbPosts[i].Introduction : "";
        }
        _wbPageLabel.Text = _wbPage.ToString();
        _wbPrev.Disabled = _wbPage <= 1;
        _wbNext.Disabled = !ClanBoards.CanPageForward(_wbPosts.Count, ClanBoards.RowsPerPage);
        _wbSeekerRow.Visible = _wbStatus.Seeker;
        _wbIgnore.Visible = !_wbStatus.Seeker;
        _wbCancelRequest.Visible = _wbStatus.Recruiter && _wbStatus.RequestedName.Length > 0;
        _wbRequestList.Visible = _wbStatus.Seeker && _wbStatus.Received > 0;
        _wbStatusText.Text = _wbStatus switch
        {
            { Recruiter: true, RequestedName.Length: > 0 } => TextTemplate.Fill(ItemData.Text(ClanBoards.TextRequestSentTo, "A request has been sent to %s."), _wbStatus.RequestedName),
            { Recruiter: true } => ItemData.Text(ClanBoards.TextNoRequestSent, "You have not sent the request yet."),
            { Member: true } => ItemData.Text(ClanBoards.TextMembersCannotRequest, "General Clan members cannot send membership request."),
            { Received: > 0 } => TextTemplate.Fill(ItemData.Text(ClanBoards.TextRequestsReceived, "Currently, %d requests are received."), _wbStatus.Received),
            _ => ItemData.Text(ClanBoards.TextNoRequests, "You have not received requests."),
        };
    }

    private void SaveWishIgnore(bool on)
    {
        _wbIgnoreNotice = on;
        var cfg = new ConfigFile();
        cfg.Load(WishSettingsPath);
        cfg.SetValue(WishSettingsSection, WishIgnoreKey, on);
        cfg.Save(WishSettingsPath);
    }

    private HudWindow BuildWishMessage()
    {
        var window = new HudWindow("wish_message", "Self Introduction", new Vector2(830, 140), bodyMinWidth: WishMessageWidth) { Visible = false };
        window.Closed += () => { window.Visible = false; if (_wbWindow != null) ShowOnTop(_wbWindow); };
        _cbLayer.AddChild(window);
        var body = window.Body;
        _wmName = UiTheme.Text("", 15, UiTheme.GoldBright);
        body.AddChild(_wmName);
        _wmDetails = UiTheme.Text("", 12, UiTheme.TextLo);
        body.AddChild(_wmDetails);
        var ageRow = new HBoxContainer();
        body.AddChild(ageRow);
        ageRow.AddChild(UiTheme.Text("Age", 12, UiTheme.TextLo));
        _wmAge = new LineEdit { MaxLength = 2, CustomMinimumSize = new Vector2(48, 0) };
        ageRow.AddChild(_wmAge);
        _wmText = UiTheme.Text("", 13, UiTheme.TextHi);
        _wmText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _wmText.CustomMinimumSize = new Vector2(WishMessageWidth, 0);
        body.AddChild(_wmText);
        _wmEdit = new TextEdit
        {
            CustomMinimumSize = new Vector2(WishMessageWidth, ClanMessageEditHeight),
            WrapMode = TextEdit.LineWrappingMode.Boundary,
        };
        _wmEdit.TextChanged += () => _wmCounter.Text = $"{_wmEdit.Text.Length}/{ClanBoards.MaxIntroBytes}";
        body.AddChild(_wmEdit);
        _wmCounter = UiTheme.Text("", 11, UiTheme.TextDim, HorizontalAlignment.Right);
        body.AddChild(_wmCounter);
        _wmNote = UiTheme.Text("Messages created are automatically deleted after 7 days.", 11, UiTheme.TextDim);
        body.AddChild(_wmNote);
        _wmButtons = new HBoxContainer();
        _wmButtons.AddThemeConstantOverride("separation", 6);
        body.AddChild(_wmButtons);
        return window;
    }

    private void ResetWishButtons()
    {
        foreach (var child in _wmButtons.GetChildren()) { _wmButtons.RemoveChild(child); child.QueueFree(); }
    }

    private void OpenWishWrite()
    {
        string self = Net.I.LastEnter.Name;
        if (_wbPosts.Exists(post => string.Equals(post.Name, self, System.StringComparison.OrdinalIgnoreCase)))
        {
            Notice.Show(this, ItemData.Text(ClanBoards.TextAlreadyRegistered, "Already Registered"));
            return;
        }
        _wmWindow ??= BuildWishMessage();
        _wmName.Text = self;
        _wmDetails.Text = $"Lv {Sheet.Level}  ·  {ClassName(_selfClass)}";
        _wmAge.GetParent<Control>().Visible = true;
        _wmAge.Text = "";
        _wmText.Visible = false;
        _wmEdit.Visible = _wmCounter.Visible = _wmNote.Visible = true;
        _wmEdit.Text = "";
        _wmCounter.Text = $"0/{ClanBoards.MaxIntroBytes}";
        ResetWishButtons();
        _wmButtons.AddChild(ClanBoardButton("Confirmed", SendWishIntro));
        ShowOnTop(_wmWindow);
    }

    private void SendWishIntro()
    {
        if (!ClanBoards.AgeValid(_wmAge.Text))
        {
            Notice.Show(this, ItemData.Text(ClanBoards.TextAgeWrong, "Please enter the age correctly."));
            return;
        }
        string text = _wmEdit.Text.Trim();
        if (text.Length == 0 || text.Length > ClanBoards.MaxIntroBytes)
        {
            Notice.Show(this, ItemData.Text(ClanBoards.TextOverLimit, "Length of message is over limit"));
            return;
        }
        if (!_wbStatus.Seeker) return;
        Net.I.SendWishIntroRegister(int.Parse(_wmAge.Text), text);
    }

    private void OpenWishView(WishPost post)
    {
        _wmWindow ??= BuildWishMessage();
        _wmName.Text = post.Name;
        _wmDetails.Text = $"Lv {post.Level}  ·  {ClassName(post.Class)}  ·  Age {post.Age}  ·  Recent Registration Date {post.Registered}";
        _wmAge.GetParent<Control>().Visible = false;
        _wmText.Visible = true;
        _wmText.Text = post.Introduction;
        _wmEdit.Visible = _wmCounter.Visible = _wmNote.Visible = false;
        ResetWishButtons();
        if (_wbStatus.Recruiter)
        {
            _wmButtons.AddChild(ClanBoardButton("Request to Join", () => RequestWishJoin(post)));
            _wmButtons.AddChild(ClanBoardButton("Write a Note", () => { OpenMailCompose(); _mailTo.Text = post.Name; }));
            _wmButtons.AddChild(ClanBoardButton("Request for Consultation", () => Net.I.SendPlayerConsultation(post.Name)));
            _wmButtons.AddChild(ClanBoardButton("OK", () => _wmWindow.Visible = false));
        }
        else
        {
            _wmButtons.AddChild(ClanBoardButton("Close", () => _wmWindow.Visible = false));
        }
        ShowOnTop(_wmWindow);
    }

    private void RequestWishJoin(WishPost post)
    {
        if (post.TargetId != ClanBoards.NoTarget)
        {
            Net.I.SendWishOnlineRequest(post.TargetId, post.Name);
            _wmWindow!.Visible = false;
            return;
        }
        _wmText.Visible = false;
        _wmEdit.Visible = _wmCounter.Visible = true;
        _wmEdit.Text = "";
        ResetWishButtons();
        _wmButtons.AddChild(ClanBoardButton("Confirmed", () => Net.I.SendWishOfflineRequest(MyClan.ClanId, post.Name, _wmEdit.Text.Trim())));
    }

    private void OnWishIntroResult(int action, int code)
    {
        if (ClanBoards.IntroResultText(action, code) is var text and > 0) Notice.Show(this, ItemData.Text(text, ""));
        if (action == ClanBoards.KindDelete && code == 1 && _wmWindow != null) _wmWindow.Visible = false;
        if (action == ClanBoards.KindRegister && code == 1 && _wmWindow != null) _wmWindow.Visible = false;
        Net.I.SendWishPage(_wbPage, refresh: false);
    }

    private void OnWishRequestResult(int action, int code)
    {
        if (ClanBoards.RequestResultText(action, code) is var text and > 0) Notice.Show(this, ItemData.Text(text, ""));
        if (action == ClanBoards.KindRegister && code == 1 && _wmWindow != null) _wmWindow.Visible = false;
        if (action == ClanBoards.KindDelete && _wrWindow is { Visible: true } && _wbStatus.Seeker) Net.I.SendWishRequestList();
        Net.I.SendWishPage(_wbPage, refresh: false);
    }

    private void OnWishPromote(int code, string name)
    {
        if (name.Length > 0)
        {
            if (_wbIgnoreNotice && MyClan.InClan) return;
            CombatNotice(TextTemplate.Fill(ItemData.Text(ClanBoards.TextSeekingClan,
                "%s is seeking a clan. Please check the Bulletin Board for Join Request."), name));
            return;
        }
        if (ClanBoards.WishPromoteText(code) is var text and > 0) Notice.Show(this, ItemData.Text(text, ""));
    }

    private void OnWishRequests(List<WishRequest> requests)
    {
        _wrRequests = requests;
        if (_wmWindow != null) _wmWindow.Visible = false;
        _wrWindow ??= BuildWishRequestList();
        foreach (var child in _wrRows.GetChildren()) { _wrRows.RemoveChild(child); child.QueueFree(); }
        foreach (var request in requests)
        {
            var panel = new PanelContainer();
            panel.AddThemeStyleboxOverride("panel", UiTheme.Row());
            var line = new HBoxContainer();
            line.AddThemeConstantOverride("separation", 8);
            panel.AddChild(line);
            var leader = UiTheme.Text(request.Leader, 12, UiTheme.TextHi);
            leader.CustomMinimumSize = new Vector2(ClanBoardNameWidth - 30, 0);
            line.AddChild(leader);
            var clan = UiTheme.Text(request.ClanName, 12, UiTheme.TextLo);
            clan.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            line.AddChild(clan);
            var captured = request;
            line.AddChild(ClanBoardButton("Message", () => OpenClanMessage(ClanMessageMode.ViewWishRequest, null, request: captured)));
            line.AddChild(ClanBoardButton("Delete", () => Net.I.SendWishRequestCancel(captured.ClanId)));
            _wrRows.AddChild(panel);
        }
        if (requests.Count == 0) _wrRows.AddChild(UiTheme.Text("You have not received requests.", 12, UiTheme.TextLo));
        ShowOnTop(_wrWindow);
    }

    private HudWindow BuildWishRequestList()
    {
        var window = new HudWindow("wish_requests", "List of Requested Applicant", new Vector2(270, 150), bodyMinWidth: WishRequestListWidth) { Visible = false };
        window.Closed += () => { window.Visible = false; if (_wbWindow != null) ShowOnTop(_wbWindow); };
        _cbLayer.AddChild(window);
        window.Body.AddChild(UiTheme.Text("There can be up to 9 applicants.", 12, UiTheme.TextLo));
        _wrRows = new VBoxContainer();
        _wrRows.AddThemeConstantOverride("separation", 2);
        window.Body.AddChild(_wrRows);
        return window;
    }

    private void AcceptWishRequest(int clanId)
    {
        Net.I.SendWishAccept(clanId);
        if (_cmWindow != null) _cmWindow.Visible = false;
        CloseWishBoard();
    }
}
