using System;

namespace LibreKO.Network;

public readonly record struct ItemGetReply
{
    public const byte Failed = 0;
    public const byte Taken = 1;
    public const byte PartyCoins = 2;
    public const byte PartyMember = 3;
    public const byte TakenToSlot = 5;
    public const byte TemporaryKept = 7;
    public const byte TemporaryLost = 8;
    public const byte GoldPosition = byte.MaxValue;
    public const int NoBundleSlot = -1;

    public const int HeaderBytes = 1 + 4;
    public const int FailureBytes = 4;
    public const int PartyCoinBytes = 1 + 4 + 4;
    public const int TakenBytes = 1 + 4 + 2 + 4;
    public const int BundleSlotBytes = 2;
    public const int TemporaryBytes = 1 + 4;

    public byte Result { get; init; }
    public int BundleId { get; init; }
    public int Failure { get; init; }
    public byte Position { get; init; }
    public int ItemId { get; init; }
    public short Count { get; init; }
    public int Money { get; init; }
    public int BundleSlot { get; init; }
    public string? Looter { get; init; }

    public bool IsGold => Result == PartyCoins || Position == GoldPosition;

    public static ItemGetReply? Read(Packet p)
    {
        if (p.RemainingBytes < HeaderBytes) return null;
        var reply = new ItemGetReply { Result = p.ReadByte(), BundleId = p.ReadInt(), BundleSlot = NoBundleSlot };

        if (reply.Result == Failed)
            return reply with { Failure = p.RemainingBytes >= FailureBytes ? p.ReadInt() : 0 };

        if (reply.Result == PartyMember)
        {
            if (p.RemainingBytes < 4) return null;
            reply = reply with { ItemId = p.ReadInt(), Looter = p.ReadString() };
            return p.RemainingBytes >= BundleSlotBytes ? reply with { BundleSlot = p.ReadUShort() } : reply;
        }

        if (reply.Result is TemporaryKept or TemporaryLost)
        {
            if (p.RemainingBytes < TemporaryBytes) return null;
            p.ReadByte();
            return reply with { ItemId = p.ReadInt() };
        }

        if (reply.Result == PartyCoins)
            return p.RemainingBytes < PartyCoinBytes
                ? null
                : reply with { Position = p.ReadByte(), ItemId = p.ReadInt(), Money = p.ReadInt() };

        if (reply.Result is not (Taken or TakenToSlot) || p.RemainingBytes < TakenBytes) return null;
        reply = reply with
        {
            Position = p.ReadByte(),
            ItemId = p.ReadInt(),
            Count = (short)Math.Min((int)p.ReadUShort(), short.MaxValue),
            Money = p.ReadInt(),
        };
        return p.RemainingBytes >= BundleSlotBytes ? reply with { BundleSlot = p.ReadUShort() } : reply;
    }
}
