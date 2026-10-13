using Godot;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private PanelContainer? _groundChip;
    private Node3D? _groundAura;

    private void GroundEffectInit()
    {
        Net.I.GroundEffectEvent += OnGroundEffect;
        OnGroundEffect(Net.I.GroundEffect);
    }

    private void GroundEffectDispose() => Net.I.GroundEffectEvent -= OnGroundEffect;

    private void OnGroundEffect(GroundEffect effect)
    {
        ShowGroundChip(effect);
        SetGroundAura(GroundEffects.HasAura(effect));
    }

    private void ShowGroundChip(GroundEffect effect)
    {
        if (_groundChip != null && IsInstanceValid(_groundChip)) _groundChip.QueueFree();
        _groundChip = null;
        if (GroundEffects.IconPath(effect) is { } path && IsInstanceValid(_buffNormalRow))
        {
            var (textId, fallback) = GroundEffects.Text(effect);
            var chip = new PanelContainer
            {
                CustomMinimumSize = new Vector2(BuffChipSize, BuffChipSize),
                MouseFilter = Control.MouseFilterEnum.Pass,
                TooltipText = TextWrap.Wrap(SystemText(textId, fallback), BuffTooltipWidth),
            };
            chip.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
            chip.AddChild(new TextureRect
            {
                Texture = ResourceLoader.Load<Texture2D>(path),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });
            _buffNormalRow.AddChild(chip);
            _buffNormalRow.MoveChild(chip, 0);
            _groundChip = chip;
        }
        ShowBuffRows();
    }

    private void SetGroundAura(bool on)
    {
        bool running = _groundAura != null && IsInstanceValid(_groundAura);
        if (!on)
        {
            if (running) _groundAura!.QueueFree();
            _groundAura = null;
            return;
        }
        if (running || !IsInstanceValid(_self) || Fx.NameForId(GroundEffects.SwampAuraFx) is not { } name) return;
        _groundAura = Fx.Spawn(name, _self, Vector3.Zero);
    }
}
