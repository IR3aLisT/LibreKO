using System;

namespace LibreKO.Network;

public partial class Net
{
    public event Action<int, int, int, long, long, int, int, int, int>? LevelChangeEvent;
    public event Action<int, int>? PeerLevelChangeEvent;
    public event Action<int, int>? LoyaltyChangeEvent;
    public event Action<int>? MannerChangeEvent;
    public event Action<int>? WeightChangeEvent;
    public event Action<int, int, int>? StateChangeEvent;

    public int SelfTransformSkill { get; private set; }
    public int SelfAbnormal { get; private set; }
    public const int NoExpeditionSide = -1;
    public int SelfExpeditionSide { get; private set; } = NoExpeditionSide;
    public bool SelfDevilForm { get; private set; }

    private void HandleLevelChange(Packet p)
    {
        if (p.RemainingBytes < 5) return;
        int charId = p.ReadInt();
        if (charId != MyCharId)
        {
            PeerLevelChangeEvent?.Invoke(charId, p.ReadByte());
            return;
        }
        int level = p.ReadByte();
        int statPoints = p.ReadShort();
        int skillPool = p.ReadByte();
        long maxExp = p.ReadLong();
        long exp = p.ReadLong();
        int maxHp = p.ReadShort(); int hp = p.ReadShort();
        int maxMp = p.ReadShort(); int mp = p.ReadShort();
        if (p.RemainingBytes >= 4) Sheet.SetMaxWeight(p.ReadInt());
        LevelChangeEvent?.Invoke(level, statPoints, skillPool, maxExp, exp, maxHp, hp, maxMp, mp);
    }

    private const int LoyaltyNationalSub = 1;
    private const int LoyaltyMannerSub = 2;

    private void HandleLoyaltyChange(Packet p)
    {
        if (p.RemainingBytes < 1) return;
        int type = p.ReadByte();
        if (type == LoyaltyMannerSub)
        {
            if (p.RemainingBytes >= sizeof(int)) MannerChangeEvent?.Invoke(p.ReadInt());
            return;
        }
        if (type != LoyaltyNationalSub) return;
        int loyalty = p.RemainingBytes >= 4 ? p.ReadInt() : 0;
        int monthly = p.RemainingBytes >= 4 ? p.ReadInt() : 0;
        LoyaltyChangeEvent?.Invoke(loyalty, monthly);
    }

    private void HandleWeightChange(Packet p)
    {
        if (p.RemainingBytes < 4) return;
        WeightChangeEvent?.Invoke(p.ReadInt());
    }

    private void HandleStateChange(Packet p)
    {
        if (p.RemainingBytes < 5) return;
        int charId = p.ReadInt();
        int type = p.ReadByte();
        int value = p.RemainingBytes >= 4 ? p.ReadInt()
                  : p.RemainingBytes >= 1 ? p.ReadByte()
                  : 0;
        if (charId == MyCharId && type == StateChange.Transformation) SelfTransformSkill = value;
        if (charId == MyCharId && type == StateChange.Abnormal) SelfAbnormal = value;
        if (charId == MyCharId && type == StateChange.ExpeditionSide) SelfExpeditionSide = value;
        if (charId == MyCharId && type == StateChange.DevilForm) SelfDevilForm = value != 0;
        if (type == StateChange.Title) ApplyTitle(charId, value);
        NoteChaosState(charId, type, value);
        StateChangeEvent?.Invoke(charId, type, value);
    }
}
