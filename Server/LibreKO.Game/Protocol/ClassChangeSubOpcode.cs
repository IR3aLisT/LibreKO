namespace LibreKO.Game.Protocol;

public enum ClassChangeSubOpcode : byte
{
    Eligibility = 1,
    JobChangeState = 2,
    StatReset = 3,
    SkillReset = 4,
    StatResetCost = 5,
    JobChangeResult = 6,
    RebirthStatChange = 7,
    RebirthStatReset = 8,
}
