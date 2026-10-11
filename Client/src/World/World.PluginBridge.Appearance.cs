using System;
using System.Collections.Generic;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;
using LibreKO.Plugins;

namespace LibreKO;

public partial class World
{
    private const int GenderChangeConfirmText = 45100;

    private void PluginAppearanceInit() =>
        PluginHost.Game.AttachAppearance(new PluginAppearanceBridge(this, gender: false), new PluginAppearanceBridge(this, gender: true));

    private IReadOnlyList<AppearanceRaceChoice> GenderRaceChoices()
    {
        var choices = new List<AppearanceRaceChoice>();
        string className = World.ClassName(_selfClass);
        foreach (int race in _genderEditor.RaceOptions)
            choices.Add(new AppearanceRaceChoice(StarterStats.RaceName(race), className, race == Net.I.LastEnter.Race));
        return choices;
    }

    private void AskGenderChange() =>
        Notice.Confirm(this, ItemData.Text(GenderChangeConfirmText, "Once accepted, the connection will be terminated. Do you want to accept?"),
            "OK", "Cancel", SubmitGenderChange);

    private sealed class PluginAppearanceBridge(World w, bool gender) : IGameAppearance
    {
        event Action? IGameAppearance.Changed { add { } remove { } }

        private LookEditor Editor => gender ? w._genderEditor : w._beautyEditor;
        private LookPreview Look => gender ? w._genderPreview : w._beautyPreview;

        public bool IsOpen => gender ? w._genderShown : w._changeHairShown;
        public string CharacterName => Net.I.LastEnter.Name;
        public Texture2D? Preview => IsOpen ? Look.Texture : null;
        public string RaceName => StarterStats.RaceName(Editor.Race);
        public int Face => Editor.Face;
        public int HairStyle => Editor.HairStyle;
        public Color HairColour => Editor.HairColour;
        public bool CanChangeRace => gender;
        public IReadOnlyList<AppearanceRaceChoice> RaceChoices => gender ? w.GenderRaceChoices() : Array.Empty<AppearanceRaceChoice>();
        public bool Busy => gender ? w._genderInFlight : w._beautyInFlight;

        public void Step(AppearancePart part, int delta)
        {
            switch (part)
            {
                case AppearancePart.Race when gender: Editor.StepRace(delta); break;
                case AppearancePart.Face: Editor.StepFace(delta); break;
                case AppearancePart.Hair: Editor.StepHair(delta); break;
            }
        }

        public void Turn(float degrees) => Look.Turn(degrees);

        public void ToggleZoom() => Look.ToggleZoom();

        public void SetHairColour(Color colour) => Editor.SetHairColour(colour);

        public void OpenHairColour() => w.OpenHairPalette(Editor);

        public void Confirm()
        {
            if (gender) w.AskGenderChange();
            else w.SubmitChangeHair();
        }

        public void Cancel()
        {
            if (gender) w.CloseGenderChange();
            else w.CloseChangeHair();
        }
    }
}
