using System.Collections.Generic;
using Godot;
using LibreKO.Domain;

namespace LibreKO;

public partial class World
{
    private static readonly Color ContinuanceNoticeColour = new("ffff00");
    private static readonly Color ContinuanceRefusalColour = new("ff3b3b");

    private int _continuanceSkill;
    private readonly HashSet<int> _extendedBuffs = new();

    private void ContinuanceInit() => Net.I.BuffExtendedEvent += OnBuffExtended;

    private void ContinuanceDispose() => Net.I.BuffExtendedEvent -= OnBuffExtended;

    private bool BeginContinuance(SkillData.Skill item)
    {
        if (!Continuance.IsContinuanceItem(item.Moral)) return false;
        _continuanceSkill = item.Id;
        CombatNotice(SystemText(Continuance.UsingText, "Using %s", item.Name), ContinuanceNoticeColour);
        return true;
    }

    private void CancelContinuance() => _continuanceSkill = 0;

    private bool ContinuanceClick(int buffSkillId)
    {
        if (_continuanceSkill == 0) return false;
        if (SkillData.Get(_continuanceSkill) == null)
        {
            CancelContinuance();
            CombatNotice(SystemText(Continuance.NoItemText, "Skill failed - Improper item"), ContinuanceRefusalColour);
            return true;
        }
        var buff = SkillData.Get(buffSkillId);
        var check = buff == null
            ? ContinuanceCheck.Cannot
            : Continuance.Check(buff.Id, buff.Type1, buff.Type2, buff.Moral, _extendedBuffs.Contains(buff.Id));
        switch (check)
        {
            case ContinuanceCheck.Cannot:
                CombatNotice(SystemText(Continuance.CannotText, "Cannot use Continuance Item for this skill."), ContinuanceRefusalColour);
                break;
            case ContinuanceCheck.Already:
                CombatNotice(SystemText(Continuance.AlreadyText, "This skill is already using the Continuance Item."), ContinuanceRefusalColour);
                break;
            case ContinuanceCheck.Send:
                Net.I.SendMagic(MagicSub.ExtendDuration, buffSkillId, _myId);
                CancelContinuance();
                break;
        }
        return true;
    }

    private void OnBuffExtended(int skillId)
    {
        CancelContinuance();
        if (SkillData.Get(skillId) is not { } s) return;
        _extendedBuffs.Add(skillId);
        if (s.Duration > 0 && Net.I.BuffEnds.TryGetValue(skillId, out double end))
            AddBuffChip(s, end + s.Duration);
        CombatNotice(SystemText(Continuance.UsedText, "Continuance Item has been used on the %s skill.", s.Name), ContinuanceRefusalColour);
    }

    private void ForgetExtendedBuff(int skillId) => _extendedBuffs.Remove(skillId);
}
