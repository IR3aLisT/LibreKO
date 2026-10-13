using System.Collections.Generic;
using Godot;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private readonly HashSet<int> _stealthIds = new();
    private readonly HashSet<int> _stealthDetected = new();
    private readonly HashSet<int> _infiltratingIds = new();
    private float _sightRadius;

    private const float StealthSeenAlpha = 0.5f;
    private const float StealthHiddenAlpha = 0f;
    private const string StealthFx = "stealth_cast";
    private const string StealthShadowMeta = "stealth_shadow";
    private const string StealthVisibleMeta = "stealth_visible";

    private void StealthInit()
    {
        Net.I.StealthHookEvents();
        Net.I.StealthEvent += OnStealth;
        Net.I.SightEvent += OnSight;
    }

    private void StealthDispose()
    {
        Net.I.SightEvent -= OnSight;
        Net.I.StealthEvent -= OnStealth;
        Net.I.StealthUnhookEvents();
    }

    private void OnSight(float radius)
    {
        if (radius <= 0f && _sightRadius <= 0f) return;

        _sightRadius = radius;
        if (radius <= 0f) _stealthDetected.Clear();
        CombatNotice(radius > 0f ? "You can see the hidden." : "The hidden fade from view.");
        foreach (int id in _stealthIds) ApplyStealthFade(id, stealthed: true);
    }

    private void StealthTick(Vector3 selfPos)
    {
        if (_stealthIds.Count == 0) return;

        float radiusSq = _sightRadius * _sightRadius;
        foreach (int id in _stealthIds)
        {
            if (id == _myId || _infiltratingIds.Contains(id)) continue;

            var body = StealthBodyOf(id);
            bool seen = _sightRadius > 0f && body != null
                && body.Position.DistanceSquaredTo(selfPos) <= radiusSq;
            if (seen == _stealthDetected.Contains(id)) continue;

            if (seen) _stealthDetected.Add(id);
            else _stealthDetected.Remove(id);
            ApplyStealthFade(id, stealthed: true);
        }
    }

    private void StealthCancelSelf()
    {
        if (!_stealthIds.Contains(_myId)) return;
        Net.I.SendStealth();
        CombatNotice("Cancelling stealth.");
    }

    private void StealthOnSpawn(int charId, int invisibility)
    {
        SetInfiltrating(charId, invisibility == Net.InvisibilityInfiltration);
        if (!_stealthIds.Add(charId)) return;
        ApplyStealthFade(charId, stealthed: true);
    }

    private void SetInfiltrating(int charId, bool on)
    {
        if (on) _infiltratingIds.Add(charId); else _infiltratingIds.Remove(charId);
        if (charId != _myId && _ents.TryGetValue(charId, out var ent)) ent.Infiltrating = on;
    }

    private void StealthForgetEntity(int charId)
    {
        _stealthIds.Remove(charId);
        _stealthDetected.Remove(charId);
        _infiltratingIds.Remove(charId);
    }

    private void OnStealth(int charId, int value)
    {
        bool on = value != 0;
        if (on)
        {
            SetInfiltrating(charId, value == Net.InvisibilityInfiltration);
            if (!_stealthIds.Add(charId))
            {
                ApplyStealthFade(charId, stealthed: true);
                return;
            }
            ApplyStealthFade(charId, stealthed: true);
            StealthSpawnFx(charId);

            if (charId == _myId)
                CombatNotice("You melt into the shadows.");
            else if (_ents.TryGetValue(charId, out var e) && !string.IsNullOrEmpty(e.Name))
                CombatNotice($"{e.Name} vanishes.");
        }
        else
        {
            SetInfiltrating(charId, false);
            if (!_stealthIds.Remove(charId)) return;
            _stealthDetected.Remove(charId);
            ApplyStealthFade(charId, stealthed: false);

            if (charId == _myId)
                CombatNotice("You step back into the light.");
        }
    }

    private void ApplyStealthFade(int charId, bool stealthed)
    {
        var body = StealthBodyOf(charId);
        if (body == null) return;

        float alpha;
        if (!stealthed) alpha = 1f;
        else if (charId == _myId) alpha = StealthSeenAlpha;
        else if (_infiltratingIds.Contains(charId)) alpha = StealthHiddenAlpha;
        else if (_ents.TryGetValue(charId, out var who) && !who.Attackable) alpha = StealthSeenAlpha;
        else alpha = _stealthDetected.Contains(charId) ? StealthSeenAlpha : StealthHiddenAlpha;

        StealthSetBodyAlpha(body, alpha);
        RefreshRankAuraFor(charId, stealthed);

        if (charId == _myId || !_ents.TryGetValue(charId, out var ent)) return;
        ent.StealthUndetected = stealthed && !_stealthDetected.Contains(charId);
        if (_selectedId == charId && HiddenFromMe(ent))
        {
            if (_autoAttack && _autoTargetId == charId) StopAutoAttack();
            Deselect();
        }
    }

    private static bool HiddenFromMe(Ent e) => e.Infiltrating || (e.StealthUndetected && e.Attackable);

    private void StealthSpawnFx(int charId)
    {
        var body = StealthBodyOf(charId);
        if (body == null) return;
        var node = Fx.Spawn(StealthFx, body, new Vector3(0, 1.0f, 0));
        if (node != null)
        {
            var timer = GetTree().CreateTimer(1.2);
            timer.Timeout += () => { if (GodotObject.IsInstanceValid(node)) node.QueueFree(); };
        }
    }

    private Node3D? StealthBodyOf(int charId)
    {
        if (charId == _myId) return GodotObject.IsInstanceValid(_self) ? _self : null;
        if (_ents.TryGetValue(charId, out var ent) && GodotObject.IsInstanceValid(ent.Body))
            return ent.Body;
        return null;
    }

    private static void StealthSetBodyAlpha(Node3D body, float alpha)
    {
        float transparency = Mathf.Clamp(1f - alpha, 0f, 1f);
        bool hidden = alpha <= StealthHiddenAlpha;
        foreach (var node in StealthDescendants(body))
            if (node is GeometryInstance3D gi)
            {
                StealthSetHidden(gi, hidden);
                if (!hidden) Fx.SetTransparency(gi, transparency);
                StealthSetShadow(gi, hidden);
            }
    }

    private static void StealthSetHidden(GeometryInstance3D gi, bool hidden)
    {
        if (hidden)
        {
            if (gi.HasMeta(StealthVisibleMeta)) return;
            gi.SetMeta(StealthVisibleMeta, gi.Visible);
            gi.Visible = false;
        }
        else if (gi.HasMeta(StealthVisibleMeta))
        {
            gi.Visible = gi.GetMeta(StealthVisibleMeta).AsBool();
            gi.RemoveMeta(StealthVisibleMeta);
        }
    }

    private static void StealthSetShadow(GeometryInstance3D gi, bool hidden)
    {
        if (hidden)
        {
            if (gi.HasMeta(StealthShadowMeta)) return;
            gi.SetMeta(StealthShadowMeta, (int)gi.CastShadow);
            gi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
        else if (gi.HasMeta(StealthShadowMeta))
        {
            gi.CastShadow = (GeometryInstance3D.ShadowCastingSetting)gi.GetMeta(StealthShadowMeta).AsInt32();
            gi.RemoveMeta(StealthShadowMeta);
        }
    }

    private static IEnumerable<Node> StealthDescendants(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            yield return child;
            foreach (var d in StealthDescendants(child))
                yield return d;
        }
    }
}
