using System.Collections.Generic;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const string PartyHudLayoutId = "hud_party";
    private const float PartyHudWidth = 152f;
    private const float PartyHudTop = 176f;
    private const float PartyHpBarHeight = 7f;
    private const float PartyMpBarHeight = 5f;
    private const float PartyMeterBarHeight = 4f;
    private const float PartyClassIconSize = 14f;
    private const int PartyHudTitleFont = 12;
    private const int PartyHudNameFont = 11;
    private const int TextPartyMakeLeader = 3431;
    private const int TextPartyKick = 3402;
    private const int TextPartyDisband = 3401;
    private const int TextPartyQuit = 3403;
    private const int PartyMenuMakeLeader = 1;
    private const int PartyMenuKick = 2;
    private const int PartyMenuCommander = 3;

    private static readonly Color PartyRowBg = new(0.06f, 0.065f, 0.055f, 0.78f);
    private static readonly Color PartyRowEdge = new(0.40f, 0.42f, 0.29f, 0.9f);
    private static readonly Color PartyRowPicked = new(0.90f, 0.86f, 0.55f, 1f);
    private static readonly Color PartyBarTrack = new(0.02f, 0.02f, 0.02f, 0.9f);
    private static readonly Color PartyLeaderNameColour = new(1f, 1f, 0f);
    private static readonly Color PartyNameColour = new(1f, 1f, 1f);
    private static readonly Color PartyMpFill = Color.Color8(31, 92, 184);
    private static readonly Dictionary<PartyHpLook, Color> PartyHpFills = new()
    {
        [PartyHpLook.Normal] = Color.Color8(168, 22, 22),
        [PartyHpLook.Lasting] = Color.Color8(184, 152, 18),
        [PartyHpLook.Drop] = Color.Color8(161, 81, 107),
        [PartyHpLook.Slow] = Color.Color8(98, 136, 27),
        [PartyHpLook.Undead] = Color.Color8(105, 105, 105),
    };

    private sealed class PartyHudRow
    {
        public PanelContainer Frame = null!;
        public StyleBoxFlat Style = null!;
        public PartyMeter Hp = null!;
        public PartyMeter Mp = null!;
        public PartyMeter Meter = null!;
        public TextureRect Icon = null!;
        public Label Name = null!;
        public int MemberId = -1;
    }

    private VBoxContainer? _partyHud;
    private readonly PartyHudRow[] _partyHudRows = new PartyHudRow[PartyHud.MaxRows];
    private PopupMenu _partyHudMenu = null!;
    private int _partyHudMenuMember = -1;
    private long _partyHudSecond = -1;
    private bool _partyHudCycles;

    private void BuildPartyHud()
    {
        var layer = new CanvasLayer { Layer = 64 };
        AddChild(layer);
        PluginHudSeam(layer, LibreKO.Plugins.HudPart.Party);
        var hud = new VBoxContainer
        {
            Visible = false,
            CustomMinimumSize = new Vector2(PartyHudWidth, 0),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        hud.AddThemeConstantOverride("separation", 3);
        layer.AddChild(hud);

        var header = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        header.AddThemeStyleboxOverride("panel", UiTheme.Chip());
        var headRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        header.AddChild(headRow);
        var title = UiTheme.Text("Party", PartyHudTitleFont, UiTheme.Gold);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        title.MouseFilter = Control.MouseFilterEnum.Ignore;
        headRow.AddChild(title);
        AddMeterButtons(headRow);
        var leave = UiTheme.IconButton(UiIcons.Get("system/close"), "Leave the party");
        leave.Pressed += AskLeaveParty;
        headRow.AddChild(leave);
        hud.AddChild(header);

        for (int i = 0; i < PartyHud.MaxRows; i++)
        {
            _partyHudRows[i] = BuildPartyHudRow(i);
            hud.AddChild(_partyHudRows[i].Frame);
        }

        _partyHudMenu = new PopupMenu();
        _partyHudMenu.IdPressed += OnPartyHudMenu;
        layer.AddChild(_partyHudMenu);

        _partyHud = hud;
        HudLayout.Attach(hud, PartyHudLayoutId, header, () => new Vector2(HudAnchor.Edge, PartyHudTop));
        RefreshPartyHud();
    }

    private PartyHudRow BuildPartyHudRow(int slot)
    {
        var row = new PartyHudRow
        {
            Style = new StyleBoxFlat { BgColor = PartyRowBg, BorderColor = PartyRowEdge },
            Frame = new PanelContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Stop },
            Hp = new PartyMeter(PartyBarTrack) { CustomMinimumSize = new Vector2(0, PartyHpBarHeight) },
            Mp = new PartyMeter(PartyBarTrack) { CustomMinimumSize = new Vector2(0, PartyMpBarHeight), Fill = PartyMpFill },
            Meter = new PartyMeter(PartyBarTrack) { CustomMinimumSize = new Vector2(0, PartyMeterBarHeight) },
            Icon = new TextureRect
            {
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = new Vector2(PartyClassIconSize, PartyClassIconSize),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            },
            Name = UiTheme.Text("", PartyHudNameFont, PartyNameColour),
        };
        row.Style.SetBorderWidthAll(1);
        row.Style.SetCornerRadiusAll(3);
        row.Style.ContentMarginLeft = row.Style.ContentMarginRight = 4;
        row.Style.ContentMarginTop = row.Style.ContentMarginBottom = 3;
        row.Frame.AddThemeStyleboxOverride("panel", row.Style);

        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 2);
        row.Frame.AddChild(box);
        box.AddChild(row.Hp);
        box.AddChild(row.Mp);
        box.AddChild(row.Meter);

        var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        line.AddThemeConstantOverride("separation", 4);
        line.AddChild(row.Icon);
        row.Name.ClipText = true;
        row.Name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        row.Name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.Name.MouseFilter = Control.MouseFilterEnum.Ignore;
        line.AddChild(row.Name);
        box.AddChild(line);

        row.Frame.GuiInput += ev => OnPartyHudRowInput(slot, ev);
        return row;
    }

    private void RefreshPartyHud()
    {
        if (_partyHud == null) return;
        PluginNotifyParty();
        bool shown = InParty;
        if (_partyHud.Visible != shown) _partyHud.Visible = shown;
        long second = (long)Now();
        _partyHudSecond = second;
        _partyHudCycles = false;
        var members = PartyMembers;
        int leader = members.Count > 0 ? members[0].CharId : -1;
        for (int i = 0; i < PartyHud.MaxRows; i++)
        {
            var row = _partyHudRows[i];
            bool used = shown && i < members.Count;
            if (row.Frame.Visible != used) row.Frame.Visible = used;
            if (!used)
            {
                row.MemberId = -1;
                continue;
            }
            FillPartyHudRow(row, members[i], members[i].CharId == leader, second);
        }
    }

    private readonly record struct PartyRowView(int Level, int Class, int Hp, int MaxHp, int Mp, int MaxMp,
        Color NameColour, Color HpColour, string Tooltip, bool Cycles);

    private PartyRowView PartyRowOf(PartyMember m, bool leader, long second)
    {
        bool self = m.CharId == _myId;
        int hp = self ? Vitals.Hp : m.Hp, maxHp = self ? Vitals.MaxHp : m.MaxHp;
        int mp = self ? Vitals.Mp : m.Mp, maxMp = self ? Vitals.MaxMp : m.MaxMp;
        int level = self ? Sheet.Level : m.Level, cls = self ? _selfClass : m.Class;
        var statuses = Net.I.PartyStatusOf(m.CharId);

        string status = "";
        if (statuses is { Count: > 0 })
        {
            var names = new List<string>();
            foreach (byte s in statuses)
                if (StatusName(s) is { Length: > 0 } name) names.Add(name);
            if (names.Count > 0) status = "\n" + string.Join(", ", names);
        }
        return new PartyRowView(level, cls, hp, maxHp, mp, maxMp, PartyNameColourOf(m.CharId, leader),
            PartyHpFills[PartyHud.HpLook(statuses, second)],
            $"{m.Name}  Lv {level} {ClassName(cls)}\nHP {hp} / {maxHp}   MP {mp} / {maxMp}{status}",
            statuses is { Count: > 1 });
    }

    private void FillPartyHudRow(PartyHudRow row, PartyMember m, bool leader, long second)
    {
        var view = PartyRowOf(m, leader, second);
        if (view.Cycles) _partyHudCycles = true;

        row.MemberId = m.CharId;
        row.Hp.Fill = view.HpColour;
        row.Hp.Target = PartyHud.Fill(view.Hp, view.MaxHp);
        row.Mp.Target = PartyHud.Fill(view.Mp, view.MaxMp);
        row.Hp.Visible = view.MaxHp > 0;
        row.Mp.Visible = view.MaxMp > 0;
        FillPartyMeter(row.Meter, m.CharId);
        if (row.Name.Text != m.Name) row.Name.Text = m.Name;
        row.Name.AddThemeColorOverride("font_color", view.NameColour);
        row.Icon.Texture = UiIcons.Get(PartyHud.ClassIcon(view.Class));
        row.Style.BorderColor = m.CharId == _partyPick ? PartyRowPicked : PartyRowEdge;
        row.Frame.TooltipText = view.Tooltip;
    }

    private void PartyHudTick(double now)
    {
        WarBoardTick(now);
        if (_partyHudCycles && (long)now != _partyHudSecond) RefreshPartyHud();
    }

    private void OnPartyHudRowInput(int slot, InputEvent ev)
    {
        if (ev is not InputEventMouseButton { Pressed: true } click) return;
        int memberId = _partyHudRows[slot].MemberId;
        if (memberId < 0) return;
        if (click.ButtonIndex == MouseButton.Left)
        {
            PickPartyMember(memberId);
            GetViewport().SetInputAsHandled();
        }
        else if (click.ButtonIndex == MouseButton.Right && ShowPartyMemberMenu(memberId))
            GetViewport().SetInputAsHandled();
    }

    private void PickPartyMember(int memberId)
    {
        _partyPick = memberId;
        if (memberId != _myId && _ents.TryGetValue(memberId, out var member) && Selectable(member))
            Select(memberId, member);
        RefreshPartyHud();
    }

    private bool ShowPartyMemberMenu(int memberId)
    {
        if (!AmLeader || memberId == _myId) return false;
        _partyHudMenuMember = memberId;
        _partyHudMenu.Clear();
        _partyHudMenu.AddItem("Make leader", PartyMenuMakeLeader);
        _partyHudMenu.AddItem("Kick out", PartyMenuKick);
        _partyHudMenu.AddItem("Make commander", PartyMenuCommander);
        PopupAtCursor(_partyHudMenu, GetViewport().GetMousePosition());
        return true;
    }

    private void RunPartyMemberMenu(int memberId, int action)
    {
        _partyHudMenuMember = memberId;
        OnPartyHudMenu(action);
    }

    private void OnPartyHudMenu(long id)
    {
        int member = _partyHudMenuMember;
        _partyHudMenuMember = -1;
        if (member < 0 || !AmLeader) return;
        string name = PartyMemberName(member);
        if (id == PartyMenuMakeLeader)
            Notice.Confirm(this, SystemText(TextPartyMakeLeader, "Do you authorize %s as a Party Leader?", name),
                "Yes", "No", () => Net.I.SendPartyPromote(member), title: "Party");
        else if (id == PartyMenuKick)
            Notice.Confirm(this, SystemText(TextPartyKick, "Would you like to ban %s from the party?", name),
                "Yes", "No", () => Net.I.SendPartyLeave(member), title: "Party");
        else if (id == PartyMenuCommander)
            Notice.Confirm(this, SystemText(CommanderOrders.AppointText, "Do you authorize %s as a Commander?", name),
                "Yes", "No", () => Net.I.SendPartyCommander(member), title: "Party");
    }

    private void AskLeaveParty()
    {
        if (!InParty) return;
        if (AmLeader)
            Notice.Confirm(this, SystemText(TextPartyDisband, "Are you sure you want to disband the party?"),
                "Yes", "No", () => Net.I.SendPartyDisband(), title: "Party");
        else
            Notice.Confirm(this, SystemText(TextPartyQuit, "Would you like to quit the party?"),
                "Yes", "No", () => Net.I.SendPartyLeave(_myId), title: "Party");
    }

    private static string PartyMemberName(int charId)
    {
        foreach (var m in PartyMembers)
            if (m.CharId == charId) return m.Name;
        return "";
    }

    private sealed partial class PartyMeter : Control
    {
        private const float GlideRate = 8f;
        private const float SettleEpsilon = 0.002f;

        private readonly Color _track;
        private Color _fill = Colors.White;
        private float _shown;
        private float _target;

        public PartyMeter(Color track)
        {
            _track = track;
            MouseFilter = MouseFilterEnum.Ignore;
            SetProcess(false);
        }

        public Color Fill
        {
            get => _fill;
            set
            {
                if (_fill == value) return;
                _fill = value;
                QueueRedraw();
            }
        }

        public float Target
        {
            get => _target;
            set
            {
                if (Mathf.IsEqualApprox(_target, value)) return;
                _target = value;
                SetProcess(true);
            }
        }

        public override void _Process(double delta)
        {
            _shown = Mathf.Lerp(_shown, _target, 1f - Mathf.Exp(-GlideRate * (float)delta));
            if (Mathf.Abs(_shown - _target) < SettleEpsilon)
            {
                _shown = _target;
                SetProcess(false);
            }
            QueueRedraw();
        }

        public override void _Draw()
        {
            DrawRect(new Rect2(Vector2.Zero, Size), _track);
            if (_shown > 0f) DrawRect(new Rect2(0f, 0f, Size.X * _shown, Size.Y), _fill);
        }
    }
}
