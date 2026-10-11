using LibreKO.Domain;

namespace LibreKO.Network;

public partial class Net
{
    public event System.Action<int>? BuffExpiredEvent;

    public const byte TargetHpPollEcho = 1;

    public const byte SkillBarSave = 1;
    public const byte SkillBarLoad = 2;
    public const int SkillBarMaxSlots = HotbarLayout.Total;

    private void HandleTargetHp(Packet p)
    {
        int id = p.ReadInt();
        byte echo = p.ReadByte();
        int maxHp = p.ReadInt();
        int hp = p.ReadInt();
        int damage = p.RemainingBytes >= 4 ? p.ReadInt() : 0;
        if (echo == TargetHpPollEcho)
            EntityHpSyncEvent?.Invoke(id, hp, maxHp);
        else
            EntityHpEvent?.Invoke(id, hp, maxHp, damage);
    }

    private const byte AttackChainedFlag = 1;

    private void HandleAttack(Packet p)
    {
        byte attackType = p.ReadByte();
        byte result = p.ReadByte();
        int attackerId = p.ReadInt();
        int targetId = p.ReadInt();
        bool chained = p.RemainingBytes >= 1 && p.ReadByte() == AttackChainedFlag;
        AttackEvent?.Invoke(attackType, result, attackerId, targetId, chained);
    }

    public event System.Action<int>? BuffExtendedEvent;

    private void HandleMagicProcess(Packet p)
    {
        byte sub = p.ReadByte();
        if (HandleTransformationMagic(sub, p)) return;
        if (sub == MagicSub.DurationExpired)
        {
            if (p.RemainingBytes >= 1) BuffExpiredEvent?.Invoke(p.ReadByte());
            return;
        }
        if (sub == MagicSub.ExtendDuration)
        {
            if (p.RemainingBytes >= 4) BuffExtendedEvent?.Invoke(p.ReadInt());
            return;
        }
        if (p.RemainingBytes < 12)
        {
            MagicEvent?.Invoke(sub, 0, 0, 0, new short[7]);
            return;
        }

        int skillId = p.ReadInt();
        int casterId = p.ReadInt();
        int targetId = p.ReadInt();
        var data = new short[7];
        for (int i = 0; i < 7 && p.RemainingBytes >= 4; i++)
            data[i] = (short)p.ReadInt();
        MagicEvent?.Invoke(sub, skillId, casterId, targetId, data);
    }

    private void HandleDead(Packet p)
    {
        int victim = p.ReadInt();
        int killer = p.RemainingBytes >= 4 ? p.ReadInt() : -1;
        if (_known.TryGetValue(victim, out var known)) known.Dead = true;
        DeadEvent?.Invoke(victim, killer);
    }

    private void HandleHpChange(Packet p)
    {
        int maxHp = p.ReadShort();
        int hp = p.ReadShort();
        int attackerId = p.RemainingBytes >= 4 ? p.ReadInt() : -1;
        SelfHpEvent?.Invoke(hp, maxHp, attackerId);
    }

    private void HandleMspChange(Packet p)
    {
        int maxMp = p.ReadShort();
        int mp = p.ReadShort();
        SelfMpEvent?.Invoke(mp, maxMp);
    }

    private const byte StaminaBar = 1;
    private const byte StaminaBarShown = 1;

    private void HandleStaminaChange(Packet p)
    {
        if (p.RemainingBytes < 4) return;
        byte kind = p.ReadByte();
        byte shown = p.ReadByte();
        if (kind != StaminaBar || shown != StaminaBarShown) return;
        int maxSp = p.ReadByte();
        int sp = p.ReadByte();
        SelfStaminaEvent?.Invoke(sp, maxSp);
    }

    public event Action<int, int>? ResurrectOfferEvent;
    public event Action<ushort, int, ushort>? RegeneRefusedEvent;

    private void HandleRegene(Packet p)
    {
        if (RegeneReply.Read(p) is not { } reply) return;
        switch (reply.Type)
        {
            case RegeneReply.Offer:
                ResurrectOfferEvent?.Invoke(reply.CasterId, reply.SkillId);
                break;
            case RegeneReply.Refused:
                RegeneRefusedEvent?.Invoke(reply.Code, reply.ItemId, reply.Amount);
                break;
            default:
                RegeneEvent?.Invoke(reply.X, reply.Z);
                break;
        }
    }

    public int[]? SkillBar { get; private set; }
    private bool _skillBarRequested;

    public void RequestSkillBarOnce()
    {
        if (_skillBarRequested) return;
        _skillBarRequested = true;
        SendSkillDataLoad();
    }

    private void ForgetSkillBar()
    {
        _skillBarRequested = false;
        SkillBar = null;
    }

    private void HandleSkillData(Packet p)
    {
        if (p.RemainingBytes < 2) { SkillDataEvent?.Invoke(System.Array.Empty<int>()); return; }
        int count = p.ReadShort();
        if (count <= 0)
        {
            SkillBar = System.Array.Empty<int>();
            SkillBarClearEvent?.Invoke();
            return;
        }
        var ids = new int[count];
        for (int i = 0; i < count && p.RemainingBytes >= 4; i++)
            ids[i] = p.ReadInt();
        SkillBar = ids;
        SkillDataEvent?.Invoke(ids);
    }
}
