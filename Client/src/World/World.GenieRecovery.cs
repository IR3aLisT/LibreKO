using Godot;
using LibreKO.Domain;

namespace LibreKO;

public partial class World
{
    private double _genieFeedAt, _geniePetPotionAt;

    private bool GenieRecover(double now)
    {
        if (!_genie.Has(GenieMode.Recovery)) return false;
        bool lowHp = GenieRecovery.Below(Vitals.Hp, Vitals.MaxHp, _genie.RecoveryPercent);
        bool lowMp = GenieRecovery.Below(Vitals.Mp, Vitals.MaxMp, _genie.RecoveryPercent);
        if ((lowHp || lowMp) && GenieRecoveryCast(_myId, lowHp, lowMp, now)) return true;
        foreach (var member in PartyMembers)
            if (member.CharId != _myId && member.Hp > 0 && _ents.TryGetValue(member.CharId, out var ally)
                && !ally.Dead && !ally.Attackable && GenieRecovery.Below(member.Hp, member.MaxHp, _genie.RecoveryPercent)
                && GenieRecoveryCast(member.CharId, true, false, now)) return true;
        return false;
    }

    private bool GenieRecoveryCast(int target, bool hp, bool mp, double now)
    {
        foreach (int id in _genie.Recovery)
        {
            var skill = SkillData.Get(id);
            if (skill == null || !((hp && GenieRestoresHp(skill)) || (mp && GenieRestoresMp(skill)))) continue;
            if (!SkillRequirementMet(skill) || !SkillReady(skill, now) || GenieSkillHeld(skill.Id, now) || !CanCastWithGear(skill)) continue;
            if (target == _myId && skill.Moral is not (SkillTarget.Self or SkillTarget.FriendWithMe or SkillTarget.Party or SkillTarget.PartyAll)) continue;
            if (target != _myId && (skill.Moral == SkillTarget.Self || !InSkillRange(target, skill))) continue;
            if (skill.FirstDamage <= 0 && GenieHasActiveBuff(skill, now)) continue;
            if (_genieSupportAt.TryGetValue((skill.Id, target), out var next) && now < next) continue;
            if (target == _myId && GenieHealStrikes(skill)) CastSkill(skill.Id);
            else GenieCastOn(target, skill.Id);
            _genieSupportAt[(skill.Id, target)] = now + GenieSupportRetryInterval;
            return true;
        }
        return false;
    }

    private bool GenieHealStrikes(SkillData.Skill skill) =>
        Net.I.HealStrikesEnemies && !_genie.Has(GenieOther.DarkKnightMace)
        && skill.Moral == SkillTarget.FriendWithMe
        && HealStrike.StrikesWith(skill.Type1, skill.DirectType, skill.FirstDamage, skill.TimeDamage)
        && GenieValidMonster(_selectedId);

    private void GenieFamiliarTick(double now)
    {
        if (Net.I.Pet is not { } pet || MyPetEntity() == null) return;
        if (GenieRecovery.WantsFeed(_genie.Has(GenieOther.FeedFamiliar), true, pet.Satisfaction) && now >= _genieFeedAt)
        {
            _genieFeedAt = now + GenieRecovery.FeedRetrySeconds;
            int food = BestPetFoodSlot();
            if (food >= 0) Net.I.SendPetFeed(food - GridStart, Inv[food].ItemId);
        }
        if (!_genie.Has(GenieMoreMode.PetPotion) || pet.Hp >= pet.MaxHp
            || !GenieRecovery.Below(pet.Hp, pet.MaxHp, _genie.PetPercent) || now < _geniePetPotionAt) return;
        _geniePetPotionAt = now + GenieRecovery.PetPotionRetrySeconds;
        int potion = _geniePetItem > 0 ? _geniePetItem : BestPotion(HealTarget.Hp);
        int slot = GenieBagSlotOf(potion);
        if (slot >= 0) Net.I.SendPetFeed(slot - GridStart, potion);
    }

    private int GenieBagSlotOf(int itemId)
    {
        if (itemId <= 0) return -1;
        for (int abs = GridStart; abs < GridStart + GridCount && abs < Inv.Length; abs++)
            if (Inv[abs].ItemId == itemId && Inv[abs].Count > 0 && Inv[abs].State != ItemFlag.Duplicate) return abs;
        return -1;
    }
}
