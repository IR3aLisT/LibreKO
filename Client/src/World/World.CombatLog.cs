using System;
using System.Collections.Generic;
using Godot;
using LibreKO.Domain;

namespace LibreKO;

public partial class World
{
    private enum CombatLogKind { Damage, Outgoing, Incoming, Recovery, Resource, Status, Spent, Experience, National }

    private VBoxContainer _combatLogFrame = null!;
    private PanelContainer _combatLogRoot = null!;
    private Button _combatLogMove = null!;
    private Button _combatLogLock = null!;
    private HudLayout? _combatLogLayout;
    private const float CombatLogToolRow = 22f;
    private const float CombatLogToolGap = 4f;
    private static readonly Vector2 CombatLogPanelSize = new(420, 205);
    private static readonly Vector2 CombatLogPanelMinimum = new(290, 135);
    private static readonly Vector2 CombatLogEdge = new(12, 77);
    private static readonly Vector2 CombatLogFrameExtra = new(0, CombatLogToolRow + CombatLogToolGap);
    private static readonly Color CombatLogEdgeColour = new(UiTheme.Edge, 0.46f);
    private HudLogText _combatLogText = null!;
    private StyleBoxFlat _combatLogPanelStyle = null!;
    private readonly Queue<string> _combatLogLines = new();
    private const int CombatLogMaxLines = 120;
    private const int TextBeginAttack = 3002;
    private const int TextStopAttack = 3003;
    private const int TextMissed = 3015;
    private const int TextNotEnoughMp = 3004;
    private const int TextOverweight = 2601;

    private void BuildCombatLog()
    {
        var layer = new CanvasLayer { Layer = 66 };
        AddChild(layer);
        PluginHudSeam(layer, LibreKO.Plugins.HudPart.CombatLog);

        _combatLogFrame = new VBoxContainer
        {
            Size = CombatLogPanelSize + CombatLogFrameExtra,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _combatLogFrame.AddThemeConstantOverride("separation", (int)CombatLogToolGap);
        layer.AddChild(_combatLogFrame);

        var tools = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.End,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(0, CombatLogToolRow),
        };
        tools.AddThemeConstantOverride("separation", 3);
        var background = HudToolButton.Create("◐", null, "Background opacity");
        background.Pressed += CycleCombatLogBackground;
        tools.AddChild(background);
        _combatLogMove = HudToolButton.Create("", "system/move", "Drag to move the combat log");
        tools.AddChild(_combatLogMove);
        _combatLogLock = HudToolButton.Create("", "system/unlock", "Lock position and size");
        _combatLogLock.Pressed += () => SetCombatLogLocked(!Config.CombatLogLocked);
        tools.AddChild(_combatLogLock);
        _combatLogFrame.AddChild(tools);

        _combatLogRoot = new PanelContainer
        {
            CustomMinimumSize = CombatLogPanelMinimum,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        _combatLogPanelStyle = new StyleBoxFlat { BorderColor = CombatLogEdgeColour };
        _combatLogPanelStyle.SetBorderWidthAll(1);
        _combatLogPanelStyle.SetCornerRadiusAll(4);
        foreach (var side in new[] { "left", "right", "top", "bottom" })
            _combatLogPanelStyle.Set($"content_margin_{side}", 7f);
        _combatLogRoot.AddThemeStyleboxOverride("panel", _combatLogPanelStyle);
        _combatLogFrame.AddChild(_combatLogRoot);
        ApplyCombatLogBackground();

        _combatLogText = new HudLogText
        {
            ScrollbarOnLeft = false,
            BbcodeEnabled = true,
            ScrollActive = true,
            ScrollFollowing = true,
            FitContent = false,
            SelectionEnabled = true,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        _combatLogText.AddThemeFontSizeOverride("normal_font_size", 12);
        _combatLogText.AddThemeColorOverride("default_color", new Color("#d4d1c9"));
        _combatLogRoot.AddChild(_combatLogText);
        _combatLogText.MetaHoverStarted += OnLogMetaHover;
        _combatLogText.MetaHoverEnded += _ => HideItemTooltip();

        AttachCombatLogLayout(_combatLogFrame, () => CombatLogSpot(GetViewport().GetVisibleRect().Size));

        _combatLogFrame.Visible = Config.CombatLog;
        Config.EffectsChanged += ApplyCombatLogVisibility;
        Net.I.DeathNoticeEvent += OnDeathNotice;
    }

    private void CombatLogDispose()
    {
        Config.EffectsChanged -= ApplyCombatLogVisibility;
        Net.I.DeathNoticeEvent -= OnDeathNotice;
    }

    private void OnDeathNotice(Net.DeathNotice n)
    {
        bool listed = Net.I.ForgetWanted(n.VictimName);
        int text = DeathNotices.TextFor(n.Kind, _zone, n.VictimIsKing, n.KillerNation, listed);
        string template = ItemData.Text(text, "- %s defeat %s ( %d, %d ) -");
        string killer = NationColouredName(n.KillerName, n.KillerNation);
        string victim = NationColouredName(n.VictimName, n.VictimNation);
        string plain, markup;
        if (text is Vanguard.KarusSlainText or Vanguard.ElMoradSlainText)
        {
            plain = TextTemplate.Fill(Vanguard.Plain(template), n.KillerName, n.VictimName, n.X, n.Z);
            markup = TextTemplate.Fill(Vanguard.Markup(BbCode.Esc(template)), BbCode.Esc(n.KillerName), BbCode.Esc(n.VictimName), n.X, n.Z);
        }
        else if (n.Kind == DeathNotices.DefeatedKind)
        {
            plain = TextTemplate.Fill(template, n.VictimName, n.KillerName);
            markup = TextTemplate.Fill(BbCode.Esc(template), victim, killer);
        }
        else
        {
            plain = TextTemplate.Fill(template, n.KillerName, n.VictimName, n.X, n.Z);
            markup = TextTemplate.Fill(BbCode.Esc(template), killer, victim, n.X, n.Z);
        }
        var colour = new Color("#" + DeathNotices.LineColourFor(_zone, n.KillerId == _myId, n.Kind));
        Floaters?.Notice(plain);
        CombatLogAdd(plain, CombatLogKind.Status, colour, markup);
    }

    private static string NationColouredName(string name, int nation) =>
        DeathNotices.NameColour(nation) is { } colour ? $"[color=#{colour}]{BbCode.Esc(name)}[/color]" : BbCode.Esc(name);

    private void ApplyCombatLogVisibility()
    {
        if (_combatLogFrame != null && GodotObject.IsInstanceValid(_combatLogFrame))
            _combatLogFrame.Visible = Config.CombatLog;
    }

    internal static Vector2 CombatLogSpot(Vector2 viewport)
    {
        Vector2 size = CombatLogPanelSize + CombatLogFrameExtra;
        return new Vector2(
            Mathf.Max(0f, viewport.X - CombatLogEdge.X - size.X),
            Mathf.Max(0f, viewport.Y - CombatLogEdge.Y - size.Y));
    }

    private void CycleCombatLogBackground()
    {
        Config.SetCombatLogBackground((Config.CombatLogBackground + 1) % ChatPrefs.Backgrounds.Length);
        ApplyCombatLogBackground();
    }

    private void ApplyCombatLogBackground()
    {
        float alpha = ChatPrefs.Backgrounds[Config.CombatLogBackground];
        _combatLogPanelStyle.BgColor = new Color(0, 0, 0, alpha);
        _combatLogPanelStyle.BorderColor = alpha > 0 ? CombatLogEdgeColour : Colors.Transparent;
    }

    private void SetCombatLogLocked(bool locked)
    {
        Config.SetCombatLogLocked(locked);
        ApplyCombatLogLock();
    }

    private void ApplyCombatLogLock()
    {
        bool locked = Config.CombatLogLocked;
        if (_combatLogLayout != null) _combatLogLayout.Locked = locked;
        _combatLogMove.Visible = !locked;
        HudToolButton.ShowLocked(_combatLogLock, locked);
    }

    private void AttachCombatLogLayout(Control target, Func<Vector2> defaultPosition, bool persist = true)
    {
        target.Modulate = Colors.White;
        _combatLogLayout = HudLayout.Attach(
            target, persist ? "hud_combat_log" : "uilab_actual_combat_log", _combatLogMove, defaultPosition,
            resizable: true,
            defaultSize: CombatLogPanelSize + CombatLogFrameExtra,
            minimumSize: CombatLogPanelMinimum + CombatLogFrameExtra,
            persist: persist,
            resizeCorner: HudLayout.Corner.TopLeft,
            resizeGripOffset: CombatLogFrameExtra,
            moveGripOverlay: false);
        ApplyCombatLogLock();
    }

    private void CombatLogAdd(string message, CombatLogKind kind, Color? tint = null, string? markup = null)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        string color = tint?.ToHtml(false) ?? kind switch
        {
            CombatLogKind.Damage or CombatLogKind.Outgoing => "ffffff",
            CombatLogKind.Incoming or CombatLogKind.Spent => "ff3b3b",
            CombatLogKind.Recovery or CombatLogKind.Resource => "6565ff",
            CombatLogKind.National => "9b9bff",
            _ => "ffff00",
        };
        string line = $"[color=#{color}]{markup ?? BbCode.Esc(message)}[/color]";
        _combatLogLines.Enqueue(line);
        bool rebuild = false;
        while (_combatLogLines.Count > CombatLogMaxLines)
        {
            string dropped = _combatLogLines.Dequeue();
            if (_combatLogText == null || rebuild) continue;
            for (int n = dropped.Split('\n').Length; n > 0 && !rebuild; n--)
                rebuild = !_combatLogText.RemoveParagraph(0, true);
        }
        if (_combatLogText != null)
        {
            if (rebuild) _combatLogText.Text = string.Join("\n", _combatLogLines);
            else
            {
                if (_combatLogLines.Count > 1) _combatLogText.AppendText("\n");
                _combatLogText.AppendText(line);
            }
        }
        PluginLogAdd(kind is CombatLogKind.Resource or CombatLogKind.Spent or CombatLogKind.Experience or CombatLogKind.National
                ? LibreKO.Plugins.GameLogKind.Item : LibreKO.Plugins.GameLogKind.Status,
            message, new Color("#" + color));
    }

    private void CombatNotice(string message) => CombatNotice(message, null);

    private void CombatNotice(string message, Color? tint)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        Floaters?.Notice(message);
        CombatLogAdd(message, CombatLogKind.Status, tint);
    }

    private static string SystemText(int id, string fallback, string? arg = null)
    {
        string text = ItemData.Text(id, fallback).Trim();
        return arg == null ? text : text.Replace("%s", arg);
    }

    private string CombatEntityName(int id)
    {
        if (id == _myId) return "You";
        return _ents.TryGetValue(id, out var e) && !string.IsNullOrWhiteSpace(e.Name)
            ? ShownName(e)
            : "Unknown";
    }
}
