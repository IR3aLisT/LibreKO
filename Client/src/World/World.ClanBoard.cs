using System.Collections.Generic;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const int ClanBoardWidth = 560;
    private const int ClanBoardRowHeight = 38;
    private const float ClanBoardMarkSize = 20f;
    private const float ClanBoardNameWidth = 150f;
    private const int ClanMessageWidth = 340;
    private const int ClanMessageEditHeight = 200;
    private const int ClanJoinListWidth = 420;

    private enum ClanMessageMode { WriteAdvert, ViewAdvert, ComposeApply, ViewApplicant, ViewWishRequest }

    private sealed class ClanBoardRow
    {
        public PanelContainer Panel = null!;
        public TextureRect Mark = null!;
        public Label Name = null!;
        public Label Text = null!;
        public Button Details = null!;
    }

    private CanvasLayer _cbLayer = null!;
    private HudWindow? _cbWindow;
    private readonly ClanBoardRow[] _cbRows = new ClanBoardRow[ClanBoards.RowsPerPage];
    private List<ClanAdvert> _cbAdverts = new();
    private ClanBoardStatus _cbStatus = new(ClanBoards.StateClanless, new List<ClanApplicant>(), 0, 0, "");
    private int _cbPage = 1;
    private Label _cbPageLabel = null!;
    private Button _cbPrev = null!, _cbNext = null!;
    private HBoxContainer _cbStaffRow = null!;
    private HBoxContainer _cbStatusRow = null!;

    private HudWindow? _cmWindow;
    private ClanMessageMode _cmMode;
    private int _cmClanId;
    private string _cmName = "";
    private TextureRect _cmMark = null!;
    private Label _cmClanName = null!, _cmClassLine = null!, _cmLeader = null!, _cmMessage = null!, _cmCounter = null!, _cmWarning = null!;
    private TextEdit _cmEdit = null!;
    private HBoxContainer _cmButtons = null!;

    private HudWindow? _cjWindow;
    private VBoxContainer _cjRows = null!;
    private int _cjPage;
    private Label _cjPageLabel = null!;

    private void ClanBoardInit()
    {
        _cbLayer = new CanvasLayer { Layer = 73 };
        AddChild(_cbLayer);
        Net.I.ClanBoardStatusEvent += OnClanBoardStatus;
        Net.I.ClanAdvertsEvent += OnClanAdverts;
        Net.I.ClanApplyResultEvent += OnClanApplyResult;
        Net.I.ClanAdvertResultEvent += OnClanAdvertResult;
        Net.I.ClanPromoteEvent += OnClanPromote;
        Net.I.ClanConsultationEvent += OnClanConsultation;
        WishBoardInit();
    }

    private void ClanBoardDispose()
    {
        Net.I.ClanBoardStatusEvent -= OnClanBoardStatus;
        Net.I.ClanAdvertsEvent -= OnClanAdverts;
        Net.I.ClanApplyResultEvent -= OnClanApplyResult;
        Net.I.ClanAdvertResultEvent -= OnClanAdvertResult;
        Net.I.ClanPromoteEvent -= OnClanPromote;
        Net.I.ClanConsultationEvent -= OnClanConsultation;
        WishBoardDispose();
    }

    private void OpenClanBoard()
    {
        if (_cbWindow is { Visible: true })
        {
            CloseClanBoard();
            return;
        }
        _cbPage = 1;
        Net.I.SendClanBoardOpen(_cbPage);
    }

    private void CloseClanBoard()
    {
        if (_cbWindow != null) _cbWindow.Visible = false;
        if (_cmWindow != null) _cmWindow.Visible = false;
        if (_cjWindow != null) _cjWindow.Visible = false;
    }

    private void OnClanBoardStatus(ClanBoardStatus status) => _cbStatus = status;

    private void OnClanAdverts(List<ClanAdvert> adverts)
    {
        _cbAdverts = adverts;
        _cbWindow ??= BuildClanBoard();
        _cbStatus = _cbStatus with { State = MyClan.InClan ? ((MyClan.IsChief || MyClan.IsViceChief) ? ClanBoards.StateStaff : ClanBoards.StateMember) : ClanBoards.StateClanless };
        RefreshClanBoard();
        if (_wbWindow != null) _wbWindow.Visible = false;
        ShowOnTop(_cbWindow);
    }

    private static void ShowOnTop(Control window)
    {
        window.Visible = true;
        window.GetParent()?.MoveChild(window, window.GetParent().GetChildCount() - 1);
    }

    private HudWindow BuildClanBoard()
    {
        var window = new HudWindow("clan_board", "Clan Recruitment Announcement", new Vector2(240, 120), bodyMinWidth: ClanBoardWidth) { Visible = false };
        window.Closed += CloseClanBoard;
        _cbLayer.AddChild(window);
        var body = window.Body;

        var head = new HBoxContainer();
        body.AddChild(head);
        var intro = UiTheme.Text("You can recruit clan members as well as request to join a clan through this Bulletin Board.", 12, UiTheme.TextLo);
        intro.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        intro.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        head.AddChild(intro);
        head.AddChild(ClanBoardButton("Bulletin Board for Join Request", OpenWishBoard));

        var captions = new HBoxContainer();
        body.AddChild(captions);
        captions.AddChild(new Control { CustomMinimumSize = new Vector2(ClanBoardMarkSize + 8, 0) });
        var clanCaption = UiTheme.Text("Clan", 12, UiTheme.Gold);
        clanCaption.CustomMinimumSize = new Vector2(ClanBoardNameWidth, 0);
        captions.AddChild(clanCaption);
        var textCaption = UiTheme.Text("Clan Introduction", 12, UiTheme.Gold);
        textCaption.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        captions.AddChild(textCaption);
        captions.AddChild(ClanBoardButton("Refresh", () => { _cbPage = 1; Net.I.SendClanBoardOpen(_cbPage); }));

        var rows = new VBoxContainer();
        rows.AddThemeConstantOverride("separation", 2);
        body.AddChild(rows);
        for (int i = 0; i < ClanBoards.RowsPerPage; i++)
        {
            _cbRows[i] = BuildClanBoardRow(i);
            rows.AddChild(_cbRows[i].Panel);
        }

        var footer = new HBoxContainer();
        body.AddChild(footer);
        _cbStaffRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _cbStaffRow.AddThemeConstantOverride("separation", 6);
        footer.AddChild(_cbStaffRow);
        _cbStaffRow.AddChild(ClanBoardButton("Promote Clan", Net.I.SendClanPromote));
        _cbStaffRow.AddChild(ClanBoardButton("Write Promotional Notes", () => OpenClanMessage(ClanMessageMode.WriteAdvert, null)));
        _cbStaffRow.AddChild(ClanBoardButton("Delete Notes", Net.I.SendClanAdvertDelete));
        footer.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        _cbPrev = ClanBoardButton("<", () => { if (_cbPage > 1) Net.I.SendClanBoardPage(--_cbPage); });
        footer.AddChild(_cbPrev);
        _cbPageLabel = UiTheme.Text("1", 12, UiTheme.TextHi, HorizontalAlignment.Center);
        _cbPageLabel.CustomMinimumSize = new Vector2(32, 0);
        footer.AddChild(_cbPageLabel);
        _cbNext = ClanBoardButton(">", () => Net.I.SendClanBoardPage(++_cbPage));
        footer.AddChild(_cbNext);

        body.AddChild(new HSeparator());
        _cbStatusRow = new HBoxContainer();
        _cbStatusRow.AddThemeConstantOverride("separation", 8);
        body.AddChild(_cbStatusRow);
        return window;
    }

    private static Button ClanBoardButton(string text, System.Action pressed)
    {
        var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.None };
        button.AddThemeFontSizeOverride("font_size", 12);
        button.Pressed += pressed;
        return button;
    }

    private ClanBoardRow BuildClanBoardRow(int index)
    {
        var row = new ClanBoardRow
        {
            Panel = new PanelContainer { CustomMinimumSize = new Vector2(0, ClanBoardRowHeight) },
            Mark = new TextureRect
            {
                CustomMinimumSize = new Vector2(ClanBoardMarkSize, ClanBoardMarkSize),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            },
            Name = UiTheme.Text("", 12, UiTheme.TextHi),
            Text = UiTheme.Text("", 12, UiTheme.TextLo),
        };
        row.Panel.AddThemeStyleboxOverride("panel", UiTheme.Row());
        var line = new HBoxContainer();
        line.AddThemeConstantOverride("separation", 8);
        row.Panel.AddChild(line);
        line.AddChild(row.Mark);
        row.Name.CustomMinimumSize = new Vector2(ClanBoardNameWidth, 0);
        row.Name.ClipText = true;
        line.AddChild(row.Name);
        row.Text.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.Text.ClipText = true;
        row.Text.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        line.AddChild(row.Text);
        row.Details = ClanBoardButton("Details", () =>
        {
            if (index < _cbAdverts.Count) OpenClanMessage(ClanMessageMode.ViewAdvert, _cbAdverts[index]);
        });
        line.AddChild(row.Details);
        return row;
    }

    private void RefreshClanBoard()
    {
        for (int i = 0; i < ClanBoards.RowsPerPage; i++)
        {
            var row = _cbRows[i];
            bool used = i < _cbAdverts.Count;
            row.Mark.Visible = row.Details.Visible = used;
            row.Name.Text = used ? _cbAdverts[i].ClanName : "";
            row.Text.Text = used ? _cbAdverts[i].Text : "";
            row.Mark.Texture = used ? ClanMark(_cbAdverts[i].ClanId, _cbAdverts[i].MarkVersion) : null;
        }
        _cbPageLabel.Text = _cbPage.ToString();
        _cbPrev.Disabled = _cbPage <= 1;
        _cbNext.Disabled = !ClanBoards.CanPageForward(_cbAdverts.Count, ClanBoards.RowsPerPage);
        _cbStaffRow.Visible = _cbStatus.Staff;

        foreach (var child in _cbStatusRow.GetChildren()) { _cbStatusRow.RemoveChild(child); child.QueueFree(); }
        if (_cbStatus.Staff)
        {
            int count = _cbStatus.Applicants.Count;
            _cbStatusRow.AddChild(UiTheme.Text(count > 0 ? $"Clan applicants: {count}" : "No one has applied to your clan yet.", 12, UiTheme.TextLo));
            if (count > 0) _cbStatusRow.AddChild(ClanBoardButton("List of Applicants", OpenClanJoinList));
        }
        else if (_cbStatus.Applied)
        {
            _cbStatusRow.AddChild(new TextureRect
            {
                Texture = ClanMark(_cbStatus.AppliedClanId, _cbStatus.AppliedMarkVersion),
                CustomMinimumSize = new Vector2(ClanBoardMarkSize, ClanBoardMarkSize),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            });
            var applied = UiTheme.Text($"You applied to {_cbStatus.AppliedClanName}.", 12, UiTheme.TextHi);
            applied.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _cbStatusRow.AddChild(applied);
            _cbStatusRow.AddChild(ClanBoardButton("Cancel Request", () => Net.I.SendClanApplyCancel(Net.I.LastEnter.Name)));
        }
        else
        {
            _cbStatusRow.AddChild(UiTheme.Text(MyClan.InClan ? "You are in a clan." : "You have not applied to a clan.", 12, UiTheme.TextLo));
        }
    }

    private void OnClanApplyResult(int kind, int code) => ShowClanBoardResult(ClanBoards.ApplyResultText(kind, code));

    private void OnClanAdvertResult(int kind, int code) => ShowClanBoardResult(ClanBoards.AdvertResultText(kind, code));

    private void ShowClanBoardResult(int textId)
    {
        if (_cmWindow != null) _cmWindow.Visible = false;
        if (textId != 0) Notice.Show(this, ItemData.Text(textId, ""));
        Net.I.SendClanBoardOpen(_cbPage);
    }

    private void OnClanPromote(int code, string chief, string clan)
    {
        if (chief.Length > 0)
            CombatNotice(TextTemplate.Fill(ItemData.Text(ClanBoards.TextAdvertPromoted,
                "%s of %s clan is currently taking applications for new clan members. Check the clan forum for details."),
                chief, clan));
        else if (ClanBoards.PromoteText(code) is var text and > 0)
            Notice.Show(this, ItemData.Text(text, ""));
    }

    private void OnClanConsultation(bool connected, string name, bool received)
    {
        if (!connected || name.Length == 0)
        {
            CombatNotice(ItemData.Text(ChatSystem.TextWhisperFailed, "Failed to connect for a private chat"));
            return;
        }
        OpenWhisperWith(name);
        OnWhisperNotice(name, received
            ? $"{name} {ItemData.Text(ClanConsultReceivedText, "is requesting clan admission consultation")}"
            : $"{ItemData.Text(ClanConsultSentText, "Requested clan admission consultation to")} {name}");
    }

    private const int ClanConsultReceivedText = 16512;
    private const int ClanConsultSentText = 16513;

    private HudWindow BuildClanMessage()
    {
        var window = new HudWindow("clan_message", "Clan", new Vector2(820, 130), bodyMinWidth: ClanMessageWidth) { Visible = false };
        window.Closed += () => { window.Visible = false; if (_cbWindow is { Visible: true }) ShowOnTop(_cbWindow); };
        _cbLayer.AddChild(window);
        var body = window.Body;

        var card = new HBoxContainer();
        card.AddThemeConstantOverride("separation", 8);
        body.AddChild(card);
        _cmMark = new TextureRect
        {
            CustomMinimumSize = new Vector2(32, 32),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        card.AddChild(_cmMark);
        var lines = new VBoxContainer();
        card.AddChild(lines);
        _cmClanName = UiTheme.Text("", 15, UiTheme.GoldBright);
        lines.AddChild(_cmClanName);
        _cmClassLine = UiTheme.Text("", 12, UiTheme.TextLo);
        lines.AddChild(_cmClassLine);
        _cmLeader = UiTheme.Text("", 12, UiTheme.TextLo);
        lines.AddChild(_cmLeader);

        _cmMessage = UiTheme.Text("", 13, UiTheme.TextHi);
        _cmMessage.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _cmMessage.CustomMinimumSize = new Vector2(ClanMessageWidth, 0);
        body.AddChild(_cmMessage);
        _cmEdit = new TextEdit
        {
            CustomMinimumSize = new Vector2(ClanMessageWidth, ClanMessageEditHeight),
            WrapMode = TextEdit.LineWrappingMode.Boundary,
        };
        _cmEdit.TextChanged += () => _cmCounter.Text = $"{_cmEdit.Text.Length}/{ClanBoards.MaxAdvertBytes}";
        body.AddChild(_cmEdit);
        _cmCounter = UiTheme.Text("", 11, UiTheme.TextDim, HorizontalAlignment.Right);
        body.AddChild(_cmCounter);
        _cmWarning = UiTheme.Text("Enter the message you want to leave for the clan representative.", 11, UiTheme.TextDim);
        _cmWarning.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _cmWarning.CustomMinimumSize = new Vector2(ClanMessageWidth, 0);
        body.AddChild(_cmWarning);
        _cmButtons = new HBoxContainer();
        _cmButtons.AddThemeConstantOverride("separation", 6);
        body.AddChild(_cmButtons);
        return window;
    }

    private void OpenClanMessage(ClanMessageMode mode, ClanAdvert? advert, string name = "", string message = "", WishRequest? request = null)
    {
        _cmWindow ??= BuildClanMessage();
        _cmMode = mode;
        _cmName = name;
        bool own = mode is ClanMessageMode.WriteAdvert or ClanMessageMode.ViewApplicant;
        _cmClanId = own ? MyClan.ClanId : advert?.ClanId ?? request?.ClanId ?? 0;
        int markVersion = own ? MyClan.MarkVersion : advert?.MarkVersion ?? request?.MarkVersion ?? 0;
        int grade = own ? MyClan.Grade : advert?.Grade ?? request?.Grade ?? 0;
        int type = own ? MyClan.Flag : advert?.Type ?? request?.Type ?? 0;
        _cmMark.Texture = ClanMark(_cmClanId, markVersion);
        _cmClanName.Text = own ? MyClan.Name : advert?.ClanName ?? request?.ClanName ?? "";
        _cmClassLine.Text = ItemData.Text(ClanBoards.ClassLineText(type), "Clan %d grade.").Replace("%d", grade.ToString());
        _cmLeader.Text = "Leader: " + (mode == ClanMessageMode.WriteAdvert ? Net.I.LastEnter.Name
            : advert?.Chief ?? request?.Leader ?? (mode == ClanMessageMode.ViewApplicant ? Net.I.LastEnter.Name : ""));

        bool editing = mode is ClanMessageMode.WriteAdvert or ClanMessageMode.ComposeApply;
        _cmEdit.Visible = _cmCounter.Visible = editing;
        _cmWarning.Visible = mode == ClanMessageMode.ComposeApply;
        _cmMessage.Visible = !editing;
        _cmEdit.Text = "";
        _cmCounter.Text = $"0/{ClanBoards.MaxAdvertBytes}";
        _cmMessage.Text = mode switch
        {
            ClanMessageMode.ViewAdvert => advert?.Text ?? "",
            ClanMessageMode.ViewApplicant => $"{name}{ItemData.Text(ClanBoards.TextApplicantMessage, "'s Message")}\n{message}",
            ClanMessageMode.ViewWishRequest => $"{request?.Leader}{ItemData.Text(ClanBoards.TextApplicantMessage, "'s Message")}\n{request?.Message}",
            _ => "",
        };

        foreach (var child in _cmButtons.GetChildren()) { _cmButtons.RemoveChild(child); child.QueueFree(); }
        switch (mode)
        {
            case ClanMessageMode.WriteAdvert:
                _cmButtons.AddChild(ClanBoardButton("Confirm", () => SendClanMessage(Net.I.SendClanAdvertRegister)));
                break;
            case ClanMessageMode.ViewAdvert when _cbStatus.State == ClanBoards.StateClanless:
                _cmButtons.AddChild(ClanBoardButton("Request to Join", () => OpenClanMessage(ClanMessageMode.ComposeApply, advert)));
                _cmButtons.AddChild(ClanBoardButton("Request for Consultation", () => Net.I.SendClanConsultation(_cmClanId)));
                break;
            case ClanMessageMode.ViewAdvert:
                _cmButtons.AddChild(ClanBoardButton("Confirm", () => _cmWindow!.Visible = false));
                break;
            case ClanMessageMode.ComposeApply:
                _cmButtons.AddChild(ClanBoardButton("Confirm", () => SendClanMessage(text => Net.I.SendClanApply(_cmClanId, text))));
                _cmButtons.AddChild(ClanBoardButton("Request for Consultation", () => Net.I.SendClanConsultation(_cmClanId)));
                break;
            case ClanMessageMode.ViewApplicant:
                _cmButtons.AddChild(ClanBoardButton("Whisper", () => Net.I.SendPlayerConsultation(_cmName)));
                _cmButtons.AddChild(ClanBoardButton("Close", () => _cmWindow!.Visible = false));
                break;
            case ClanMessageMode.ViewWishRequest:
                _cmButtons.AddChild(ClanBoardButton("Accept", () => AcceptWishRequest(_cmClanId)));
                _cmButtons.AddChild(ClanBoardButton("Reject", () => { Net.I.SendWishRequestCancel(_cmClanId); _cmWindow!.Visible = false; }));
                break;
        }
        ShowOnTop(_cmWindow);
        if (editing) _cmEdit.CallDeferred(Control.MethodName.GrabFocus);
    }

    private void SendClanMessage(System.Action<string> send)
    {
        string text = _cmEdit.Text.Trim();
        if (text.Length > ClanBoards.MaxAdvertBytes)
        {
            Notice.Show(this, ItemData.Text(ClanBoards.TextOverLimit, "Length of message is over limit"));
            return;
        }
        send(text);
    }

    private void OpenClanJoinList()
    {
        _cjWindow ??= BuildClanJoinList();
        _cjPage = 0;
        if (_cbWindow != null) _cbWindow.Visible = false;
        RefreshClanJoinList();
        ShowOnTop(_cjWindow);
    }

    private HudWindow BuildClanJoinList()
    {
        var window = new HudWindow("clan_join_list", "List of Applicants", new Vector2(260, 140), bodyMinWidth: ClanJoinListWidth) { Visible = false };
        window.Closed += () => { window.Visible = false; if (_cbWindow != null) ShowOnTop(_cbWindow); };
        _cbLayer.AddChild(window);
        _cjRows = new VBoxContainer();
        _cjRows.AddThemeConstantOverride("separation", 2);
        window.Body.AddChild(_cjRows);
        var footer = new HBoxContainer();
        window.Body.AddChild(footer);
        footer.AddChild(ClanBoardButton("Back", () => { window.Visible = false; if (_cbWindow != null) ShowOnTop(_cbWindow); }));
        footer.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        footer.AddChild(ClanBoardButton("<", () => { if (_cjPage > 0) { _cjPage--; RefreshClanJoinList(); } }));
        _cjPageLabel = UiTheme.Text("1", 12, UiTheme.TextHi, HorizontalAlignment.Center);
        _cjPageLabel.CustomMinimumSize = new Vector2(32, 0);
        footer.AddChild(_cjPageLabel);
        footer.AddChild(ClanBoardButton(">", () =>
        {
            if ((_cjPage + 1) * ClanBoards.ApplicantsPerPage < _cbStatus.Applicants.Count) { _cjPage++; RefreshClanJoinList(); }
        }));
        return window;
    }

    private void RefreshClanJoinList()
    {
        foreach (var child in _cjRows.GetChildren()) { _cjRows.RemoveChild(child); child.QueueFree(); }
        var applicants = _cbStatus.Applicants;
        for (int i = _cjPage * ClanBoards.ApplicantsPerPage; i < applicants.Count && i < (_cjPage + 1) * ClanBoards.ApplicantsPerPage; i++)
        {
            var applicant = applicants[i];
            var panel = new PanelContainer();
            panel.AddThemeStyleboxOverride("panel", UiTheme.Row());
            var line = new HBoxContainer();
            line.AddThemeConstantOverride("separation", 8);
            panel.AddChild(line);
            line.AddChild(UiTheme.Text($"{i + 1}", 12, UiTheme.TextDim));
            line.AddChild(UiTheme.Text($"Lv {applicant.Level}", 12, UiTheme.TextLo));
            var name = UiTheme.Text(applicant.Name, 12, UiTheme.TextHi);
            name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            line.AddChild(name);
            line.AddChild(UiTheme.Text(ClassName(applicant.Class), 12, UiTheme.TextLo));
            line.AddChild(ClanBoardButton("Message", () => OpenClanMessage(ClanMessageMode.ViewApplicant, null, applicant.Name, applicant.Message)));
            line.AddChild(ClanBoardButton("Delete", () => Net.I.SendClanApplyCancel(applicant.Name)));
            _cjRows.AddChild(panel);
        }
        if (applicants.Count == 0) _cjRows.AddChild(UiTheme.Text("No one has applied to your clan yet.", 12, UiTheme.TextLo));
        _cjPageLabel.Text = (_cjPage + 1).ToString();
    }
}
