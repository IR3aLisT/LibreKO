using System;
using System.Collections.Generic;
using LibreKO.Domain;

namespace LibreKO.Network;

public partial class Net
{
    public const byte UserInformationSub = 2;
    public const byte EquipmentViewSub = 6;

    public const int InspectWornSlots =
        InventoryConstants.SlotMax + InventoryConstants.CospreWireMax;

    public enum EquipmentViewResult : short
    {
        Accepted = 0,
        NoSuchUser = -1,
        CannotChooseYourself = -2,
        NotInSameRegion = -3,
        NoViewEquipmentItem = -4,
    }

    public readonly struct EquipmentView
    {
        public readonly string Name;
        public readonly int Class, Race, Face, Hair, Level, RebirthLevel, Nation;
        public readonly int MaxHp, MaxMp;
        public readonly int Str, StrBonus, Sta, StaBonus, Dex, DexBonus;
        public readonly int Intel, IntelBonus, Magic, MagicBonus;
        public readonly int Attack, Defence;
        public readonly int FireR, IceR, LightningR, MagicR, CurseR, PoisonR;
        public readonly List<(int Slot, int ItemId, short Durability, byte Flag)> Worn;

        public EquipmentView(string name, int cls, int race, int face, int hair,
            int level, int rebirthLevel, int nation, int maxHp, int maxMp,
            int str, int strBonus, int sta, int staBonus, int dex, int dexBonus,
            int intel, int intelBonus, int magic, int magicBonus,
            int attack, int defence,
            int fireR, int iceR, int lightningR, int magicR, int curseR, int poisonR,
            List<(int Slot, int ItemId, short Durability, byte Flag)> worn)
        {
            Name = name; Class = cls; Race = race; Face = face; Hair = hair;
            Level = level; RebirthLevel = rebirthLevel; Nation = nation;
            MaxHp = maxHp; MaxMp = maxMp;
            Str = str; StrBonus = strBonus; Sta = sta; StaBonus = staBonus;
            Dex = dex; DexBonus = dexBonus; Intel = intel; IntelBonus = intelBonus;
            Magic = magic; MagicBonus = magicBonus;
            Attack = attack; Defence = defence;
            FireR = fireR; IceR = iceR; LightningR = lightningR;
            MagicR = magicR; CurseR = curseR; PoisonR = poisonR;
            Worn = worn;
        }
    }

    public event Action<UserInfoReply>? UserInformationEvent;
    public event Action? UserInformationMissingEvent;
    public event Action<EquipmentViewResult, EquipmentView>? EquipmentViewEvent;

    private void HandleUserInfoOpcode(Packet p)
    {
        if (p.RemainingBytes < 1) return;
        byte sub = p.ReadByte();

        switch (sub)
        {
            case UserInformationSub: HandleUserInformation(p); break;
            case EquipmentViewSub:   HandleEquipmentView(p); break;
            default:                 HandleNearbyPlayers(p, sub); break;
        }
    }

    private void HandleUserInformation(Packet p)
    {
        var (result, info) = UserInfoReply.Read(p);
        if (info != null) UserInformationEvent?.Invoke(info);
        else if (result is UserInfoReply.NoUser or UserInfoReply.NoUserAgain) UserInformationMissingEvent?.Invoke();
    }

    private void HandleEquipmentView(Packet p)
    {
        if (p.RemainingBytes < 2) return;
        var result = (EquipmentViewResult)p.ReadShort();
        if (result != EquipmentViewResult.Accepted)
        {
            EquipmentViewEvent?.Invoke(result, default);
            return;
        }

        string name = p.ReadSByteString();
        int cls = p.ReadShort();
        int race = p.ReadByte();
        int face = p.ReadByte();
        int hair = p.ReadInt();
        int level = p.ReadByte();
        int rebirth = p.ReadByte();
        int nation = p.ReadByte();

        int maxHp = p.ReadShort();
        int maxMp = p.ReadShort();
        p.ReadShort();

        int str = p.ReadByte(), strBonus = p.ReadByte();
        int sta = p.ReadByte(), staBonus = p.ReadByte();
        int dex = p.ReadByte(), dexBonus = p.ReadByte();
        int intel = p.ReadByte(), intelBonus = p.ReadByte();
        int magic = p.ReadByte(), magicBonus = p.ReadByte();

        int attack = p.ReadShort(), defence = p.ReadShort();
        int fireR = p.ReadShort(), iceR = p.ReadShort(), lightR = p.ReadShort();
        int magicR = p.ReadShort(), curseR = p.ReadShort(), poisonR = p.ReadShort();

        var worn = new List<(int, int, short, byte)>(InspectWornSlots);
        for (int i = 0; i < InspectWornSlots && p.RemainingBytes >= 11; i++)
        {
            int itemId = p.ReadInt();
            short dura = p.ReadShort();
            byte flag = p.ReadByte();
            p.ReadInt();
            if (itemId != 0)
                worn.Add((InspectWornSlot(i), itemId, dura, flag));
        }

        EquipmentViewEvent?.Invoke(EquipmentViewResult.Accepted, new EquipmentView(
            name, cls, race, face, hair, level, rebirth, nation, maxHp, maxMp,
            str, strBonus, sta, staBonus, dex, dexBonus, intel, intelBonus, magic, magicBonus,
            attack, defence, fireR, iceR, lightR, magicR, curseR, poisonR, worn));
    }

    private static int InspectWornSlot(int wireIndex) =>
        wireIndex < InventoryConstants.SlotMax
            ? wireIndex
            : InventoryConstants.CospreStart
              + InventoryConstants.CospreWirePositions[wireIndex - InventoryConstants.SlotMax];

    public void SendUserInformationRequest(string name) => SendInspectRequest(UserInformationSub, name);

    public void SendEquipmentViewRequest(string name) => SendInspectRequest(EquipmentViewSub, name);

    private void SendInspectRequest(byte sub, string name)
    {
        if (string.IsNullOrEmpty(name)) return;
        var p = new Packet(GameOpcodes.GS_USER_INFO);
        p.WriteByte(sub);
        p.WriteSByteString(name);
        _conn.Send(p);
    }
}
