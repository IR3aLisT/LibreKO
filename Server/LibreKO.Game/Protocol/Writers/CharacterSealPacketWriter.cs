using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Infrastructure.Network;

namespace LibreKO.Game.Protocol.Writers;

public static class CharacterSealPacketWriter
{
    public const byte SealSub = 9;
    public const byte ListSub = 1;
    public const byte SealCharacterSub = 2;
    public const byte UnsealSub = 3;
    public const byte SheetSub = 4;
    public const byte AchievementsSub = 6;
    public const byte Failed = 0;
    public const byte Succeeded = 1;
    public const byte Extended = 2;
    public const byte SheetFailed = 2;
    private const int ExtendedPadding = 5;

    public readonly record struct RingSummary(string Name, byte Class, byte Level, ushort ExpHundredths, byte Race, byte Rebirth = 0);

    public readonly record struct Sheet(
        string Name, byte Nation, byte Race, short Class, byte Level, int Loyalty,
        byte Strength, byte Stamina, byte Dexterity, byte Intelligence, byte Magic, int Gold,
        byte FreePoints, byte[] SkillTrees, IReadOnlyList<ItemSlot> Items);

    public static Packet List(IReadOnlyList<PreGamePacketWriter.CharacterSlot?> slots)
    {
        var packet = Head(ListSub, Succeeded);
        foreach (var slot in slots)
            PreGamePacketWriter.WriteSlot(packet, slot);
        return packet;
    }

    public static Packet ListFailed(byte code)
    {
        var packet = Head(ListSub, Failed);
        packet.WriteByte(code);
        return packet;
    }

    public static Packet Sealed(byte bagPos, int ringId, int uniqueId, RingSummary summary)
    {
        var packet = Head(SealCharacterSub, Succeeded);
        packet.WriteByte(bagPos);
        packet.WriteInt(ringId);
        packet.WriteInt(uniqueId);
        packet.WriteString(summary.Name);
        WriteSummary(packet, summary);
        return packet;
    }

    public static Packet SealFailed(byte code)
    {
        var packet = Head(SealCharacterSub, Failed);
        packet.WriteByte(code);
        return packet;
    }

    public static Packet SealRefused(short code)
    {
        var packet = Head(SealCharacterSub, Extended);
        for (int i = 0; i < ExtendedPadding; i++) packet.WriteByte(0);
        packet.WriteShort(code);
        return packet;
    }

    public static Packet Unsealed(byte bagPos, int ringUniqueId)
    {
        var packet = Head(UnsealSub, Succeeded);
        packet.WriteByte(bagPos);
        packet.WriteInt(ringUniqueId);
        return packet;
    }

    public static Packet UnsealFailed()
    {
        var packet = Head(UnsealSub, Failed);
        packet.WriteByte(0);
        return packet;
    }

    public static Packet SheetRefused() => Head(SheetSub, SheetFailed);

    public static Packet CharacterSheet(Sheet sheet)
    {
        var packet = Head(SheetSub, Succeeded);
        packet.WriteSByteString(sheet.Name);
        packet.WriteByte(sheet.Nation);
        packet.WriteByte(sheet.Race);
        packet.WriteUShort((ushort)sheet.Class);
        packet.WriteByte(sheet.Level);
        packet.WriteInt(sheet.Loyalty);
        packet.WriteByte(sheet.Strength);
        packet.WriteByte(sheet.Stamina);
        packet.WriteByte(sheet.Dexterity);
        packet.WriteByte(sheet.Intelligence);
        packet.WriteByte(sheet.Magic);
        packet.WriteInt(sheet.Gold);
        packet.WriteByte(sheet.FreePoints);
        packet.WriteInt(1);
        foreach (var tree in sheet.SkillTrees)
            packet.WriteByte(tree);
        foreach (var item in sheet.Items)
        {
            packet.WriteInt(item.ItemId);
            packet.WriteUShort((ushort)Math.Max((short)0, item.Durability));
            packet.WriteUShort(item.Count);
            packet.WriteByte((byte)item.Flag);
        }
        for (int i = 0; i < 6; i++)
            packet.WriteByte(0);
        return packet;
    }

    public static Packet NoAchievements()
    {
        var packet = Head(AchievementsSub, Succeeded);
        packet.WriteUInt(0);
        return packet;
    }

    public static void WriteSummary(Packet packet, RingSummary summary)
    {
        packet.WriteString(summary.Name);
        packet.WriteByte(summary.Class);
        packet.WriteByte(summary.Level);
        packet.WriteUShort(summary.ExpHundredths);
        packet.WriteUShort(summary.Race);
        packet.WriteByte(summary.Rebirth);
    }

    private static Packet Head(byte kind, byte result)
    {
        var packet = new Packet(GameOpcodes.GS_ITEM_UPGRADE);
        packet.WriteByte(SealSub);
        packet.WriteByte(kind);
        packet.WriteByte(result);
        return packet;
    }
}
