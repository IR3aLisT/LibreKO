using System;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private bool _gesturePending;

    private void PressInteract()
    {
        if (TryStartGather()) return;
        if (_selfDead || IsStunned() || SelfCasting(Now()) || TradeHoldsInput) return;
        if (_selectedId >= 0 && _ents.ContainsKey(_selectedId)) FaceToward(_myId, _selectedId);
        _gesturePending = true;
    }

    private void GestureTick()
    {
        if (!_gesturePending) return;
        if (_selfDead)
        {
            _gesturePending = false;
            return;
        }

        if (_selectedId >= 0 && _ents.TryGetValue(_selectedId, out var target) && target.Attackable)
        {
            if (!InBasicAttackRange(_selectedId)) return;
            if (target.Dead && !Dying(target))
            {
                _gesturePending = false;
                CastSkill(Gestures.CorpseSkillFor((int)(GD.Randi() % Gestures.CorpseSkillChoices), Net.I.Nation));
                return;
            }
        }

        _gesturePending = false;
        _hasMoveTarget = false;
        _terrainMoveHeld = false;
        _autoMoveForward = false;
        Net.I.SendEmote(Gestures.TauntFor((int)(GD.Randi() % Gestures.TauntChoices)));
    }

    private void SendChatGesture(int action)
    {
        if (_selfDead || ZoneLooks.MutesChatGestures(_zone)) return;
        Net.I.SendEmote(action);
    }

    private void ApplyGesture(int charId, int action)
    {
        int clip = Gestures.ClipFor(action);
        if (clip < 0) return;
        PlaySkillAction(charId, clip, Array.Empty<string>(), ActionRankPosture, restart: true);
    }

    private void PlayCorpseJiggle(int id)
    {
        if (id == _myId)
        {
            if (!_selfDead || _selfAnim == null) return;
            double own = PlayActionOn(_selfAnim, Gestures.CorpseJiggleClip, Array.Empty<string>());
            if (own > 0) _selfActionUntil = Now() + own;
            return;
        }

        if (!_ents.TryGetValue(id, out var e) || !e.Dead || Dying(e) || e.IsNpc || e.Anim == null) return;
        double len = PlayActionOn(e.Anim, Gestures.CorpseJiggleClip, Array.Empty<string>());
        if (len > 0) e.ActionUntil = Now() + len;
    }
}
