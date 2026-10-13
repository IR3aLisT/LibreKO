using System;
using System.Buffers.Binary;

namespace LibreKO.Domain;

[Flags]
public enum GenieMode : byte
{
    None = 0,
    Attack = 1,
    Support = 2,
    Recovery = 4,
    BasicAttack = 8,
    LeaderTarget = 16,
    Combo = 32,
    HpPotion = 64,
    MpPotion = 128,
}

[Flags]
public enum GenieMoreMode : byte
{
    None = 0,
    PetPotion = 1,
}

[Flags]
public enum GenieExtra : byte
{
    None = 0,
    StayInPlace = 1,
    HideRange = 2,
    CureCurse = 4,
    HuntInPlace = 8,
}

public sealed class GenieSetup
{
    public const int AttackCells = GenieOptionsBlob.GroupCells;
    public const int RecoveryCells = GenieOptionsBlob.GroupCells;
    public const int SupportCells = 12;
    public const int MinPercent = 1;
    public const int MaxPercent = 99;
    public const int MinRange = 3;
    public const int MaxRange = 80;
    public const int MinHammerPercent = 1;
    public const int MaxHammerPercent = 50;
    public const int DefaultHpPercent = 70;
    public const int DefaultMpPercent = 35;
    public const int DefaultRange = 25;
    public const int DefaultHammerPercent = 10;
    public const GenieMode DefaultModes =
        GenieMode.Attack | GenieMode.Support | GenieMode.BasicAttack | GenieMode.HpPotion | GenieMode.MpPotion;

    public GenieMode Modes = DefaultModes;
    public GenieMoreMode MoreModes;
    public GenieExtra Extras;
    public GenieOther Other;
    public int HpPercent = DefaultHpPercent;
    public int MpPercent = DefaultMpPercent;
    public int RecoveryPercent = GenieOptionsBlob.DefaultRecoveryPercent;
    public int PetPercent = GenieOptionsBlob.DefaultPetPercent;
    public int Range = DefaultRange;
    public int TownHpCount = GenieTownReturn.DefaultCount;
    public int TownMpCount = GenieTownReturn.DefaultCount;
    public int HammerPercent = DefaultHammerPercent;
    public readonly int[] Attack = new int[AttackCells];
    public readonly int[] Recovery = new int[RecoveryCells];
    public readonly int[] Support = new int[SupportCells];
    public readonly bool[] SupportParty = new bool[SupportCells];

    public bool Has(GenieMode mode) => (Modes & mode) != 0;
    public bool Has(GenieMoreMode mode) => (MoreModes & mode) != 0;
    public bool Has(GenieExtra extra) => (Extras & extra) != 0;
    public bool Has(GenieOther other) => (Other & other) != 0;

    public void Set(GenieMode mode, bool on) => Modes = on ? Modes | mode : Modes & ~mode;
    public void Set(GenieMoreMode mode, bool on) => MoreModes = on ? MoreModes | mode : MoreModes & ~mode;
    public void Set(GenieExtra extra, bool on) => Extras = on ? Extras | extra : Extras & ~extra;
    public void Set(GenieOther other, bool on) => Other = on ? Other | other : Other & ~other;

    public int[] Cells(GenieCellKind kind) => kind switch
    {
        GenieCellKind.Attack => Attack,
        GenieCellKind.Recovery => Recovery,
        _ => Support,
    };

    public byte[] ToBytes()
    {
        var bytes = new byte[GenieOptionsBlob.Size];
        bytes[GenieOptionsBlob.LayoutOffset] = GenieOptionsBlob.CurrentLayout;
        bytes[GenieOptionsBlob.ModesOffset] = (byte)Modes;
        bytes[GenieOptionsBlob.HpPercentOffset] = (byte)Math.Clamp(HpPercent, MinPercent, MaxPercent);
        bytes[GenieOptionsBlob.MpPercentOffset] = (byte)Math.Clamp(MpPercent, MinPercent, MaxPercent);
        bytes[GenieOptionsBlob.RangeOffset] = (byte)Math.Clamp(Range, MinRange, MaxRange);
        GenieOptionsBlob.WriteIds(bytes, GenieOptionsBlob.AttackOffset, Attack);
        GenieOptionsBlob.WriteIds(bytes, GenieOptionsBlob.RecoveryOffset, Recovery);
        GenieOptionsBlob.WriteIds(bytes, GenieOptionsBlob.SupportOffset, Support.AsSpan(0, GenieOptionsBlob.GroupCells));
        GenieOptionsBlob.WriteIds(bytes, GenieOptionsBlob.SupportTailOffset, Support.AsSpan(GenieOptionsBlob.GroupCells));
        bytes[GenieOptionsBlob.ExtrasOffset] = (byte)Extras;
        bytes[GenieOptionsBlob.OtherOffset] = (byte)((ushort)Other & 0xFF);
        bytes[GenieOptionsBlob.OtherHighOffset] = (byte)((ushort)Other >> 8);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(GenieOptionsBlob.TownHpCountOffset, sizeof(ushort)), (ushort)TownHpCount);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(GenieOptionsBlob.TownMpCountOffset, sizeof(ushort)), (ushort)TownMpCount);
        bytes[GenieOptionsBlob.RecoveryPercentOffset] = (byte)Math.Clamp(RecoveryPercent, MinPercent, MaxPercent);
        bytes[GenieOptionsBlob.MoreModesOffset] = (byte)MoreModes;
        bytes[GenieOptionsBlob.PetPercentOffset] = (byte)Math.Clamp(PetPercent, MinPercent, MaxPercent);
        ushort party = 0;
        for (int i = 0; i < SupportCells; i++) if (SupportParty[i]) party |= (ushort)(1 << i);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(GenieOptionsBlob.SupportPartyOffset, sizeof(ushort)), party);
        bytes[GenieOptionsBlob.HammerPercentOffset] = (byte)Math.Clamp(HammerPercent, MinHammerPercent, MaxHammerPercent);
        return bytes;
    }

    public static GenieSetup? FromBytes(byte[] saved, Func<int, GenieCellKind> kindOf)
    {
        if (GenieOptionsBlob.Upgrade(saved, GenieOptionsBlob.Size, kindOf) is not { } bytes) return null;
        var setup = new GenieSetup
        {
            Modes = (GenieMode)bytes[GenieOptionsBlob.ModesOffset],
            HpPercent = StoredPercent(bytes[GenieOptionsBlob.HpPercentOffset], DefaultHpPercent),
            MpPercent = StoredPercent(bytes[GenieOptionsBlob.MpPercentOffset], DefaultMpPercent),
            Range = bytes[GenieOptionsBlob.RangeOffset] is >= MinRange and <= MaxRange ? bytes[GenieOptionsBlob.RangeOffset] : DefaultRange,
            Extras = (GenieExtra)bytes[GenieOptionsBlob.ExtrasOffset],
            Other = (GenieOther)(bytes[GenieOptionsBlob.OtherOffset] | bytes[GenieOptionsBlob.OtherHighOffset] << 8),
            TownHpCount = GenieTownReturn.StoredOrDefault(
                BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(GenieOptionsBlob.TownHpCountOffset, sizeof(ushort)))),
            TownMpCount = GenieTownReturn.StoredOrDefault(
                BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(GenieOptionsBlob.TownMpCountOffset, sizeof(ushort)))),
            RecoveryPercent = StoredPercent(bytes[GenieOptionsBlob.RecoveryPercentOffset], GenieOptionsBlob.DefaultRecoveryPercent),
            MoreModes = (GenieMoreMode)bytes[GenieOptionsBlob.MoreModesOffset],
            PetPercent = StoredPercent(bytes[GenieOptionsBlob.PetPercentOffset], GenieOptionsBlob.DefaultPetPercent),
            HammerPercent = bytes[GenieOptionsBlob.HammerPercentOffset] is >= MinHammerPercent and <= MaxHammerPercent
                ? bytes[GenieOptionsBlob.HammerPercentOffset] : DefaultHammerPercent,
        };
        GenieOptionsBlob.ReadIds(bytes, GenieOptionsBlob.AttackOffset, AttackCells).CopyTo(setup.Attack, 0);
        GenieOptionsBlob.ReadIds(bytes, GenieOptionsBlob.RecoveryOffset, RecoveryCells).CopyTo(setup.Recovery, 0);
        GenieOptionsBlob.ReadIds(bytes, GenieOptionsBlob.SupportOffset, GenieOptionsBlob.GroupCells).CopyTo(setup.Support, 0);
        GenieOptionsBlob.ReadIds(bytes, GenieOptionsBlob.SupportTailOffset, SupportCells - GenieOptionsBlob.GroupCells)
            .CopyTo(setup.Support, GenieOptionsBlob.GroupCells);
        ushort party = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(GenieOptionsBlob.SupportPartyOffset, sizeof(ushort)));
        for (int i = 0; i < SupportCells; i++) setup.SupportParty[i] = (party & (1 << i)) != 0;
        return setup;
    }

    public GenieSetup Clone() => FromBytes(ToBytes(), _ => GenieCellKind.Support)!;

    private static int StoredPercent(byte stored, int fallback) => stored is >= MinPercent and <= MaxPercent ? stored : fallback;
}
