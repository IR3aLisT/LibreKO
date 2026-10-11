using LibreKO.Domain;

namespace LibreKO.Network;

public readonly record struct CharacterSealList(bool Ok, int Code, CharacterSummary[] Slots);

public readonly record struct CharacterSealOutcome(int Result, int Code, int BagPos, int ItemId, int UniqueId, PetItemInfo? Summary);

public readonly record struct CharacterUnsealOutcome(int Result, int BagPos);

public sealed class SealedCharacterSheet
{
    public string Name = "";
    public int Nation, Race, Class, Level, Loyalty, Gold, FreePoints, Bonus;
    public int[] Stats = new int[CharacterSealWire.StatCount];
    public int[] StatBonus = new int[CharacterSealWire.StatCount];
    public int[] SkillTrees = new int[CharacterSealWire.SkillTrees];
    public ItemSlot[] Items = new ItemSlot[CharacterSealWire.SheetItems];
}

public static class CharacterSealWire
{
    public const byte Sub = 9;
    public const byte ListSub = 1;
    public const byte SealSub = 2;
    public const byte UnsealSub = 3;
    public const byte SheetSub = 4;
    public const byte AchievementsSub = 6;
    public const byte Failed = 0;
    public const byte Succeeded = 1;
    public const byte Extended = 2;
    public const int StatCount = 5;
    public const int SkillTrees = 4;
    public const int EquipItems = 14;
    public const int BagItems = 28;
    public const int SheetItems = EquipItems + BagItems;
    private const int ExtendedPadding = 5;
    private const int SheetSkillGap = 4;
    private const int AchievementIdBytes = 2;

    public static CharacterSealList ReadList(Packet p)
    {
        if (p.ReadByte() != Succeeded)
            return new CharacterSealList(false, p.RemainingBytes > 0 ? p.ReadByte() : 0, []);
        var slots = new CharacterSummary[CharacterRecordWire.SlotCount];
        for (int i = 0; i < slots.Length; i++)
            slots[i] = p.RemainingBytes >= sizeof(short) ? CharacterRecordWire.Read(p) : new CharacterSummary();
        return new CharacterSealList(true, 0, slots);
    }

    public static CharacterSealOutcome ReadSeal(Packet p)
    {
        int result = p.ReadByte();
        switch (result)
        {
            case Succeeded:
            {
                int bagPos = p.ReadByte();
                int itemId = p.ReadInt();
                int uniqueId = p.ReadInt();
                p.ReadString();
                var summary = PetWire.ReadUniqueBlock(p, uniqueId);
                return new CharacterSealOutcome(result, 0, bagPos, itemId, uniqueId, summary);
            }
            case Extended:
                p.ReadBytes(ExtendedPadding);
                return new CharacterSealOutcome(result, p.ReadShort(), -1, 0, 0, null);
            default:
                return new CharacterSealOutcome(result, p.RemainingBytes > 0 ? p.ReadByte() : 0, -1, 0, 0, null);
        }
    }

    public static CharacterUnsealOutcome ReadUnseal(Packet p)
    {
        int result = p.ReadByte();
        return result == Succeeded
            ? new CharacterUnsealOutcome(result, p.ReadByte())
            : new CharacterUnsealOutcome(result, -1);
    }

    public static SealedCharacterSheet? ReadSheet(Packet p)
    {
        if (p.ReadByte() != Succeeded) return null;
        var sheet = new SealedCharacterSheet
        {
            Name = p.ReadSByteString(),
            Nation = p.ReadByte(),
            Race = p.ReadByte(),
            Class = p.ReadUShort(),
            Level = p.ReadByte(),
            Loyalty = p.ReadInt(),
        };
        for (int i = 0; i < StatCount; i++) sheet.Stats[i] = p.ReadByte();
        sheet.Gold = p.ReadInt();
        sheet.FreePoints = p.ReadByte();
        p.ReadBytes(SheetSkillGap);
        for (int i = 0; i < SkillTrees; i++) sheet.SkillTrees[i] = p.ReadByte();
        for (int i = 0; i < SheetItems; i++)
        {
            int itemId = p.ReadInt();
            short durability = (short)p.ReadUShort();
            short count = (short)p.ReadUShort();
            byte flag = p.ReadByte();
            sheet.Items[i] = new ItemSlot { ItemId = itemId, Durability = durability, Count = count, Flag = flag };
        }
        if (p.RemainingBytes >= 1 + StatCount)
        {
            sheet.Bonus = p.ReadByte();
            for (int i = 0; i < StatCount; i++) sheet.StatBonus[i] = p.ReadByte();
        }
        return sheet;
    }

    public static int[] ReadAchievements(Packet p)
    {
        if (p.ReadByte() != Succeeded || p.RemainingBytes < sizeof(uint)) return [];
        int bytes = (int)p.ReadUInt();
        int count = System.Math.Min(bytes / AchievementIdBytes, p.RemainingBytes / AchievementIdBytes);
        var ids = new int[count];
        for (int i = 0; i < count; i++) ids[i] = p.ReadUShort();
        return ids;
    }
}
