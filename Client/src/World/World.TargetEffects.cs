using System.Collections.Generic;
using Godot;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const int TargetEffectsPerRow = 10;
    private const float TargetEffectSize = 22f;
    private const double TargetEffectsPollInterval = 1.0;
    private const int TargetEffectTooltipWidth = 44;
    private const int TargetEffectTimerFont = 9;
    private const double SecondsPerMinute = 60.0;

    private static readonly Color TargetEffectHarmful = new(0.85f, 0.3f, 0.3f);
    private static readonly Color TargetEffectHelpful = new(0.79f, 0.64f, 0.15f);

    private GridContainer _targetEffectsRow = null!;
    private readonly List<(TargetEffect Effect, Label Timer)> _targetEffectCells = new();
    private int _targetEffectsFor = -1;
    private double _targetEffectsAt;
    private double _targetEffectsPollAt;

    private void TargetEffectsInit() => Net.I.TargetEffectsEvent += OnTargetEffects;

    private void TargetEffectsDispose() => Net.I.TargetEffectsEvent -= OnTargetEffects;

    private void BuildTargetEffectsRow(VBoxContainer frame)
    {
        _targetEffectsRow = new GridContainer
        {
            Columns = TargetEffectsPerRow,
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _targetEffectsRow.AddThemeConstantOverride("h_separation", 2);
        _targetEffectsRow.AddThemeConstantOverride("v_separation", 2);
        frame.AddChild(_targetEffectsRow);
    }

    private void TargetEffectsPollTick(double now)
    {
        if (_selectedId < 0 || now < _targetEffectsPollAt) return;
        _targetEffectsPollAt = now + TargetEffectsPollInterval;
        if (_ents.TryGetValue(_selectedId, out var e) && !e.Dead && _selectedId != _myId)
            Net.I.SendTargetEffectsRequest(_selectedId);
    }

    private void OnTargetEffects(TargetEffectsReply reply)
    {
        if (reply.TargetId != _selectedId) return;
        _targetEffectsAt = Now();
        if (reply.TargetId == _targetEffectsFor && SameEffects(reply.Effects))
        {
            for (int i = 0; i < _targetEffectCells.Count; i++)
                _targetEffectCells[i] = (reply.Effects[i], _targetEffectCells[i].Timer);
            return;
        }
        _targetEffectsFor = reply.TargetId;
        RebuildTargetEffects(reply.Effects);
    }

    private bool SameEffects(IReadOnlyList<TargetEffect> effects)
    {
        if (effects.Count != _targetEffectCells.Count) return false;
        for (int i = 0; i < effects.Count; i++)
            if (effects[i].SkillId != _targetEffectCells[i].Effect.SkillId) return false;
        return true;
    }

    private void RebuildTargetEffects(IReadOnlyList<TargetEffect> effects)
    {
        foreach (var child in _targetEffectsRow.GetChildren()) child.QueueFree();
        _targetEffectCells.Clear();
        foreach (var effect in effects)
        {
            if (SkillData.Get(effect.SkillId) is not { } s) continue;
            _targetEffectCells.Add((effect, AddTargetEffectCell(s)));
        }
        _targetEffectsRow.Visible = _targetEffectCells.Count > 0;
    }

    private Label AddTargetEffectCell(SkillData.Skill s)
    {
        bool harmful = s.IsEnemy || s.MoveSpeedPercent < 100;
        var cell = new PanelContainer
        {
            CustomMinimumSize = new Vector2(TargetEffectSize, TargetEffectSize),
            MouseFilter = Control.MouseFilterEnum.Pass,
            TooltipText = $"{s.Name}\n{TextWrap.Wrap(s.Desc, TargetEffectTooltipWidth)}",
        };
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.05f, 0.07f, 0.85f),
            BorderColor = harmful ? TargetEffectHarmful : TargetEffectHelpful,
        };
        style.SetCornerRadiusAll(3);
        style.SetBorderWidthAll(1);
        cell.AddThemeStyleboxOverride("panel", style);

        if (SkillData.Icon(s.Id) is { } tex)
            cell.AddChild(new TextureRect
            {
                Texture = tex,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });
        else
            cell.AddChild(new Label
            {
                Text = s.Name.Length > 0 ? s.Name[..1] : "?",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });

        var timer = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        timer.AddThemeFontSizeOverride("font_size", TargetEffectTimerFont);
        timer.AddThemeColorOverride("font_outline_color", Colors.Black);
        timer.AddThemeConstantOverride("outline_size", 3);
        cell.AddChild(timer);
        _targetEffectsRow.AddChild(cell);
        return timer;
    }

    private void TickTargetEffects()
    {
        if (_targetEffectsFor >= 0 && _targetEffectsFor != _selectedId)
        {
            _targetEffectsFor = -1;
            RebuildTargetEffects(System.Array.Empty<TargetEffect>());
            _targetEffectsPollAt = 0;
        }
        if (_targetEffectCells.Count == 0) return;
        double elapsed = Now() - _targetEffectsAt;
        foreach (var (effect, timer) in _targetEffectCells)
        {
            double left = Mathf.Max(0, effect.Seconds - (float)elapsed);
            timer.Text = left >= SecondsPerMinute ? $"{left / SecondsPerMinute:0}M" : $"{left:0}S";
        }
    }
}
