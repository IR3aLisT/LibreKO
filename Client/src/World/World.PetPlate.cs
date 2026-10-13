using Godot;
using LibreKO.Domain;

namespace LibreKO;

public partial class World
{
    private const string PetPlateLayoutId = "pet_plate";
    private static readonly Vector2 PetPlateSize = new(152, 90);
    private const float PetPlateGap = 8f;
    private static readonly Color PetPlateLevelColour = new("ffff00");
    private static readonly Color PetPlateExpColour = new("e0bb52");

    private CanvasLayer _petPlateLayer = null!;
    private PanelContainer? _petPlate;
    private Label _petPlateName = null!, _petPlateLevel = null!;
    private StatBar _petPlateHp = null!, _petPlateMp = null!;
    private Control _petPlateSatisfaction = null!, _petPlateExp = null!;
    private static readonly Color PetPlateTrack = new(0.02f, 0.02f, 0.02f, 0.9f);

    private void PetPlateInit()
    {
        _petPlateLayer = new CanvasLayer { Layer = 59 };
        AddChild(_petPlateLayer);
        _petPlate = BuildPetPlate();
        _petPlateLayer.AddChild(_petPlate);
        var layout = HudLayout.Attach(_petPlate, PetPlateLayoutId, _petPlate,
            () => new Vector2(HudAnchor.Edge + PartyHudWidth + PetPlateGap, PartyHudTop));
        layout.Clicked += OpenPet;
        RefreshPetPlate(Net.I.Pet);
    }

    private PanelContainer BuildPetPlate()
    {
        var plate = new PanelContainer
        {
            CustomMinimumSize = PetPlateSize,
            Size = PetPlateSize,
            Visible = false,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
        };
        plate.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        var canvas = new Control { CustomMinimumSize = PetPlateSize, MouseFilter = Control.MouseFilterEnum.Ignore };
        plate.AddChild(canvas);

        _petPlateName = PetPlateLabel(canvas, new Rect2(17, 4, 86, 13), UiTheme.TextHi, HorizontalAlignment.Left);
        PetPlateLabel(canvas, new Rect2(104, 4, 17, 13), PetPlateLevelColour, HorizontalAlignment.Center).Text = "LV";
        _petPlateLevel = PetPlateLabel(canvas, new Rect2(120, 4, 17, 13), UiTheme.TextHi, HorizontalAlignment.Center);
        _petPlateHp = PetPlateBar(canvas, new Rect2(14, 27, 118, 14), UiTheme.Hp);
        _petPlateMp = PetPlateBar(canvas, new Rect2(14, 43, 118, 14), UiTheme.Mp);
        _petPlateSatisfaction = PetPlateThinBar(canvas, new Rect2(14, 60, 118, 8), PetPlateShadedFill(UiTheme.Good));
        _petPlateExp = PetPlateThinBar(canvas, new Rect2(26, 70, 97, 3), new ColorRect { Color = PetPlateExpColour });
        return plate;
    }

    private static Label PetPlateLabel(Control canvas, Rect2 rect, Color colour, HorizontalAlignment align)
    {
        var label = UiTheme.Text("", 11, colour, align);
        label.VerticalAlignment = VerticalAlignment.Center;
        label.ClipText = true;
        label.Position = rect.Position;
        label.Size = rect.Size;
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        canvas.AddChild(label);
        return label;
    }

    private static StatBar PetPlateBar(Control canvas, Rect2 rect, Color colour)
    {
        var bar = new StatBar(colour, rect.Size) { Position = rect.Position, Size = rect.Size, MouseFilter = Control.MouseFilterEnum.Ignore };
        canvas.AddChild(bar);
        return bar;
    }

    private static Control PetPlateThinBar(Control canvas, Rect2 rect, Control fill)
    {
        var track = new ColorRect { Color = PetPlateTrack, Position = rect.Position, Size = rect.Size, MouseFilter = Control.MouseFilterEnum.Ignore };
        canvas.AddChild(track);
        fill.Size = new Vector2(0, rect.Size.Y);
        fill.MouseFilter = Control.MouseFilterEnum.Ignore;
        track.AddChild(fill);
        return fill;
    }

    private static TextureRect PetPlateShadedFill(Color colour) => new()
    {
        Texture = new GradientTexture2D
        {
            Gradient = new Gradient { Colors = [colour.Lightened(StatBar.FillLighten), colour.Darkened(StatBar.FillDarken)], Offsets = [0f, 1f] },
            FillFrom = Vector2.Zero,
            FillTo = Vector2.Down,
            Width = 1,
            Height = PetPlateGradientHeight,
        },
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.Scale,
    };

    private const int PetPlateGradientHeight = 16;

    private static void SetThinBar(Control fill, float fraction)
    {
        if (fill.GetParent() is not Control track) return;
        fill.Size = new Vector2(track.Size.X * Mathf.Clamp(fraction, 0f, 1f), track.Size.Y);
    }

    private void RefreshPetPlate(PetSheet? pet)
    {
        if (_petPlate == null || !IsInstanceValid(_petPlate)) return;
        bool shown = pet != null && !_selfDead && !_petShown;
        _petPlate.Visible = shown;
        if (!shown || pet == null) return;
        _petPlateName.Text = pet.Name;
        _petPlateLevel.Text = pet.Level.ToString();
        _petPlateHp.Set(pet.Hp, pet.MaxHp);
        _petPlateMp.Set(pet.Mp, pet.MaxMp);
        SetThinBar(_petPlateSatisfaction, pet.SatisfactionFraction);
        SetThinBar(_petPlateExp, pet.ExpFraction);
        _petPlate.TooltipText = $"{pet.Satisfaction / 100f:0.00}% satisfaction\n{pet.ExpPercent / 100f:0.00}% EXP";
    }
}
