using LibreKO.Domain;

namespace LibreKO;

public partial class World
{
    private bool _platesHidden;

    private void DisplayPrefsInit()
    {
        Config.PlatesChanged += RefreshPlateTitles;
        Config.CospreChanged += RedressOthersForCospre;
    }

    private void DisplayPrefsDispose()
    {
        Config.PlatesChanged -= RefreshPlateTitles;
        Config.CospreChanged -= RedressOthersForCospre;
    }

    private void ToggleNamePlates()
    {
        _platesHidden = !_platesHidden;
        SelfPlate()?.SetVisible(!_platesHidden);
    }

    private void RefreshPlateTitles()
    {
        SelfPlate()?.RefreshTitle();
        foreach (var e in _ents.Values) e.Plate?.RefreshTitle();
    }

    private void RedressOthersForCospre()
    {
        foreach (var e in _ents.Values)
            if (!e.IsNpc) RedressEntity(e, force: true);
    }

    private static int[]? ShownCospre(int[]? gear) => CospreFilter.Hide(gear, Config.HiddenCospre);
}
