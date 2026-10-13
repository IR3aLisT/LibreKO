using System.Collections.Generic;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const int WeaponRightSlot = 6;
    private const int WeaponLeftSlot = 7;

    private readonly Dictionary<string, PackedScene?> _gearScenes = new();
    private readonly Dictionary<string, List<string>> _gearPathsByLook = new();
    private readonly HashSet<int> _deferredRedress = new();

    private bool SceneReadyFor(EntitySnapshot info) =>
        BodySceneReady(info) && (info.IsNpc || GearLoaded(PlayerRig.LookRace(info.Race, info.Class), info.Gear));

    private bool GearLoaded(int lookRace, int[]? gear)
    {
        foreach (var path in GearPaths(lookRace, gear))
            if (!StreamGear(path)) return false;
        return true;
    }

    private bool StreamGear(string path)
    {
        if (_gearScenes.ContainsKey(path) || _partCache.ContainsKey(path)) return true;
        if (ResourceLoader.HasCached(path) && _sceneLoadInFlight != path)
        {
            _gearScenes[path] = ResourceLoader.Load(path) as PackedScene;
            return true;
        }
        if (_sceneLoadInFlight == null)
        {
            if (ResourceLoader.LoadThreadedRequest(path) != Error.Ok)
            {
                _gearScenes[path] = null;
                return true;
            }
            _sceneLoadInFlight = path;
        }
        if (_sceneLoadInFlight != path) return false;
        var status = ResourceLoader.LoadThreadedGetStatus(path);
        if (status == ResourceLoader.ThreadLoadStatus.InProgress) return false;
        _gearScenes[path] = status == ResourceLoader.ThreadLoadStatus.Loaded
            ? ResourceLoader.LoadThreadedGet(path) as PackedScene
            : null;
        _sceneLoadInFlight = null;
        return true;
    }

    private List<string> GearPaths(int lookRace, int[]? gear)
    {
        string key = $"{lookRace}:{(gear == null ? "" : string.Join(',', gear))}";
        if (_gearPathsByLook.TryGetValue(key, out var cached)) return cached;
        var paths = new List<string>();
        if (gear != null)
        {
            if (!_partsLoaded) { _partsIndex = LoadPartsIndex(); _partsLoaded = true; }
            foreach (var (gi, _) in ArmorSlotToPart)
            {
                if (gi >= gear.Length || gear[gi] <= 0) continue;
                int resource = ArmorResource(gear[gi]);
                if (resource == 0) continue;
                int prefix = CostumeLook.PartPrefix(gear[gi], ItemData.Get(gear[gi])?.SaleType ?? 0);
                foreach (string stem in CostumeLook.PartStems(prefix, resource, lookRace))
                {
                    string path = $"res://assets/items/armor/{stem}.glb";
                    if (!ResourceLoader.Exists(path)) continue;
                    paths.Add(path);
                    break;
                }
            }

            var weapons = WeaponCatalog.Index;
            foreach (int slot in new[] { WeaponRightSlot, WeaponLeftSlot })
            {
                if (slot >= gear.Length || gear[slot] <= 0) continue;
                if (!weapons.TryGetValue(WeaponCatalog.ResolveBaseId(gear[slot]), out var weapon)) continue;
                string path = $"res://assets/items/weapon/{weapon.Stem}.glb";
                if (ResourceLoader.Exists(path)) paths.Add(path);
            }

            _wingIndex ??= LoadWingIndex();
            foreach (int slot in WingVisualSlots)
            {
                if (slot >= gear.Length || gear[slot] <= 0 || !TryWingPart(gear[slot], out var wing)) continue;
                string path = $"res://assets/wings/{wing.Stem}.glb";
                if (ResourceLoader.Exists(path)) paths.Add(path);
            }
        }
        _gearPathsByLook[key] = paths;
        return paths;
    }

    private void RedressWhenGearLoaded(Ent e)
    {
        if (GearLoaded(e.LookRace, e.Gear)) RedressEntity(e);
        else _deferredRedress.Add(e.Id);
    }

    private void GearStreamTick()
    {
        if (_deferredRedress.Count == 0) return;
        foreach (int id in new List<int>(_deferredRedress))
        {
            if (!_ents.TryGetValue(id, out var e))
            {
                _deferredRedress.Remove(id);
                continue;
            }
            if (!GearLoaded(e.LookRace, e.Gear)) return;
            _deferredRedress.Remove(id);
            RedressEntity(e);
        }
    }
}
