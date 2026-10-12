using System.Collections.Generic;
using Godot;
using LibreKO.Domain;
using FileAccess = Godot.FileAccess;

namespace LibreKO;

public partial class World
{
    private const float NpcFinderBlipRadius = 4.5f;
    private static List<NpcSpot>? _npcSpots;
    private int _npcFinderKind = NpcFinder.NoKind;

    private static IReadOnlyList<NpcSpot> NpcSpots => _npcSpots ??= LoadNpcSpots();

    private static List<NpcSpot> LoadNpcSpots()
    {
        if (!FileAccess.FileExists(NpcFinder.ResourcePath)) return new List<NpcSpot>();
        using var file = FileAccess.Open(NpcFinder.ResourcePath, FileAccess.ModeFlags.Read);
        return file == null ? new List<NpcSpot>() : NpcFinder.Parse(file.GetAsText());
    }

    private void BuildNpcFinder()
    {
        if (_miniMap != null) _miniMap.NpcKindPicked += PickNpcKind;
        RefreshNpcKinds();
    }

    private void PickNpcKind(int kind)
    {
        _npcFinderKind = kind == _npcFinderKind ? NpcFinder.NoKind : kind;
        RefreshNpcKinds();
    }

    private bool NpcKindPresent(int kind) =>
        kind >= 1 && kind <= NpcFinder.KindCount
        && NpcFinder.KindsPresent(NpcSpots, _zone, Net.I.LastEnter.Nation)[kind - 1];

    private void RefreshNpcKinds()
    {
        _miniMap?.SetNpcKinds(NpcFinder.KindsPresent(NpcSpots, _zone, Net.I.LastEnter.Nation), _npcFinderKind);
        UpdateMiniMap();
    }

    private void AddNpcFinderBlips()
    {
        if (_npcFinderKind == NpcFinder.NoKind) return;
        foreach (var spot in NpcFinder.Spots(NpcSpots, _zone, Net.I.LastEnter.Nation, _npcFinderKind))
            _blipScratch.Add(new MiniMap.Blip(spot.X, spot.Z, UiTheme.GoldVivid, NpcFinderBlipRadius, false, Diamond: true));
    }
}
