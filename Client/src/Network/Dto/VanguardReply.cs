using System;
using System.Collections.Generic;

namespace LibreKO.Network;

public enum VanguardReplyKind
{
    Invite,
    Appointed,
    AppointFailed,
    List,
    Positions,
    Remove,
    Closed,
}

public readonly record struct VanguardMark(int X, int Z, string Name);

public sealed record VanguardReply(VanguardReplyKind Kind, IReadOnlyList<string> Names, IReadOnlyList<VanguardMark> Marks)
{
    public const byte SubAppoint = 1;
    public const byte SubWanted = 2;
    public const byte SubClosed = 3;
    public const byte AppointInvite = 1;
    public const byte AppointResult = 2;
    public const byte WantedList = 1;
    public const byte WantedPositions = 2;
    public const byte WantedRemove = 3;
    public const byte AcceptRequest = 0;
    private const byte Appointed = 1;
    private const byte Failed = 2;
    private const int MarkBytes = 3 * sizeof(short);

    public static VanguardReply? Read(Packet p)
    {
        if (p.RemainingBytes < 1) return null;
        byte sub = p.ReadByte();
        if (sub == SubClosed) return Of(VanguardReplyKind.Closed);
        if (p.RemainingBytes < 1) return null;
        byte kind = p.ReadByte();
        return (sub, kind) switch
        {
            (SubAppoint, AppointInvite) => Of(VanguardReplyKind.Invite),
            (SubAppoint, AppointResult) when p.RemainingBytes >= 1 => p.ReadByte() switch
            {
                Appointed => Of(VanguardReplyKind.Appointed),
                Failed => Of(VanguardReplyKind.AppointFailed),
                _ => null,
            },
            (SubWanted, WantedList) => ReadList(p),
            (SubWanted, WantedPositions) => ReadPositions(p),
            (SubWanted, WantedRemove) when p.RemainingBytes >= sizeof(short) =>
                new VanguardReply(VanguardReplyKind.Remove, new[] { p.ReadString() }, Array.Empty<VanguardMark>()),
            _ => null,
        };
    }

    private static VanguardReply Of(VanguardReplyKind kind) =>
        new(kind, Array.Empty<string>(), Array.Empty<VanguardMark>());

    private static VanguardReply? ReadList(Packet p)
    {
        if (p.RemainingBytes < 1) return null;
        int count = p.ReadByte();
        var names = new List<string>(count);
        for (int i = 0; i < count && p.RemainingBytes >= sizeof(short); i++)
            names.Add(p.ReadString());
        return new VanguardReply(VanguardReplyKind.List, names, Array.Empty<VanguardMark>());
    }

    private static VanguardReply? ReadPositions(Packet p)
    {
        if (p.RemainingBytes < sizeof(short)) return null;
        int count = Math.Max(0, (int)p.ReadShort());
        var marks = new List<VanguardMark>(count);
        for (int i = 0; i < count && p.RemainingBytes >= MarkBytes; i++)
        {
            int x = p.ReadShort();
            int z = p.ReadShort();
            marks.Add(new VanguardMark(x, z, p.ReadString()));
        }
        var names = marks.ConvertAll(m => m.Name);
        return new VanguardReply(VanguardReplyKind.Positions, names, marks);
    }
}
