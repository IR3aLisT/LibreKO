using System.Collections.Generic;
using LibreKO.Domain;

namespace LibreKO.Network;

public readonly record struct SmashPiece(int ItemId, byte BagSlot, ushort Count);

public sealed record ItemSmashReply(short Result, int ItemId, byte BagSlot, IReadOnlyList<SmashPiece> Pieces)
{
    private const int ResultBytes = 2;
    private const int HeadBytes = 4 + 1 + 2;
    private const int PieceBytes = 4 + 1 + 2;

    public static ItemSmashReply? Read(Packet p)
    {
        if (p.RemainingBytes < ResultBytes) return null;
        short result = p.ReadShort();
        if (result != ItemSmash.Succeeded) return new ItemSmashReply(result, 0, 0, []);
        if (p.RemainingBytes < HeadBytes) return null;
        int itemId = p.ReadInt();
        byte bagSlot = p.ReadByte();
        int count = p.ReadShort();
        var pieces = new List<SmashPiece>();
        for (int i = 0; i < count && i < ItemSmash.ResultSlots && p.RemainingBytes >= PieceBytes; i++)
            pieces.Add(new SmashPiece(p.ReadInt(), p.ReadByte(), p.ReadUShort()));
        return new ItemSmashReply(result, itemId, bagSlot, pieces);
    }
}
