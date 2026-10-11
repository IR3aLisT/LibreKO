using System.Collections.Generic;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const int VanguardHidden = 0;
    private const int VanguardInviteFull = 1;
    private const int VanguardListFull = 2;
    private const int VanguardInviteMini = 3;
    private const int VanguardListMini = 4;
    private const int VanguardInviteWidth = 389;
    private const int VanguardInviteMiniWidth = 204;
    private const int VanguardListWidth = 384;
    private const int VanguardListMiniWidth = 171;
    private const int VanguardWindowInset = 20;
    private const int VanguardTopDivisor = 8;
    private const float VanguardBarHeight = 12f;
    private const float VanguardRingInner = 0.85f;
    private const float VanguardRingOuter = 1.05f;
    private const float VanguardRingLift = 0.08f;
    private const float VanguardBlipRadius = 6f;
    private static readonly Color VanguardLineColour = new("#" + Vanguard.ListColour);
    private static readonly Color VanguardAppointColour = new("#" + Vanguard.AppointedColour);
    private static readonly Color VanguardRingColour = new(0.62f, 0.25f, 0.95f, 0.85f);
    private static readonly Color VanguardBarColour = new("c0392b");

    private const string AppointTitle = "[Appoint Vanguard]";
    private const string AppointHeading = "ANNOUNCEMENT";
    private const string AppointBody = "Enemy nation is keeping an eye on you. Your nation want to appoint you as a Vanguard to raise the morale. Once you become a Vanguard, enemy nation will put a reward on your neck. However, if you survive, you will receive a huge reward. Would like to accept the offer?";
    private const string AppointSafety = "(Entering into Safety Zone is prohibited. You will be teleported to center of Ronarkland)";
    private const string AppointMiniLine = "If accept, You will be appointed as a Vanguard";
    private const string AppointMiniSafety = "(If accepted, you cannot enter Safety Zone)";
    private const string AppointHide = "Do not show [Appoint Vanguard]";
    private const string WantedTitle = "Wanted List";
    private const string WantedHeading = "WANTED";
    private const string WantedBody = "Enemy nation's vanguards are rampaging. Kill the vanguards from enemy nation and raise the morale of allies. Authorities will reward the knight who kills the enemy vanguard.";
    private const string WantedSee = "See [Wanted List]";
    private const string WantedHide = "Do not show [Wanted List]";
    private const string WantedToggleTip = "Wanted List UI on/off";
    private const string WantedOpenTip = "Wanted List UI";

    private CanvasLayer _vanguardLayer = null!;
    private HudWindow _vanguardInvite = null!, _vanguardInviteMini = null!, _vanguardList = null!, _vanguardListMini = null!;
    private Label _vanguardCount = null!, _vanguardOthers = null!;
    private ColorRect _vanguardNationBand = null!;
    private StatBar _vanguardBar = null!, _vanguardMiniBar = null!;
    private readonly Label[] _vanguardRows = new Label[Vanguard.ListRows];
    private CheckBox _vanguardInviteHide = null!, _vanguardListHide = null!;
    private Button _wantedToggle = null!, _wantedOpen = null!;
    private bool _vanguardUiOn = true;
    private double _vanguardCountdown;
    private readonly Dictionary<string, MeshInstance3D> _vanguardRings = new(System.StringComparer.OrdinalIgnoreCase);

    private void VanguardInit()
    {
        _vanguardLayer = new CanvasLayer { Layer = 62 };
        AddChild(_vanguardLayer);
        BuildVanguardInvite();
        BuildVanguardInviteMini();
        BuildVanguardList();
        BuildVanguardListMini();
        BuildWantedMiniMapButtons();
        Net.I.VanguardEvent += OnVanguard;
    }

    private void VanguardDispose() => Net.I.VanguardEvent -= OnVanguard;

    private HudWindow VanguardWindow(string id, string title, int width)
    {
        float screenWidth = IsInsideTree() ? GetViewport().GetVisibleRect().Size.X : 0f;
        float screenHeight = IsInsideTree() ? GetViewport().GetVisibleRect().Size.Y : 0f;
        var origin = new Vector2(Mathf.Max(0f, screenWidth - width), screenHeight / VanguardTopDivisor);
        var window = new HudWindow(id, title, origin, width - VanguardWindowInset) { Visible = false };
        window.Closed += () => window.Visible = false;
        _vanguardLayer.AddChild(window);
        return window;
    }

    private static VBoxContainer VanguardBody(HudWindow window)
    {
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 6);
        window.Body.AddChild(body);
        return body;
    }

    private static Label VanguardText(string text, int size, Color colour, bool wrap = false, bool centred = false)
    {
        var label = UiTheme.Text(text, size, colour);
        if (wrap) label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        if (centred) label.HorizontalAlignment = HorizontalAlignment.Center;
        return label;
    }

    private void BuildVanguardInvite()
    {
        _vanguardInvite = VanguardWindow("vanguard_invite", AppointTitle, VanguardInviteWidth);
        var body = VanguardBody(_vanguardInvite);
        _vanguardNationBand = new ColorRect { CustomMinimumSize = new Vector2(0f, 4f) };
        body.AddChild(_vanguardNationBand);
        body.AddChild(VanguardText(AppointHeading, 14, UiTheme.GoldBright, centred: true));
        body.AddChild(VanguardText(AppointBody, 12, UiTheme.TextHi, wrap: true));
        body.AddChild(VanguardText(AppointSafety, 12, UiTheme.TextLo, wrap: true));

        var countRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        countRow.AddChild(VanguardText("You have", 12, UiTheme.TextHi));
        _vanguardCount = VanguardText("10", 12, UiTheme.GoldBright);
        countRow.AddChild(_vanguardCount);
        countRow.AddChild(VanguardText("seconds left to decide", 12, UiTheme.TextHi));
        body.AddChild(countRow);
        _vanguardBar = new StatBar(VanguardBarColour, new Vector2(VanguardInviteWidth - 2 * VanguardWindowInset, VanguardBarHeight));
        body.AddChild(_vanguardBar);
        body.AddChild(VanguardAnswerRow());

        _vanguardInviteHide = UiTheme.FlatCheck(AppointHide, false);
        _vanguardInviteHide.Toggled += on => { if (on) TurnVanguardUiOff(); };
        body.AddChild(_vanguardInviteHide);
    }

    private HBoxContainer VanguardAnswerRow()
    {
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 24);
        var accept = UiTheme.ActionButton("Accept", "");
        accept.Pressed += AcceptVanguard;
        var reject = UiTheme.SmallButton("Reject", "");
        reject.Pressed += HideVanguardInvites;
        row.AddChild(accept);
        row.AddChild(reject);
        return row;
    }

    private void BuildVanguardInviteMini()
    {
        _vanguardInviteMini = VanguardWindow("vanguard_invite_mini", AppointTitle, VanguardInviteMiniWidth);
        var body = VanguardBody(_vanguardInviteMini);
        var detail = UiTheme.SmallButton("Detail", "");
        detail.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        detail.Pressed += () => ShowVanguardMode(VanguardInviteFull);
        body.AddChild(detail);
        body.AddChild(VanguardText(AppointMiniLine, 12, UiTheme.TextHi, wrap: true));
        body.AddChild(VanguardText(AppointMiniSafety, 12, UiTheme.TextLo, wrap: true));
        _vanguardMiniBar = new StatBar(VanguardBarColour, new Vector2(VanguardInviteMiniWidth - 2 * VanguardWindowInset, VanguardBarHeight));
        body.AddChild(_vanguardMiniBar);
        body.AddChild(VanguardAnswerRow());
    }

    private void BuildVanguardList()
    {
        _vanguardList = VanguardWindow("vanguard_list", WantedTitle, VanguardListWidth);
        var body = VanguardBody(_vanguardList);
        body.AddChild(VanguardText(WantedHeading, 14, UiTheme.GoldBright, centred: true));
        body.AddChild(VanguardText(WantedBody, 12, UiTheme.TextHi, wrap: true));
        var rows = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        rows.AddThemeConstantOverride("h_separation", 8);
        for (int i = 0; i < Vanguard.ListRows; i++)
        {
            rows.AddChild(VanguardText(Vanguard.RowLabel(i), 13, UiTheme.TextLo));
            _vanguardRows[i] = VanguardText(Vanguard.EmptyRow, 13, UiTheme.TextHi);
            _vanguardRows[i].CustomMinimumSize = new Vector2(160f, 0f);
            rows.AddChild(_vanguardRows[i]);
        }
        body.AddChild(rows);
        var othersRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        othersRow.AddChild(VanguardText("...", 12, UiTheme.TextLo));
        _vanguardOthers = VanguardText("0", 12, UiTheme.GoldBright);
        othersRow.AddChild(_vanguardOthers);
        othersRow.AddChild(VanguardText("user(s)", 12, UiTheme.TextLo));
        body.AddChild(othersRow);
        _vanguardListHide = UiTheme.FlatCheck(WantedHide, false);
        _vanguardListHide.Toggled += on =>
        {
            if (!on) return;
            TurnVanguardUiOff();
            _vanguardList.Visible = false;
        };
        body.AddChild(_vanguardListHide);
    }

    private void BuildVanguardListMini()
    {
        _vanguardListMini = VanguardWindow("vanguard_list_mini", WantedTitle, VanguardListMiniWidth);
        var body = VanguardBody(_vanguardListMini);
        body.AddChild(VanguardText(WantedSee, 12, UiTheme.TextHi, centred: true));
        var detail = UiTheme.ActionButton("Detail", "");
        detail.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        detail.Pressed += () =>
        {
            _vanguardListMini.Visible = false;
            if (Vanguard.ModeShown(_vanguardUiOn, SelfStallOpen)) ShowWantedList();
        };
        body.AddChild(detail);
    }

    private void BuildWantedMiniMapButtons()
    {
        if (_miniMap == null) return;
        _wantedToggle = UiTheme.IconButton(UiIcons.Get("system/scroll"), WantedToggleTip);
        _wantedToggle.Position = new Vector2(4f, 162f);
        _wantedToggle.Pressed += ToggleVanguardUi;
        _miniMap.AddChild(_wantedToggle);
        _wantedOpen = UiTheme.IconButton(UiIcons.Get("system/search"), WantedOpenTip);
        _wantedOpen.Position = new Vector2(4f, 186f);
        _wantedOpen.Pressed += ShowWantedList;
        _miniMap.AddChild(_wantedOpen);
        RefreshWantedToggle();
    }

    private bool SelfStallOpen => _sellStallShown || _stalls.ContainsKey(_myId);

    private void ToggleVanguardUi()
    {
        _vanguardUiOn = !_vanguardUiOn;
        RefreshWantedToggle();
    }

    private void TurnVanguardUiOff()
    {
        _vanguardUiOn = false;
        HideVanguardInvites();
        RefreshWantedToggle();
    }

    private void RefreshWantedToggle()
    {
        if (_wantedToggle == null || !IsInstanceValid(_wantedToggle)) return;
        _wantedToggle.Modulate = _vanguardUiOn ? Colors.White : UiTheme.TextLo;
    }

    private void HideVanguardInvites()
    {
        _vanguardInvite.Visible = false;
        _vanguardInviteMini.Visible = false;
    }

    private void AcceptVanguard()
    {
        HideVanguardInvites();
        Net.I.SendVanguardAccept();
    }

    private void ShowVanguardMode(int mode)
    {
        if (SelfStallOpen) return;
        if (mode == VanguardInviteMini) _vanguardCountdown = Vanguard.InviteSeconds;
        bool shown = _vanguardUiOn;
        _vanguardInvite.Visible = shown && mode == VanguardInviteFull;
        _vanguardInviteMini.Visible = shown && mode == VanguardInviteMini;
        _vanguardListMini.Visible = shown && mode == VanguardListMini;
        if (shown && mode == VanguardListFull) ShowWantedList();
        else _vanguardList.Visible = false;
        if (_vanguardInvite.Visible)
            _vanguardNationBand.Color = new Color("#" + DeathNotices.NameColour(Net.I.Nation));
        foreach (var window in new[] { _vanguardInvite, _vanguardInviteMini, _vanguardListMini })
            if (window.Visible) ShowOnTop(window);
    }

    private void ShowWantedList()
    {
        var names = Net.I.WantedNames;
        for (int i = 0; i < Vanguard.ListRows; i++)
            _vanguardRows[i].Text = i < names.Count ? names[i] : Vanguard.EmptyRow;
        _vanguardOthers.Text = Vanguard.OthersCount(Net.I.WantedCount).ToString();
        _vanguardInvite.Visible = false;
        _vanguardInviteMini.Visible = false;
        _vanguardListMini.Visible = false;
        ShowOnTop(_vanguardList);
    }

    private void OnVanguard(VanguardReply reply)
    {
        switch (reply.Kind)
        {
            case VanguardReplyKind.Invite:
                ShowVanguardMode(VanguardInviteMini);
                _vanguardCountdown = Vanguard.InviteSeconds;
                break;
            case VanguardReplyKind.Appointed:
                CombatNotice(ItemData.Text(Vanguard.AppointedText, "You have been appointed as a Vanguard. You are now wanted from enemy nation"), VanguardAppointColour);
                break;
            case VanguardReplyKind.AppointFailed:
                CombatNotice(ItemData.Text(Vanguard.FailedText, "Nomination Failed - Vanguard recruitment has been completed"), VanguardAppointColour);
                break;
            case VanguardReplyKind.List:
                if (reply.Names.Count == 0) break;
                ShowVanguardMode(VanguardListMini);
                foreach (var line in Vanguard.ListLines(reply.Names))
                    CombatNotice(line, VanguardLineColour);
                break;
            case VanguardReplyKind.Positions:
                if (Net.I.IsGm)
                    for (int i = 0; i < reply.Marks.Count; i++)
                        CombatNotice(Vanguard.GmLine(i, reply.Marks[i].Name, reply.Marks[i].X, reply.Marks[i].Z), VanguardLineColour);
                break;
            case VanguardReplyKind.Closed:
                CombatNotice(ItemData.Text(Vanguard.ClosedText, "Closed"), VanguardLineColour);
                break;
        }
    }

    private void VanguardTick(double delta)
    {
        if (_vanguardCountdown > 0)
        {
            _vanguardCountdown = Mathf.Max(0, _vanguardCountdown - delta);
            float left = (float)(_vanguardCountdown / Vanguard.InviteSeconds);
            _vanguardBar.SetFraction(left, "");
            _vanguardMiniBar.SetFraction(left, "");
            _vanguardCount.Text = ((int)_vanguardCountdown).ToString();
            if (_vanguardCountdown <= 0) HideVanguardInvites();
        }
        SyncVanguardRings();
    }

    private void SyncVanguardRings()
    {
        var wanted = Net.I.WantedNames;
        if (wanted.Count == 0 && _vanguardRings.Count == 0) return;
        var keep = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var e in _ents.Values)
        {
            if (e.IsNpc || e.Dead || !wanted.Contains(e.Name)) continue;
            keep.Add(e.Name);
            if (_vanguardRings.TryGetValue(e.Name, out var ring) && IsInstanceValid(ring) && ring.GetParent() == e.Body) continue;
            if (ring != null && IsInstanceValid(ring)) ring.QueueFree();
            ring = VanguardRing();
            e.Body.AddChild(ring);
            _vanguardRings[e.Name] = ring;
        }
        foreach (var name in new List<string>(_vanguardRings.Keys))
        {
            if (keep.Contains(name)) continue;
            if (IsInstanceValid(_vanguardRings[name])) _vanguardRings[name].QueueFree();
            _vanguardRings.Remove(name);
        }
    }

    private static MeshInstance3D VanguardRing() => new()
    {
        Mesh = new TorusMesh { InnerRadius = VanguardRingInner, OuterRadius = VanguardRingOuter },
        Position = new Vector3(0f, VanguardRingLift, 0f),
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = VanguardRingColour,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        },
    };

    private void AddVanguardBlips()
    {
        foreach (var mark in Net.I.VanguardMarks)
            _blipScratch.Add(new MiniMap.Blip(mark.X, mark.Z, NamePlate.Enemy, VanguardBlipRadius, true));
    }
}
