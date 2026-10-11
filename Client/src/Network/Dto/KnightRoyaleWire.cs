namespace LibreKO.Network;

public readonly record struct RoyaleSignUp(int Result, int Participants, int Seconds, int Mode);

public readonly record struct RoyaleEntry(int Result, int Level, int MaxExp, int Exp);

public readonly record struct RoyaleStats(int CharId, int Level, int MaxExp, int Exp, int MaxHp, int Hp, int MaxMp, int Mp);

public readonly record struct RoyaleItem(int Slot, int ItemId, int Count, int Durability, int Flag);

public readonly record struct RoyaleLoot(int BoxId, int SlotOrCode, int ItemId, int Count);

public static class KnightRoyaleWire
{
    public const byte SubSignUp = 1;
    public const byte SubEntry = 2;
    public const byte SubProgress = 3;
    public const byte SubNotice = 4;
    public const byte SubLoot = 5;
    public const byte SubItemAdd = 6;
    public const byte SubRestore = 7;
    public const byte SubDestroy = 8;
    public const byte SubCombine = 9;

    public const byte ProgressExp = 1;
    public const byte ProgressStats = 2;

    public const byte NoticeAbandoned = 1;
    public const byte NoticeGas = 2;
    public const byte NoticeChest = 3;
    public const byte NoticeReward = 4;
    public const byte NoticeWinner = 5;
    public const byte NoticeDelegate = 6;
    public const byte NoticeSurvivors = 7;

    public const byte Apply = 1;
    public const byte Cancel = 2;

    public const int LootFailed = 0xFFF9;
    public const int LootQuantity = 0xFFF5;
    public const int LootWeight = 0xFFFE;
    public const int MaxBagSlot = 8;

    public static RoyaleSignUp ReadSignUp(Packet p)
    {
        int result = p.RemainingBytes >= 1 ? p.ReadByte() : 0;
        int participants = 0, seconds = 0, mode = 0;
        if (result is Domain.KnightRoyale.SignUpCount or Domain.KnightRoyale.SignUpOpen && p.RemainingBytes >= 1)
            participants = p.ReadByte();
        if (result == Domain.KnightRoyale.SignUpOpen && p.RemainingBytes >= 3)
        {
            seconds = p.ReadUShort();
            mode = p.ReadByte();
        }
        return new RoyaleSignUp(result, participants, seconds, mode);
    }

    public static RoyaleEntry ReadEntry(Packet p) => p.RemainingBytes >= 6
        ? new RoyaleEntry(p.ReadByte(), p.ReadByte(), p.ReadShort(), p.ReadShort())
        : default;

    public static RoyaleStats ReadStats(Packet p) => p.RemainingBytes >= 17
        ? new RoyaleStats(p.ReadInt(), p.ReadByte(), p.ReadShort(), p.ReadShort(), p.ReadShort(), p.ReadShort(), p.ReadShort(), p.ReadShort())
        : default;

    public static RoyaleItem ReadItem(Packet p) => p.RemainingBytes >= 10
        ? new RoyaleItem(p.ReadByte(), p.ReadInt(), p.ReadShort(), p.ReadShort(), p.ReadByte())
        : default;

    public static RoyaleLoot ReadLoot(Packet p)
    {
        if (p.RemainingBytes < 6) return default;
        int box = p.ReadInt();
        int slot = p.ReadUShort();
        if (slot is LootFailed or LootQuantity or LootWeight || p.RemainingBytes < 6)
            return new RoyaleLoot(box, slot, 0, 0);
        return new RoyaleLoot(box, slot, p.ReadInt(), p.ReadUShort());
    }

    public static (string Name, int Value) ReadNamed(Packet p)
    {
        string name = p.RemainingBytes >= 1 ? p.ReadSByteString() : "";
        int value = p.RemainingBytes >= 1 ? p.ReadByte() : 0;
        return (name, value);
    }

    public static (int X, int Z) ReadSpot(Packet p) => p.RemainingBytes >= 4 ? (p.ReadShort(), p.ReadShort()) : (0, 0);

    public static Domain.KnightRoyale.Reward ReadReward(Packet p) => p.RemainingBytes >= 5
        ? new Domain.KnightRoyale.Reward(p.ReadByte(), p.ReadByte(), p.ReadByte(), p.ReadShort())
        : default;
}
