using System;

namespace LibreKO.Network;

public partial class Net
{
    public const byte KnClanBattle = 0x13;
    public const byte KnClanBattleAnswer = 0x14;
    public const byte KnClanBattleSurrender = 0x15;
    public const byte KnClanBattleBegun = 0x16;
    public const byte KnClanBattleEnd = 0x17;

    public const byte ClanBattleChallenged = 1;
    public const byte ClanBattleTheySurrendered = 1;
    public const byte ClanBattleCleared = 2;

    public short EnemyClanId { get; private set; }

    public event Action<byte, int, string>? ClanBattleResultEvent;
    public event Action<short>? ClanBattleBegunEvent;
    public event Action<byte, string>? ClanBattleEndEvent;

    private void HandleClanBattleResult(Packet p)
    {
        if (ClanBattleWire.ReadResult(p) is { } reply)
            ClanBattleResultEvent?.Invoke(reply.Result, reply.ChallengerId, reply.Challenger);
    }

    private void HandleClanBattleBegun(Packet p)
    {
        if (p.RemainingBytes < 2) return;
        SetEnemyClan(p.ReadShort());
        ClanBattleBegunEvent?.Invoke(EnemyClanId);
    }

    private void HandleClanBattleEnd(Packet p)
    {
        if (ClanBattleWire.ReadEnd(p) is not { } end) return;
        if (end.Kind == ClanBattleCleared) SetEnemyClan(0);
        ClanBattleEndEvent?.Invoke(end.Kind, end.Clan);
    }

    private void SetEnemyClan(short clanId)
    {
        EnemyClanId = clanId;
        foreach (var known in _known.Values)
            if (!known.IsNpc) known.Attackable = IsHostilePlayer(known);
    }

    public bool AtClanWarWith(EntitySnapshot e) => EnemyClanId > 0 && e.KnightsId == EnemyClanId;

    public void SendClanBattleChallenge(int targetId)
    {
        var p = new Packet(GameOpcodes.GS_KNIGHTS_PROCESS);
        p.WriteByte(KnClanBattle);
        p.WriteInt(targetId);
        _conn.Send(p);
    }

    public void SendClanBattleAnswer(bool accept, int challengerId)
    {
        var p = new Packet(GameOpcodes.GS_KNIGHTS_PROCESS);
        p.WriteByte(KnClanBattleAnswer);
        p.WriteByte((byte)(accept ? 1 : 0));
        p.WriteInt(challengerId);
        _conn.Send(p);
    }

    public void SendClanBattleSurrender()
    {
        var p = new Packet(GameOpcodes.GS_KNIGHTS_PROCESS);
        p.WriteByte(KnClanBattleSurrender);
        _conn.Send(p);
    }
}

public static class ClanBattleWire
{
    public readonly record struct ResultReply(byte Result, int ChallengerId, string Challenger);
    public readonly record struct EndReply(byte Kind, string Clan);

    public static ResultReply? ReadResult(Packet p)
    {
        if (p.RemainingBytes < 1) return null;
        byte result = p.ReadByte();
        if (result != Net.ClanBattleChallenged || p.RemainingBytes < 6) return new ResultReply(result, 0, "");
        int challenger = p.ReadInt();
        p.ReadUShort();
        return new ResultReply(result, challenger, p.RemainingBytes >= 2 ? p.ReadString() : "");
    }

    public static EndReply? ReadEnd(Packet p)
    {
        if (p.RemainingBytes < 1) return null;
        byte kind = p.ReadByte();
        if (kind == Net.ClanBattleCleared || p.RemainingBytes < 6) return new EndReply(kind, "");
        p.ReadShort();
        p.ReadUShort();
        return new EndReply(kind, p.ReadString());
    }
}
