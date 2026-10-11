using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const float CommanderButtonSize = 40f;
    private const float CommanderIconInset = 3f;
    private const int CommanderAlarmLayer = 88;
    private const int CommanderCooldownFont = 13;
    private const float CommanderBadgeSize = 88f;
    private const float CommanderGlyphSize = 66f;
    private const float CommanderGlyphInset = 11f;
    private const float CommanderRingSize = 86f;
    private const float CommanderRingInset = 1f;
    private const float CommanderSweepSize = 64f;
    private const float CommanderSweepInset = 12f;
    private const float CommanderPinSize = 50f;
    private const float CommanderPinLeft = 25f;
    private const float CommanderPinUp = 75f;
    private const float CommanderPinClanUp = 17f;
    private const float CommanderSheetCell = 68f;
    private const float CommanderSheetEdge = 2f;
    private const float CommanderRingEdge = 2f;
    private const float CommanderRingImage = 124f;
    private const float CommanderAlphaScale = 255f;
    private const string CommanderDir = "res://assets/ui/commander/";
    private const string CommanderMarkTexture = CommanderDir + "target.png";
    private const string CommanderFlashRed = CommanderDir + "flash_red.png";
    private const string CommanderFlashBlue = CommanderDir + "flash_blue.png";
    private const string CommanderBadgeSheet = CommanderDir + "badge.png";
    private static readonly Rect2 CommanderPlateRegion = new(138f, 2f, 88f, 88f);
    private static readonly Color CommanderNameColour = new(0f, 1f, 0f);
    private static readonly Color CommanderNoTargetColour = new("ff3b3b");

    private HudWindow? _commanderBar;
    private readonly Button[] _commanderButtons = new Button[CommanderOrders.Bar.Length];
    private readonly Label[] _commanderCooldowns = new Label[CommanderOrders.Bar.Length];
    private BoxContainer _commanderSlots = null!;
    private bool _commanderUserHidden;
    private TextureRect _commanderPin = null!;
    private int _commanderMarkId = -1;
    private double _commanderMarkSentAt;
    private CanvasLayer _commanderAlarmLayer = null!;
    private TextureRect _commanderFlash = null!;
    private Control _commanderBadge = null!;
    private TextureRect _commanderGlyph = null!, _commanderRing = null!, _commanderSweep = null!;
    private readonly CommanderPulse _commanderPulse = new();
    private Texture2D[] _commanderRingBlue = System.Array.Empty<Texture2D>(), _commanderRingRed = System.Array.Empty<Texture2D>();
    private Texture2D[] _commanderSweepFrames = System.Array.Empty<Texture2D>();
    private bool _commanderRedRing;

    private bool AmCommander => InParty && Net.I.PartyCommanderId == _myId;

    private void PartyCommanderInit()
    {
        BuildCommanderBar();
        BuildCommanderAlarm();
        Net.I.PartyCommanderEvent += OnPartyCommander;
        Net.I.CommanderMarkEvent += OnCommanderMark;
        Net.I.CommanderOrderEvent += OnCommanderOrder;
        Net.I.PartyDisbandEvent += OnCommanderPartyGone;
        Net.I.PartyRemovedEvent += OnCommanderMemberGone;
        RefreshCommanderBar();
    }

    private void PartyCommanderDispose()
    {
        Net.I.PartyCommanderEvent -= OnPartyCommander;
        Net.I.CommanderMarkEvent -= OnCommanderMark;
        Net.I.CommanderOrderEvent -= OnCommanderOrder;
        Net.I.PartyDisbandEvent -= OnCommanderPartyGone;
        Net.I.PartyRemovedEvent -= OnCommanderMemberGone;
    }

    private void BuildCommanderBar()
    {
        _commanderBar = new HudWindow("commander", "Commander") { Visible = false };
        _commanderBar.Closed += () =>
        {
            _commanderUserHidden = true;
            _commanderBar.Visible = false;
        };
        var layer = new CanvasLayer { Layer = 70 };
        AddChild(layer);
        layer.AddChild(_commanderBar);
        var flip = UiTheme.SmallButton("\u21c4", "Change the bar direction");
        flip.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        flip.Pressed += FlipCommanderBar;
        _commanderBar.Body.AddChild(flip);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);
        _commanderBar.Body.AddChild(row);
        _commanderSlots = row;
        for (int i = 0; i < CommanderOrders.Bar.Length; i++)
        {
            int slot = i;
            var skill = SkillData.Get(CommanderOrders.Bar[i].Skill);
            var button = new Button
            {
                FocusMode = Control.FocusModeEnum.None,
                CustomMinimumSize = new Vector2(CommanderButtonSize, CommanderButtonSize),
                TooltipText = CommanderTooltip(skill, slot),
            };
            var icon = new TextureRect
            {
                Texture = SkillData.Icon(CommanderOrders.Bar[i].Skill),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            icon.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            icon.OffsetLeft = icon.OffsetTop = CommanderIconInset;
            icon.OffsetRight = icon.OffsetBottom = -CommanderIconInset;
            button.AddChild(icon);
            button.Pressed += () => UseCommanderSlot(slot);
            var cooldown = UiTheme.Text("", CommanderCooldownFont, UiTheme.TextHi, HorizontalAlignment.Center);
            cooldown.VerticalAlignment = VerticalAlignment.Center;
            cooldown.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            cooldown.MouseFilter = Control.MouseFilterEnum.Ignore;
            button.AddChild(cooldown);
            _commanderButtons[i] = button;
            _commanderCooldowns[i] = cooldown;
            row.AddChild(button);
        }
    }

    private void FlipCommanderBar()
    {
        var parent = _commanderSlots.GetParent();
        BoxContainer flipped = _commanderSlots is HBoxContainer ? new VBoxContainer() : new HBoxContainer();
        flipped.AddThemeConstantOverride("separation", 4);
        foreach (var button in _commanderButtons)
            button.Reparent(flipped);
        parent.AddChild(flipped);
        _commanderSlots.QueueFree();
        _commanderSlots = flipped;
        _commanderBar?.ResetSize();
    }

    private static string CommanderTooltip(SkillData.Skill? skill, int slot)
    {
        string key = KeyBinds.Get(KeyAction.CommanderSlot1 + slot).Text;
        string name = skill?.Name ?? "";
        string desc = skill?.Desc ?? "";
        return desc.Length > 0 ? $"{name}\n{desc}\n{key}" : $"{name}\n{key}";
    }

    private void BuildCommanderAlarm()
    {
        _commanderAlarmLayer = new CanvasLayer { Layer = CommanderAlarmLayer };
        AddChild(_commanderAlarmLayer);
        _commanderFlash = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
        };
        _commanderFlash.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _commanderAlarmLayer.AddChild(_commanderFlash);

        var sheet = ResourceLoader.Exists(CommanderBadgeSheet) ? GD.Load<Texture2D>(CommanderBadgeSheet) : null;
        _commanderBadge = new Control
        {
            CustomMinimumSize = new Vector2(CommanderBadgeSize, CommanderBadgeSize),
            Size = new Vector2(CommanderBadgeSize, CommanderBadgeSize),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
        };
        _commanderAlarmLayer.AddChild(_commanderBadge);
        _commanderBadge.AddChild(CommanderImage(sheet == null ? null : new AtlasTexture { Atlas = sheet, Region = CommanderPlateRegion },
            0f, CommanderBadgeSize));
        _commanderGlyph = CommanderImage(null, CommanderGlyphInset, CommanderGlyphSize);
        _commanderBadge.AddChild(_commanderGlyph);
        _commanderRing = CommanderImage(null, CommanderRingInset, CommanderRingSize);
        _commanderRing.Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
        _commanderBadge.AddChild(_commanderRing);
        _commanderSweep = CommanderImage(null, CommanderSweepInset, CommanderSweepSize);
        _commanderSweep.Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
        _commanderBadge.AddChild(_commanderSweep);
        _commanderRingBlue = CommanderFrames("ring_blue_", CommanderPulse.RingImages, true);
        _commanderRingRed = CommanderFrames("ring_red_", CommanderPulse.RingImages, true);
        _commanderSweepFrames = CommanderFrames("sweep_", CommanderPulse.SweepImages, false);

        _commanderPin = new TextureRect
        {
            Texture = ResourceLoader.Exists(CommanderMarkTexture) ? GD.Load<Texture2D>(CommanderMarkTexture) : null,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            Size = new Vector2(CommanderPinSize, CommanderPinSize),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
        };
        _commanderAlarmLayer.AddChild(_commanderPin);
    }

    private static TextureRect CommanderImage(Texture2D? texture, float inset, float size) => new()
    {
        Texture = texture,
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.Scale,
        Position = new Vector2(inset, inset),
        Size = new Vector2(size, size),
        MouseFilter = Control.MouseFilterEnum.Ignore,
    };

    private static Texture2D[] CommanderFrames(string stem, int count, bool trimEdge)
    {
        var frames = new Texture2D[count];
        for (int i = 0; i < count; i++)
        {
            string path = CommanderDir + stem + i + ".png";
            var image = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
            frames[i] = image != null && trimEdge
                ? new AtlasTexture { Atlas = image, Region = new Rect2(CommanderRingEdge, CommanderRingEdge, CommanderRingImage, CommanderRingImage) }
                : image!;
        }
        return frames;
    }

    private static AtlasTexture? CommanderGlyphTexture(Texture2D? sheet, int glyph)
    {
        if (sheet == null || glyph < 0) return null;
        int column = glyph % 2, row = glyph / 2;
        return new AtlasTexture
        {
            Atlas = sheet,
            Region = new Rect2(CommanderSheetEdge + column * CommanderSheetCell, CommanderSheetEdge + row * CommanderSheetCell,
                CommanderGlyphSize, CommanderGlyphSize),
        };
    }

    private void PlaceCommanderBadge()
    {
        if (!IsInsideTree()) return;
        var screen = GetViewport().GetVisibleRect().Size;
        _commanderBadge.Position = screen * 0.5f;
    }

    private void RefreshCommanderBar()
    {
        if (_commanderBar == null) return;
        _commanderBar.Visible = AmCommander && !_commanderUserHidden;
    }

    private void OnPartyCommander(CommanderNamedReply reply)
    {
        if (reply.CharId == _myId) _commanderUserHidden = false;
        CombatNotice(reply.CharId == _myId
            ? SystemText(CommanderOrders.YouText, "Now, you are the commander")
            : TextTemplate.Fill(SystemText(CommanderOrders.NamedText, "Now, %s was a commander"), reply.Name));
        ClearCommanderMark();
        RefreshCommanderBar();
        RefreshPartyHud();
    }

    private void OnCommanderPartyGone()
    {
        ClearCommanderMark();
        RefreshCommanderBar();
    }

    private void OnCommanderMemberGone(int charId, string name)
    {
        if (charId == _myId || Net.I.PartyCommanderId < 0) ClearCommanderMark();
        RefreshCommanderBar();
    }

    private void OnCommanderMark(CommanderMarkReply reply)
    {
        if (reply.Clear) { ClearCommanderMark(); return; }
        PlaceCommanderMark(reply.TargetId);
        Audio.PlayUi(CommanderOrders.MarkSound);
    }

    private void PlaceCommanderMark(int targetId)
    {
        _commanderMarkId = targetId;
    }

    private void ClearCommanderMark()
    {
        _commanderPin.Visible = false;
        _commanderMarkId = -1;
        _commanderMarkSentAt = 0;
    }

    private void UpdateCommanderPin()
    {
        if (_commanderMarkId < 0 || _camera == null)
        {
            _commanderPin.Visible = false;
            return;
        }
        _ents.TryGetValue(_commanderMarkId, out var e);
        Node3D? host = _commanderMarkId == _myId ? _self : e?.Body;
        bool hidden = e != null && (e.StealthUndetected || e.Infiltrating);
        if (host == null || hidden || host.GlobalPosition.DistanceTo(_self.GlobalPosition) > CommanderOrders.PinReach)
        {
            _commanderPin.Visible = false;
            return;
        }
        var head = host.GlobalPosition + new Vector3(0f, HeadHeightOf(_commanderMarkId), 0f);
        if (_camera.IsPositionBehind(head))
        {
            _commanderPin.Visible = false;
            return;
        }
        var point = _camera.UnprojectPosition(head);
        bool clan = e != null ? e.KnightsId > 0 : MyClan.InClan;
        _commanderPin.Position = point - new Vector2(CommanderPinLeft, CommanderPinUp + (clan ? CommanderPinClanUp : 0f));
        _commanderPin.Visible = true;
    }

    private void OnCommanderOrder(byte order)
    {
        int glyph = CommanderOrders.Glyph(order);
        if (glyph == CommanderOrders.NoGlyph) return;
        _commanderFlash.Texture = GD.Load<Texture2D>(CommanderOrders.RedFlash(order) ? CommanderFlashRed : CommanderFlashBlue);
        var sheet = ResourceLoader.Exists(CommanderBadgeSheet) ? GD.Load<Texture2D>(CommanderBadgeSheet) : null;
        _commanderGlyph.Texture = CommanderGlyphTexture(sheet, CommanderOrders.SheetCell(order));
        _commanderRedRing = CommanderOrders.RedRing(glyph);
        _commanderPulse.Start();
        PlaceCommanderBadge();
        _commanderFlash.Visible = true;
        _commanderBadge.Visible = true;
        ShowCommanderPulse();
        if (CommanderOrders.Sound(order) is var sound and > 0) Audio.PlayUi(sound);
    }

    private void ShowCommanderPulse()
    {
        _commanderFlash.Modulate = new Color(1f, 1f, 1f, _commanderPulse.Alpha / CommanderAlphaScale);
        var ring = _commanderRedRing ? _commanderRingRed : _commanderRingBlue;
        _commanderRing.Texture = ring.Length > 0 ? ring[_commanderPulse.RingImage] : null;
        _commanderSweep.Visible = _commanderPulse.Sweeping;
        _commanderSweep.Texture = _commanderSweepFrames.Length > 0 ? _commanderSweepFrames[_commanderPulse.SweepImage] : null;
    }

    private void CommanderFrame(double delta)
    {
        UpdateCommanderPin();
        if (!_commanderPulse.Running) return;
        _commanderPulse.Frame(delta);
        if (_commanderPulse.Running)
        {
            ShowCommanderPulse();
            return;
        }
        _commanderFlash.Visible = false;
        _commanderBadge.Visible = false;
    }

    private void PressCommanderKey(int slot)
    {
        CombatNotice(SystemText(CommanderOrders.KeyHintText,
            "You have entered the pet or commander related shortcut key (Caps Lock or ALT)."), new Color(1f, 1f, 0f));
        UseCommanderSlot(slot);
    }

    private void UseCommanderSlot(int slot)
    {
        if (!AmCommander || slot < 0 || slot >= CommanderOrders.Bar.Length) return;
        var entry = CommanderOrders.Bar[slot];
        var skill = SkillData.Get(entry.Skill);
        if (skill == null || SkillOnCooldown(skill, Now())) return;
        if (entry.Order == CommanderOrders.None)
        {
            if (_selectedId < 0)
            {
                CombatLogAdd(SystemText(CommanderOrders.NoTargetText, "No target."), CombatLogKind.Status, CommanderNoTargetColour);
                return;
            }
            bool clear = CommanderOrders.ClearsMark(_commanderMarkId, _selectedId);
            Net.I.SendCommanderMark(_selectedId, clear);
            _commanderMarkSentAt = clear ? 0 : Now();
            StartSkillCooldown(skill);
            return;
        }
        Net.I.SendCommanderOrder(entry.Order);
        foreach (var other in CommanderOrders.Bar)
            if (other.Order != CommanderOrders.None && SkillData.Get(other.Skill) is { } orderSkill)
                StartSkillCooldown(orderSkill);
    }

    private void CommanderTick(double now)
    {
        if (_commanderBar is not { Visible: true }) return;
        for (int i = 0; i < CommanderOrders.Bar.Length; i++)
        {
            var skill = SkillData.Get(CommanderOrders.Bar[i].Skill);
            double left = skill != null && _skillReady.TryGetValue(skill.Id, out double until) ? until - now : 0;
            _commanderButtons[i].Disabled = left > 0;
            _commanderCooldowns[i].Text = left > 0 ? Mathf.CeilToInt((float)left).ToString() : "";
        }
        if (_commanderMarkId >= 0 && _commanderMarkSentAt > 0 && now - _commanderMarkSentAt >= CommanderOrders.MarkSeconds)
        {
            Net.I.SendCommanderMark(_commanderMarkId, true);
            _commanderMarkSentAt = 0;
        }
    }

    private Color PartyNameColourOf(int charId, bool leader) =>
        leader ? PartyLeaderNameColour : charId == Net.I.PartyCommanderId ? CommanderNameColour : PartyNameColour;
}
