using System;
using System.Collections.Generic;
using LibreKO.Domain;

namespace LibreKO.Network;

public partial class Net
{
    public const byte SubPartyWarBoard = 38;
    private const int WarBoardShareBytes = 6;

    public event Action<WarBoardReply>? PartyWarBoardEvent;

    public byte PartyRowMeterMode { get; private set; } = PartyWarBoard.ModeDamage;

    private void HandlePartyWarBoard(Packet p)
    {
        if (ReadWarBoard(p) is { } reply) PartyWarBoardEvent?.Invoke(reply);
    }

    internal static WarBoardReply? ReadWarBoard(Packet p)
    {
        if (p.RemainingBytes < 3) return null;
        byte target = p.ReadByte();
        byte mode = p.ReadByte();
        int count = p.ReadByte();
        var shares = new List<WarBoardShare>(count);
        for (int i = 0; i < count && p.RemainingBytes >= WarBoardShareBytes; i++)
            shares.Add(new WarBoardShare(p.ReadInt(), p.ReadUShort()));
        return new WarBoardReply(target, mode, shares);
    }

    public void SendPartyWarBoard(byte target, byte mode)
    {
        if (target == PartyWarBoard.TargetRows) PartyRowMeterMode = mode;
        var p = new Packet(GameOpcodes.GS_PARTY);
        p.WriteByte(SubPartyWarBoard);
        p.WriteByte(target);
        p.WriteByte(mode);
        _conn.Send(p);
    }
}
