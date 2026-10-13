using System.Collections.Generic;

namespace LibreKO.Network;

public readonly record struct RepurchaseRow(byte Row, int ItemId, int Price, uint DeletesAt);

public sealed class RepurchaseReply
{
    public const byte Open = 1;
    public const byte Buy = 2;
    public const byte Close = 3;
    public const byte Refresh = 4;
    public const byte Succeeded = 1;
    public const short NoRoom = -4;
    public const short Expired = -6;
    public const short NoCoins = -8;

    public byte Op { get; private init; }
    public bool Ok { get; private init; }
    public List<RepurchaseRow> Rows { get; } = new();
    public byte BagPosition { get; private init; }
    public int ItemId { get; private init; }
    public byte Flag { get; private init; }
    public short Error { get; private init; }

    public static RepurchaseReply Read(Packet p)
    {
        byte op = p.RemainingBytes >= 1 ? p.ReadByte() : (byte)0;
        if (op is Open or Refresh)
        {
            bool ok = p.RemainingBytes >= 1 && p.ReadByte() == Succeeded;
            var list = new RepurchaseReply { Op = op, Ok = ok };
            if (!ok || p.RemainingBytes < 1) return list;
            int count = p.ReadByte();
            for (int i = 0; i < count && p.RemainingBytes >= 13; i++)
                list.Rows.Add(new RepurchaseRow(p.ReadByte(), p.ReadInt(), p.ReadInt(), p.ReadUInt()));
            return list;
        }
        if (op != Buy || p.RemainingBytes < 2) return new RepurchaseReply { Op = op };
        bool bought = p.ReadByte() == Succeeded;
        p.ReadByte();
        if (bought)
        {
            if (p.RemainingBytes < 6) return new RepurchaseReply { Op = op };
            return new RepurchaseReply { Op = op, Ok = true, BagPosition = p.ReadByte(), ItemId = p.ReadInt(), Flag = p.ReadByte() };
        }
        if (p.RemainingBytes < 6) return new RepurchaseReply { Op = op };
        return new RepurchaseReply { Op = op, ItemId = p.ReadInt(), Error = p.ReadShort() };
    }
}
