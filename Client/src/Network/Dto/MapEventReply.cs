using System;

namespace LibreKO.Network;

public enum MapEventKind
{
    Owners,
    Captured,
    Points,
    Provisions,
    Cont,
    Result,
    Clock,
}

public sealed record MapEventReply(MapEventKind Kind, byte[] Owners, int Index, int Nation, long A, long B)
{
    public const byte SubOwners = 0;
    public const byte SubCaptured = 1;
    public const byte SubPoints = 2;
    public const byte SubProvisions = 3;
    public const byte SubCont = 4;
    public const byte SubResult = 5;
    public const byte SubClock = 6;
    private const byte CapturedOne = 1;

    public static MapEventReply? Read(Packet p)
    {
        if (p.RemainingBytes < 1) return null;
        return p.ReadByte() switch
        {
            SubOwners when p.RemainingBytes >= 1 => ReadOwners(p),
            SubCaptured when p.RemainingBytes >= 3 && p.ReadByte() == CapturedOne =>
                Of(MapEventKind.Captured, index: p.ReadByte(), nation: p.ReadByte()),
            SubPoints when p.RemainingBytes >= 2 * sizeof(ushort) => Of(MapEventKind.Points, a: p.ReadUShort(), b: p.ReadUShort()),
            SubProvisions when p.RemainingBytes >= 1 + sizeof(ushort) =>
                Of(MapEventKind.Provisions, nation: p.ReadByte(), a: p.ReadUShort()),
            SubCont when p.RemainingBytes >= sizeof(uint) => Of(MapEventKind.Cont, a: p.ReadUInt()),
            SubResult when p.RemainingBytes >= 1 => Of(MapEventKind.Result, nation: p.ReadByte()),
            SubClock when p.RemainingBytes >= sizeof(int) => Of(MapEventKind.Clock, a: p.ReadInt()),
            _ => null,
        };
    }

    private static MapEventReply ReadOwners(Packet p)
    {
        int count = p.ReadByte();
        var owners = new byte[Math.Min(count, p.RemainingBytes)];
        for (int i = 0; i < owners.Length; i++) owners[i] = p.ReadByte();
        return new MapEventReply(MapEventKind.Owners, owners, 0, 0, 0, 0);
    }

    private static MapEventReply Of(MapEventKind kind, int index = 0, int nation = 0, long a = 0, long b = 0) =>
        new(kind, Array.Empty<byte>(), index, nation, a, b);
}
