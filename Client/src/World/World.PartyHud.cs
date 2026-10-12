using System.Collections.Generic;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const string PartyHudLayoutId = "hud_party";
    private const float PartyHudWidth = 290f;
    private const float PartyHudTop = 176f;
    private const int PartyHudHeaderGap = 6;
    private const float PartyRowInsetX = 4f;
    private const float PartyRowInsetY = 3f;
    private const float PartyRowGap = 4f;
    private const float PartyRowBodyWidth = PartyHudWidth - 2f * PartyRowInsetX;
    private const float PartyDiamondSize = 48f;
    private static readonly Rect2 PartyNameLine = new(50f, -5f, 214f, 20f);
    private static readonly Rect2 PartyHpRect = new(43f, 15f, 222f, 9f);
    private static readonly Rect2 PartyMpRect = new(44f, 26f, 221f, 8f);
    private static readonly Rect2 PartySignalRect = new(266f, 0f, 16f, 14f);
    private const float PartyBuffLeft = 50f;
    private const float PartyBuffTop = 38f;
    private const float PartyBuffIconSize = 16f;
    private const float PartyBuffIconGap = 3f;
    private const int PartyBuffLetterFont = 9;
    private const int PartyHudTitleFont = 16;
    private const int PartyHudNameFont = 14;
    private const int PartyNameLineSeparation = 5;
    private const int PartyTextOutline = 3;
    private const float PartyHeaderIconSize = 20f;
    private const float PartyGearSize = 22f;
    private const int PartyGearIconSize = 14;
    private const int PartyHeaderSeparation = 8;
    private const int PartyHeaderMarginX = 6;
    private const int PartyHeaderMarginY = 3;
    private const float PartyDragThreshold = 6f;
    private const double PartyRowSlideSeconds = 0.12;
    private const int PartyLiftedZ = 1;
    private const float PartyConfigWidth = 260f;
    private const float PartyConfigGap = 4f;
    private const int PartyConfigFont = 12;
    private const int PartyConfigSeparation = 6;
    private const string PartyRejoinLabel = "Re-invite after disconnection";
    private const string PartyWarBoardLabel = "Show Party War Board";
    private const string PartyCommanderLabel = "Show Commander Board";
    private const string PartyCommanderOnlyTip = "Only the party commander has the commander board.";
    private const string PartyLeaveLabel = "Leave Party";
    private const int TextPartyMakeLeader = 3431;
    private const int TextPartyKick = 3402;
    private const int TextPartyDisband = 3401;
    private const int TextPartyQuit = 3403;
    private const int PartyMenuMakeLeader = 1;
    private const int PartyMenuKick = 2;
    private const int PartyMenuCommander = 3;

    private static readonly Color PartyBarTrack = new(0.02f, 0.02f, 0.02f, 0.9f);
    private static readonly Color PartyLeaderNameColour = new(1f, 1f, 0f);
    private static readonly Color PartyNameColour = new(1f, 1f, 1f);
    private static readonly Color PartyLevelColour = new("e0bf7c");
    private static readonly Color PartyTitleColour = new("e3c88f");
    private static readonly Color PartyGearBg = new(0.08f, 0.09f, 0.11f, 0.95f);
    private static readonly Color PartyGearHoverBg = new(0.13f, 0.14f, 0.17f, 0.98f);
    private static readonly Color PartyTextOutlineColour = new(0f, 0f, 0f, 0.7f);
    private static readonly Color PartyHpRibbonFill = new("f02a2a");
    private static readonly Color PartyMpRibbonFill = new("2f86f0");
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
        public StyleBoxEmpty Style = null!;
        public Control Body = null!;
        public LevelOrb Diamond = null!;
        public StatBar Hp = null!;
        public StatBar Mp = null!;
        public Label Level = null!;
        public Label Name = null!;
        public PartySignalIcon Signal = null!;
        public Control Buffs = null!;
        public readonly List<int> BuffIds = new();
        public int MemberId = -1;
        public int Class = -1;
        public float TargetY = float.NaN;
        public bool Fresh;
        public Tween? Slide;
    }

    private sealed class PartyRowDrag(PartyHudRow row, float pressY, float grab)
    {
        public readonly PartyHudRow Row = row;
        public readonly float PressY = pressY;
        public readonly float Grab = grab;
        public bool Lifted;
        public int Index;
    }

    private VBoxContainer? _partyHud;
    private CanvasLayer _partyHudLayer = null!;
    private Control _partyHudList = null!;
    private Button _partyGear = null!;
    private readonly PartyHudRow[] _partyHudRows = new PartyHudRow[PartyHud.MaxRows];
    private readonly List<int> _partyHudOrder = new();
    private PartyRowDrag? _partyDrag;
    private PopupMenu _partyHudMenu = null!;
    private int _partyHudMenuMember = -1;
    private long _partyHudSecond = -1;
    private long _partySignalSecond = -1;
    private bool _partyHudCycles;
    private PopupPanel? _partyConfig;
    private CheckButton _partyConfigRejoin = null!;
    private CheckButton _partyConfigBoard = null!;
    private CheckButton _partyConfigCommander = null!;

    private void BuildPartyHud()
    {
        var layer = new CanvasLayer { Layer = 64 };
        AddChild(layer);
        _partyHudLayer = layer;
        PluginHudSeam(layer, LibreKO.Plugins.HudPart.Party);
        var hud = new VBoxContainer
        {
            Visible = false,
            CustomMinimumSize = new Vector2(PartyHudWidth, 0),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        hud.AddThemeConstantOverride("separation", PartyHudHeaderGap);
        layer.AddChild(hud);

        var header = BuildPartyHudHeader();
        hud.AddChild(header);
        _partyHudList = new Control
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(PartyHudWidth, 0),
        };
        hud.AddChild(_partyHudList);
        for (int i = 0; i < PartyHud.MaxRows; i++)
        {
            _partyHudRows[i] = BuildPartyHudRow();
            _partyHudList.AddChild(_partyHudRows[i].Frame);
        }

        _partyHudMenu = new PopupMenu();
        _partyHudMenu.IdPressed += OnPartyHudMenu;
        layer.AddChild(_partyHudMenu);

        _partyHud = hud;
        HudLayout.Attach(hud, PartyHudLayoutId, header, () => new Vector2(HudAnchor.Edge, PartyHudTop));
        RefreshPartyHud();
    }

    private PanelContainer BuildPartyHudHeader()
    {
        var style = new StyleBoxEmpty();
        style.ContentMarginLeft = style.ContentMarginRight = PartyHeaderMarginX;
        style.ContentMarginTop = style.ContentMarginBottom = PartyHeaderMarginY;
        var header = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        header.AddThemeStyleboxOverride("panel", style);
        var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        line.AddThemeConstantOverride("separation", PartyHeaderSeparation);
        header.AddChild(line);
        line.AddChild(new TextureRect
        {
            Texture = UiIcons.Get("system/users-three"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(PartyHeaderIconSize, PartyHeaderIconSize),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            SelfModulate = PartyTitleColour,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        var title = UiTheme.Text("Party", PartyHudTitleFont, PartyTitleColour);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        title.MouseFilter = Control.MouseFilterEnum.Ignore;
        line.AddChild(title);
        _partyGear = UiTheme.IconButton(UiIcons.Get("system/gear"), "Party settings");
        _partyGear.CustomMinimumSize = new Vector2(PartyGearSize, PartyGearSize);
        _partyGear.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        _partyGear.AddThemeConstantOverride("icon_max_width", PartyGearIconSize);
        _partyGear.AddThemeColorOverride("icon_normal_color", UiTheme.TextHi);
        _partyGear.AddThemeStyleboxOverride("normal", PartyGearStyle(PartyGearBg, PartyGearBg));
        _partyGear.AddThemeStyleboxOverride("pressed", PartyGearStyle(PartyGearBg, PartyGearBg));
        _partyGear.AddThemeStyleboxOverride("hover", PartyGearStyle(PartyGearHoverBg, UiTheme.Gold));
        _partyGear.Pressed += OpenPartyConfig;
        line.AddChild(_partyGear);
        return header;
    }

    private static StyleBoxFlat PartyGearStyle(Color background, Color edge)
    {
        var style = new StyleBoxFlat { BgColor = background, BorderColor = edge };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(2);
        return style;
    }

    private PartyHudRow BuildPartyHudRow()
    {
        var row = new PartyHudRow
        {
            Style = new StyleBoxEmpty(),
            Frame = new PanelContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Stop },
            Body = new Control
            {
                MouseFilter = Control.MouseFilterEnum.Ignore,
                CustomMinimumSize = new Vector2(PartyRowBodyWidth, PartyRowHeight - 2f * PartyRowInsetY),
            },
            Diamond = new LevelOrb(PartyDiamondSize)
            {
                Size = new Vector2(PartyDiamondSize, PartyDiamondSize),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            },
            Hp = new StatBar(PartyHpRibbonFill, PartyHpRect.Size, StatBar.RibbonKind.Upper)
            {
                Position = PartyHpRect.Position,
                Size = PartyHpRect.Size,
                Glide = true,
                ShowNumbers = false,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            },
            Mp = new StatBar(PartyMpRibbonFill, PartyMpRect.Size, StatBar.RibbonKind.Lower)
            {
                Position = PartyMpRect.Position,
                Size = PartyMpRect.Size,
                Glide = true,
                ShowNumbers = false,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            },
            Level = PartyRowText(PartyLevelColour),
            Name = PartyRowText(PartyNameColour),
            Signal = new PartySignalIcon
            {
                Position = PartySignalRect.Position,
                Size = PartySignalRect.Size,
                CustomMinimumSize = PartySignalRect.Size,
                MouseFilter = Control.MouseFilterEnum.Pass,
            },
            Buffs = new Control
            {
                Position = new Vector2(PartyBuffLeft, PartyBuffTop),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            },
        };
        row.Style.ContentMarginLeft = row.Style.ContentMarginRight = PartyRowInsetX;
        row.Style.ContentMarginTop = row.Style.ContentMarginBottom = PartyRowInsetY;
        row.Frame.AddThemeStyleboxOverride("panel", row.Style);
        row.Frame.AddChild(row.Body);

        row.Body.AddChild(row.Hp);
        row.Body.AddChild(row.Mp);
        row.Body.AddChild(row.Diamond);
        var line = new HBoxContainer
        {
            Position = PartyNameLine.Position,
            Size = PartyNameLine.Size,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        line.AddThemeConstantOverride("separation", PartyNameLineSeparation);
        line.AddChild(row.Level);
        row.Name.ClipText = true;
        row.Name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        row.Name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        line.AddChild(row.Name);
        row.Body.AddChild(line);
        row.Body.AddChild(row.Signal);
        row.Body.AddChild(row.Buffs);

        row.Frame.GuiInput += ev => OnPartyHudRowInput(row, ev);
        return row;
    }

    private static Label PartyRowText(Color colour)
    {
        var label = UiTheme.Text("", PartyHudNameFont, colour);
        label.AddThemeConstantOverride("outline_size", PartyTextOutline);
        label.AddThemeColorOverride("font_outline_color", PartyTextOutlineColour);
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        return label;
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
        int leader = PartyMembers.Count > 0 ? PartyMembers[0].CharId : -1;
        var members = shown ? PartyRows : new List<PartyMember>();
        if (members.Count > PartyHud.MaxRows) members.RemoveRange(PartyHud.MaxRows, members.Count - PartyHud.MaxRows);
        _partyHudOrder.Clear();
        foreach (var m in members) _partyHudOrder.Add(m.CharId);
        foreach (var row in _partyHudRows)
            if (row.MemberId >= 0 && !_partyHudOrder.Contains(row.MemberId)) ReleasePartyRow(row);
        foreach (var m in members)
            FillPartyHudRow(PartyRowFor(m.CharId) ?? ClaimPartyRow(m.CharId), m, m.CharId == leader, second);
        PlacePartyRows();
    }

    private PartyHudRow? PartyRowFor(int memberId)
    {
        foreach (var row in _partyHudRows)
            if (row.MemberId == memberId) return row;
        return null;
    }

    private PartyHudRow ClaimPartyRow(int memberId)
    {
        var row = System.Array.Find(_partyHudRows, r => r.MemberId < 0)!;
        row.MemberId = memberId;
        row.Fresh = true;
        row.TargetY = float.NaN;
        row.Hp.SnapNext();
        row.Mp.SnapNext();
        row.Frame.Visible = true;
        return row;
    }

    private void ReleasePartyRow(PartyHudRow row)
    {
        if (_partyDrag?.Row == row) _partyDrag = null;
        row.Slide?.Kill();
        row.Slide = null;
        row.MemberId = -1;
        row.TargetY = float.NaN;
        row.Frame.ZIndex = 0;
        row.Frame.Visible = false;
    }

    private readonly record struct PartyRowView(int Level, int Class, int Hp, int MaxHp, int Mp, int MaxMp,
        Color NameColour, Color HpColour, string Tooltip, bool Cycles, PartyHpLook HpLook, double LevelPercent, int PingMs);

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
        var look = PartyHud.HpLook(statuses, second);
        return new PartyRowView(level, cls, hp, maxHp, mp, maxMp, PartyNameColourOf(m.CharId, leader),
            PartyHpFills[look],
            $"{m.Name}  Lv {level} {ClassName(cls)}\nHP {hp} / {maxHp}   MP {mp} / {maxMp}{status}",
            statuses is { Count: > 1 }, look, PartyLevelPercentOf(m.CharId), PartyPingOf(m.CharId));
    }

    private int PartyPingOf(int memberId) => memberId == _myId ? Net.I.PingMs : Net.I.PartyPingOf(memberId);

    private double PartyLevelPercentOf(int memberId)
    {
        if (memberId == _myId) return Sheet.ExpPercent;
        return Net.I.PartyLevelPermilleOf(memberId) is { } permille ? PartyHud.LevelPercent(permille) : 0.0;
    }

    private void FillPartyHudRow(PartyHudRow row, PartyMember m, bool leader, long second)
    {
        var view = PartyRowOf(m, leader, second);
        if (view.Cycles) _partyHudCycles = true;

        row.Hp.FillOverride = view.HpLook == PartyHpLook.Normal ? null : view.HpColour;
        row.Hp.Set(view.Hp, view.MaxHp);
        row.Mp.Set(view.Mp, view.MaxMp);
        row.Hp.Visible = view.MaxHp > 0;
        row.Mp.Visible = view.MaxMp > 0;
        string level = $"Lv.{view.Level}";
        if (row.Level.Text != level) row.Level.Text = level;
        if (row.Name.Text != m.Name) row.Name.Text = m.Name;
        row.Name.AddThemeColorOverride("font_color", view.NameColour);
        if (row.Class != view.Class)
        {
            row.Class = view.Class;
            row.Diamond.Icon = UiIcons.Get(PartyHud.ClassIcon(view.Class));
        }
        row.Diamond.SetGauge(view.LevelPercent);
        row.Signal.Ms = view.PingMs;
        row.Frame.TooltipText = view.Tooltip;
        ApplyPartyRowLook(row);
        FillPartyBuffs(row, Net.I.PartyBuffsOf(m.CharId));
    }

    private void ApplyPartyRowLook(PartyHudRow row) =>
        row.Diamond.Highlighted = row.MemberId >= 0 && row.MemberId == PartyHighlight;

    private void FollowPartyHighlight()
    {
        _partyPick = PartyHud.KeepPick(_partyPick, _selectedId, _partyPickSelection);
        int highlight = PartyHighlight;
        if (highlight == _partyHudHighlight) return;
        _partyHudHighlight = highlight;
        foreach (var row in _partyHudRows) ApplyPartyRowLook(row);
        PluginNotifyParty();
    }

    private const float PartyRowHeight = PartyBuffTop + PartyBuffIconSize + 2f * PartyRowInsetY;

    private void PlacePartyRows()
    {
        var order = new List<PartyHudRow>(_partyHudOrder.Count);
        foreach (int id in _partyHudOrder)
            if (PartyRowFor(id) is { } row) order.Add(row);
        var lifted = _partyDrag is { Lifted: true } drag ? drag.Row : null;
        if (lifted != null && order.Remove(lifted)) order.Insert(Mathf.Clamp(_partyDrag!.Index, 0, order.Count), lifted);
        float y = 0f;
        foreach (var row in order)
        {
            float height = PartyRowHeight;
            row.Frame.Size = new Vector2(PartyHudWidth, height);
            if (row != lifted) SlidePartyRow(row, y);
            y += height + PartyRowGap;
        }
        float listHeight = order.Count > 0 ? y - PartyRowGap : 0f;
        if (Mathf.IsEqualApprox(_partyHudList.CustomMinimumSize.Y, listHeight)) return;
        _partyHudList.CustomMinimumSize = new Vector2(PartyHudWidth, listHeight);
        _partyHud?.ResetSize();
    }

    private static void SlidePartyRow(PartyHudRow row, float y)
    {
        if (Mathf.IsEqualApprox(row.TargetY, y)) return;
        row.TargetY = y;
        row.Slide?.Kill();
        row.Slide = null;
        bool snap = row.Fresh || !row.Frame.IsInsideTree();
        row.Fresh = false;
        if (snap)
        {
            row.Frame.Position = new Vector2(0f, y);
            return;
        }
        row.Slide = row.Frame.CreateTween();
        row.Slide.TweenProperty(row.Frame, "position:y", y, PartyRowSlideSeconds)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
    }

    private static void FillPartyBuffs(PartyHudRow row, IReadOnlyList<int> skillIds)
    {
        if (row.BuffIds.SequenceEqual(skillIds)) return;
        row.BuffIds.Clear();
        row.BuffIds.AddRange(skillIds);
        foreach (var icon in row.Buffs.GetChildren())
        {
            row.Buffs.RemoveChild(icon);
            icon.QueueFree();
        }
        float pitch = PartyBuffIconSize + PartyBuffIconGap;
        int perLine = Mathf.Max(1, (int)((PartyRowBodyWidth - PartyBuffLeft + PartyBuffIconGap) / pitch));
        int count = 0;
        foreach (int skillId in skillIds)
        {
            if (count == perLine) break;
            if (SkillData.Get(skillId) is not { } skill) continue;
            var icon = PartyBuffIcon(skill);
            icon.Position = new Vector2(count * pitch, 0f);
            icon.Size = new Vector2(PartyBuffIconSize, PartyBuffIconSize);
            row.Buffs.AddChild(icon);
            count++;
        }
        row.Buffs.Size = new Vector2(PartyRowBodyWidth - PartyBuffLeft, PartyBuffIconSize);
    }

    private static Control PartyBuffIcon(SkillData.Skill skill)
    {
        var size = new Vector2(PartyBuffIconSize, PartyBuffIconSize);
        if (SkillData.Icon(skill.Id) is { } texture)
            return new TextureRect
            {
                Texture = texture,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = size,
                MouseFilter = Control.MouseFilterEnum.Pass,
                TooltipText = BuffTooltip(skill),
            };

        var letter = UiTheme.Text(skill.Name.Length > 0 ? skill.Name[..1] : "?", PartyBuffLetterFont, PartyNameColour,
            HorizontalAlignment.Center);
        letter.CustomMinimumSize = size;
        letter.VerticalAlignment = VerticalAlignment.Center;
        letter.MouseFilter = Control.MouseFilterEnum.Pass;
        letter.TooltipText = BuffTooltip(skill);
        return letter;
    }

    private void RefreshPartySignals()
    {
        bool changed = false;
        foreach (var row in _partyHudRows)
        {
            if (row.MemberId < 0) continue;
            int ms = PartyPingOf(row.MemberId);
            if (row.Signal.Ms != ms)
            {
                row.Signal.Ms = ms;
                changed = true;
            }
            if (row.MemberId == _myId) row.Diamond.SetGauge(Sheet.ExpPercent);
        }
        if (changed) PluginNotifyParty();
    }

    private void PartyHudTick(double now)
    {
        WarBoardTick(now);
        UserVoteTick(now);
        FullMoonAutoTick(now);
        FollowPartyHighlight();
        long second = (long)now;
        if (_partyHudCycles && second != _partyHudSecond) RefreshPartyHud();
        if (second == _partySignalSecond) return;
        _partySignalSecond = second;
        if (InParty) RefreshPartySignals();
    }

    private void OnPartyHudRowInput(PartyHudRow row, InputEvent ev)
    {
        if (row.MemberId < 0) return;
        if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Left } left)
        {
            float y = row.Frame.Position.Y + left.Position.Y;
            if (left.Pressed) _partyDrag = new PartyRowDrag(row, y, left.Position.Y);
            else DropPartyRow(row);
            row.Frame.AcceptEvent();
        }
        else if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
        {
            if (ShowPartyMemberMenu(row.MemberId)) row.Frame.AcceptEvent();
        }
        else if (ev is InputEventMouseMotion motion && _partyDrag?.Row == row)
        {
            DragPartyRow(row.Frame.Position.Y + motion.Position.Y);
            row.Frame.AcceptEvent();
        }
    }

    private void DragPartyRow(float listY)
    {
        if (_partyDrag is not { } drag) return;
        var row = drag.Row;
        if (!drag.Lifted)
        {
            if (Mathf.Abs(listY - drag.PressY) < PartyDragThreshold || _partyHudOrder.Count < 2) return;
            drag.Lifted = true;
            drag.Index = _partyHudOrder.IndexOf(row.MemberId);
            row.Slide?.Kill();
            row.Slide = null;
            row.TargetY = float.NaN;
            row.Frame.ZIndex = PartyLiftedZ;
            ApplyPartyRowLook(row);
        }
        float height = row.Frame.Size.Y;
        float top = Mathf.Clamp(listY - drag.Grab, 0f, Mathf.Max(0f, _partyHudList.CustomMinimumSize.Y - height));
        row.Frame.Position = new Vector2(0f, top);
        int index = PartyOrder.DropIndex(OtherPartyRowCentres(row), top);
        if (index == drag.Index) return;
        drag.Index = index;
        PlacePartyRows();
    }

    private List<float> OtherPartyRowCentres(PartyHudRow dragged)
    {
        var centres = new List<float>(_partyHudOrder.Count);
        float y = 0f;
        foreach (int id in _partyHudOrder)
        {
            if (PartyRowFor(id) is not { } row || row == dragged) continue;
            float height = PartyRowHeight;
            centres.Add(y + height * 0.5f);
            y += height + PartyRowGap;
        }
        return centres;
    }

    private void DropPartyRow(PartyHudRow row)
    {
        var drag = _partyDrag;
        _partyDrag = null;
        if (drag == null || drag.Row != row) return;
        if (!drag.Lifted)
        {
            PickPartyMember(row.MemberId);
            return;
        }
        row.Frame.ZIndex = 0;
        _partyOrder.MoveTo(row.MemberId, drag.Index);
        RefreshPartyHud();
    }

    private void PickPartyMember(int memberId)
    {
        if (memberId != _myId && _ents.TryGetValue(memberId, out var member) && Selectable(member))
            Select(memberId, member);
        else
            Deselect();
        _partyPick = memberId;
        _partyPickSelection = _selectedId;
        RefreshPartyHud();
    }

    private void MovePartyRowTo(int memberId, int index)
    {
        if (_partyOrder.MoveTo(memberId, index)) RefreshPartyHud();
    }

    private void OpenPartyConfig()
    {
        if (_partyConfig == null) BuildPartyConfig();
        RefreshPartyConfig();
        var popup = _partyConfig!;
        popup.ResetSize();
        popup.Position = (Vector2I)PartyConfigSpot(popup.Size, GetViewport().GetVisibleRect().Size);
        popup.Popup();
    }

    private Vector2 PartyConfigSpot(Vector2 size, Vector2 screen)
    {
        var gear = _partyGear.GetGlobalRect();
        float x = Mathf.Clamp(gear.End.X + PartyConfigGap, 0f, Mathf.Max(0f, screen.X - size.X));
        float y = Mathf.Clamp(gear.Position.Y, 0f, Mathf.Max(0f, screen.Y - size.Y));
        return new Vector2(x, y);
    }

    private void BuildPartyConfig()
    {
        var style = UiTheme.WindowPanel(radius: 3, alpha: 1f);
        style.ContentMarginLeft = style.ContentMarginRight = 10;
        style.ContentMarginTop = style.ContentMarginBottom = 8;
        _partyConfig = new PopupPanel();
        _partyConfig.AddThemeStyleboxOverride("panel", style);
        _partyHudLayer.AddChild(_partyConfig);
        var column = new VBoxContainer { CustomMinimumSize = new Vector2(PartyConfigWidth, 0) };
        column.AddThemeConstantOverride("separation", PartyConfigSeparation);
        _partyConfig.AddChild(column);
        column.AddChild(UiTheme.SectionTitle("Party settings"));
        _partyConfigRejoin = PartyConfigToggle(column, PartyRejoinLabel, on =>
        {
            if (on != _partyRejoin.On) TogglePartyRejoin();
        });
        _partyConfigBoard = PartyConfigToggle(column, PartyWarBoardLabel, on =>
        {
            if (on != (_warBoard is { Visible: true })) ToggleWarBoard();
            RefreshPartyConfig();
        });
        _partyConfigCommander = PartyConfigToggle(column, PartyCommanderLabel, on =>
        {
            _commanderUserHidden = !on;
            RefreshCommanderBar();
        });
        var leave = UiTheme.SmallButton(PartyLeaveLabel, "");
        leave.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        leave.Pressed += () =>
        {
            _partyConfig.Hide();
            AskLeaveParty();
        };
        column.AddChild(leave);
    }

    private static CheckButton PartyConfigToggle(VBoxContainer column, string text, System.Action<bool> toggled)
    {
        var toggle = new CheckButton { Text = text, FocusMode = Control.FocusModeEnum.None };
        toggle.AddThemeFontSizeOverride("font_size", PartyConfigFont);
        toggle.AddThemeColorOverride("font_color", UiTheme.TextHi);
        toggle.AddThemeColorOverride("font_hover_color", UiTheme.GoldBright);
        toggle.AddThemeColorOverride("font_pressed_color", UiTheme.TextHi);
        toggle.AddThemeColorOverride("font_hover_pressed_color", UiTheme.GoldBright);
        toggle.AddThemeColorOverride("font_disabled_color", UiTheme.TextDim);
        toggle.Toggled += on => toggled(on);
        column.AddChild(toggle);
        return toggle;
    }

    private void RefreshPartyConfig()
    {
        if (_partyConfig == null) return;
        _partyConfigRejoin.SetPressedNoSignal(_partyRejoin.On);
        _partyConfigRejoin.TooltipText = PartyRejoinTip;
        _partyConfigBoard.SetPressedNoSignal(_warBoard is { Visible: true });
        _partyConfigCommander.SetPressedNoSignal(AmCommander && !_commanderUserHidden);
        _partyConfigCommander.Disabled = !AmCommander;
        _partyConfigCommander.TooltipText = AmCommander ? "" : PartyCommanderOnlyTip;
    }

    private bool ShowPartyMemberMenu(int memberId)
    {
        bool vote = _zone == UserVote.JuraidZone;
        if (memberId == _myId || (!AmLeader && !vote)) return false;
        _partyHudMenuMember = memberId;
        _partyHudMenu.Clear();
        if (AmLeader)
        {
            _partyHudMenu.AddItem("Make leader", PartyMenuMakeLeader);
            _partyHudMenu.AddItem("Kick out", PartyMenuKick);
            _partyHudMenu.AddItem("Make commander", PartyMenuCommander);
        }
        if (vote) _partyHudMenu.AddItem(ItemData.Text(UserVote.TitleText, "User Vote"), PartyMenuUserVote);
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
        if (member < 0) return;
        if (id == PartyMenuUserVote)
        {
            OpenUserVote(member);
            return;
        }
        if (!AmLeader) return;
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

    private sealed partial class PartySignalIcon : Control
    {
        private const int ArcCount = 3;
        private const float ArcFrom = -Mathf.Pi * 0.75f;
        private const float ArcTo = -Mathf.Pi * 0.25f;
        private const int ArcPoints = 12;
        private const float ArcWidth = 1.7f;
        private const float ShadowWidth = 3.2f;
        private const float DotRadius = 1.7f;
        private const float DotShadowGrow = 0.8f;
        private const float BaseInset = 1.5f;
        private const float TopInset = 1f;
        private const float StrikeWidth = 1.6f;
        private static readonly Color Green = new("4cd964");
        private static readonly Color Yellow = new("f2cf3a");
        private static readonly Color Red = new("e8483c");
        private static readonly Color Grey = new(0.56f, 0.56f, 0.56f);
        private static readonly Color Unlit = new(1f, 1f, 1f, 0.16f);
        private static readonly Color Shadow = new(0f, 0f, 0f, 0.45f);
        private static readonly Dictionary<PartySignalLevel, Color> Tints = new()
        {
            [PartySignalLevel.Strong] = Green,
            [PartySignalLevel.Good] = Yellow,
            [PartySignalLevel.Weak] = Red,
            [PartySignalLevel.Poor] = Red,
            [PartySignalLevel.Lost] = Grey,
            [PartySignalLevel.Unknown] = Grey,
        };

        private int _ms = PartySignal.Unknown;

        public PartySignalIcon() => TooltipText = PartySignal.Tooltip(_ms);

        public int Ms
        {
            get => _ms;
            set
            {
                if (_ms == value) return;
                _ms = value;
                TooltipText = PartySignal.Tooltip(value);
                QueueRedraw();
            }
        }

        public override void _Draw()
        {
            var level = PartySignal.Level(_ms);
            int lit = PartySignal.Arcs(level);
            bool lost = level == PartySignalLevel.Lost;
            Color tint = Tints[level];
            var centre = new Vector2(Size.X * 0.5f, Size.Y - DotRadius - BaseInset);
            float step = (centre.Y - TopInset) / ArcCount;
            for (int arc = 1; arc <= ArcCount; arc++)
            {
                float radius = step * arc;
                Color colour = arc <= lit || lost ? tint : Unlit;
                if (arc <= lit || lost) DrawArc(centre, radius, ArcFrom, ArcTo, ArcPoints, Shadow, ShadowWidth, true);
                DrawArc(centre, radius, ArcFrom, ArcTo, ArcPoints, colour, ArcWidth, true);
            }
            DrawCircle(centre, DotRadius + DotShadowGrow, Shadow);
            DrawCircle(centre, DotRadius, tint);
            if (!lost) return;
            var from = new Vector2(TopInset, TopInset);
            var to = new Vector2(Size.X - TopInset, Size.Y - TopInset);
            DrawLine(from, to, Shadow, ShadowWidth, true);
            DrawLine(from, to, tint, StrikeWidth, true);
        }
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
