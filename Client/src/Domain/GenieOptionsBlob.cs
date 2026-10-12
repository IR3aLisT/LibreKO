using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace LibreKO.Domain;

public enum GenieCellKind
{
    Attack,
    Recovery,
    Support,
}

public static class GenieOptionsBlob
{
    public const int Size = 256;
    public const int SecondLayoutSize = 128;
    public const byte FirstLayout = 1;
    public const byte SecondLayout = 2;
    public const byte CurrentLayout = 3;
    public const int LayoutOffset = 0;
    public const int ModesOffset = 1;
    public const int HpPercentOffset = 2;
    public const int MpPercentOffset = 3;
    public const int RangeOffset = 4;
    public const int SkillIdBytes = sizeof(int);
    public const int GroupCells = 8;
    public const int AttackOffset = 5;
    public const int RecoveryOffset = AttackOffset + GroupCells * SkillIdBytes;
    public const int SupportOffset = RecoveryOffset + GroupCells * SkillIdBytes;
    public const int ExtrasOffset = SupportOffset + GroupCells * SkillIdBytes;
    public const int OtherOffset = ExtrasOffset + 1;
    public const int TownHpCountOffset = OtherOffset + 1;
    public const int TownMpCountOffset = TownHpCountOffset + sizeof(ushort);
    public const int RecoveryPercentOffset = TownMpCountOffset + sizeof(ushort);
    public const int MoreModesOffset = RecoveryPercentOffset + 1;
    public const int PetPercentOffset = MoreModesOffset + 1;
    public const int SupportPartyOffset = PetPercentOffset + 1;
    public const int OtherHighOffset = SupportPartyOffset + sizeof(ushort);
    public const int HammerPercentOffset = OtherHighOffset + 1;
    public const int SupportTailOffset = SecondLayoutSize;
    public const int SecondSelfOffset = RecoveryOffset;
    public const int SecondPartyOffset = SupportOffset;
    public const int DefaultRecoveryPercent = 30;
    public const int DefaultPetPercent = 30;

    public static byte LayoutOf(byte[] bytes) => bytes.Length > LayoutOffset ? bytes[LayoutOffset] : (byte)0;

    public static byte[]? Upgrade(byte[] bytes, int size, Func<int, GenieCellKind> kindOf)
    {
        if (bytes.Length != size && bytes.Length != SecondLayoutSize) return null;
        byte layout = LayoutOf(bytes);
        if (layout is not (FirstLayout or SecondLayout or CurrentLayout)) return null;
        if (layout == CurrentLayout && bytes.Length == size) return bytes;
        var upgraded = new byte[size];
        Array.Copy(bytes, upgraded, Math.Min(bytes.Length, size));
        if (layout == CurrentLayout) return upgraded;
        if (layout == FirstLayout)
        {
            upgraded[LayoutOffset] = SecondLayout;
            upgraded[RecoveryPercentOffset] = bytes[HpPercentOffset];
        }
        SecondToCurrent(upgraded, kindOf);
        return upgraded;
    }

    private static void SecondToCurrent(byte[] bytes, Func<int, GenieCellKind> kindOf)
    {
        var self = ReadIds(bytes, SecondSelfOffset, GroupCells);
        var party = ReadIds(bytes, SecondPartyOffset, GroupCells);
        var recovery = new List<int>();
        var support = new List<(int Id, bool Party)>();
        foreach (int id in self)
        {
            if (id == 0) continue;
            if (kindOf(id) == GenieCellKind.Recovery) { if (!recovery.Contains(id)) recovery.Add(id); }
            else if (support.FindIndex(s => s.Id == id) < 0) support.Add((id, false));
        }
        foreach (int id in party)
        {
            if (id == 0) continue;
            if (kindOf(id) == GenieCellKind.Recovery) { if (!recovery.Contains(id)) recovery.Add(id); continue; }
            int at = support.FindIndex(s => s.Id == id);
            if (at >= 0) support[at] = (id, true);
            else support.Add((id, true));
        }
        var modes = (GenieMode)bytes[ModesOffset];
        bool selfOn = (modes & GenieMode.Support) != 0, partyOn = (modes & GenieMode.Recovery) != 0;
        modes &= ~(GenieMode.Support | GenieMode.Recovery);
        if ((selfOn || partyOn) && support.Count > 0) modes |= GenieMode.Support;
        if ((selfOn || partyOn) && recovery.Count > 0) modes |= GenieMode.Recovery;
        bytes[ModesOffset] = (byte)modes;
        var setupRecovery = new int[GroupCells];
        for (int i = 0; i < GroupCells && i < recovery.Count; i++) setupRecovery[i] = recovery[i];
        WriteIds(bytes, RecoveryOffset, setupRecovery);
        var supportIds = new int[GenieSetup.SupportCells];
        ushort partyMask = 0;
        for (int i = 0; i < GenieSetup.SupportCells && i < support.Count; i++)
        {
            supportIds[i] = support[i].Id;
            if (support[i].Party) partyMask |= (ushort)(1 << i);
        }
        WriteIds(bytes, SupportOffset, supportIds.AsSpan(0, GroupCells));
        WriteIds(bytes, SupportTailOffset, supportIds.AsSpan(GroupCells));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(SupportPartyOffset, sizeof(ushort)), partyMask);
        bytes[PetPercentOffset] = DefaultPetPercent;
        bytes[OtherHighOffset] = (byte)((ushort)GenieOther.DarkKnightMace >> 8);
        bytes[LayoutOffset] = CurrentLayout;
    }

    public static int[] ReadIds(byte[] bytes, int offset, int count)
    {
        var ids = new int[count];
        for (int i = 0; i < count; i++)
            ids[i] = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset + i * SkillIdBytes, SkillIdBytes));
        return ids;
    }

    public static void WriteIds(byte[] bytes, int offset, ReadOnlySpan<int> ids)
    {
        for (int i = 0; i < ids.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset + i * SkillIdBytes, SkillIdBytes), ids[i]);
    }
}
