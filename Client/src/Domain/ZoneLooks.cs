using System;

namespace LibreKO.Domain;

public static class ZoneLooks
{
    public const int Prison = 92;
    public const int DelosBasement = 35;
    public const int DungeonDefence = 89;
    public const int ElMoradDeitySkill = 478019;
    public const int KarusDeitySkill = 478020;
    public const int PathosSkill = 490360;
    public const int ElMoradDeityWeapon = 191304511;
    public const int KarusDeityWeapon = 191402522;
    public const float ElMoradDeityScale = 1.4f;
    public const float KarusDeityScale = 1.2f;
    public const int KarusKurian = 6;
    public const int ElMoradKurian = 14;
    public const int KarusDevilModel = 20001;
    public const int ElMoradDevilModel = 20002;
    public const float DevilScale = 1.1f;
    public const float DevilStartScale = 0.4f;
    public const double DevilGrowSeconds = 1.0;
    private static readonly int[] PathosZones = { 37, 38, 39, 76 };
    private static readonly int[] DeityZones = { 45, 85 };

    public static bool HidesOtherPlayers(int zone) => zone == Prison;

    public static bool IsPathosZone(int zone) => Array.IndexOf(PathosZones, zone) >= 0;

    public static bool IsDeityZone(int zone) => Array.IndexOf(DeityZones, zone) >= 0;

    public static bool MutesChatGestures(int zone) => zone == DungeonDefence || IsDeityZone(zone);

    public static bool KeepsNamesOutOfChat(int zone) => IsDeityZone(zone);

    private static bool WearsOwnDeity(int zone) => zone is DelosBasement or DungeonDefence || IsDeityZone(zone);

    public static int DeityOf(int nation) => nation == Nations.ElMorad ? ElMoradDeitySkill : KarusDeitySkill;

    private static int Opposite(int nation) => nation == Nations.ElMorad ? Nations.Karus : Nations.ElMorad;

    public static int SelfLook(int zone, int selfNation) =>
        IsPathosZone(zone) ? PathosSkill : WearsOwnDeity(zone) ? DeityOf(selfNation) : 0;

    public static int OtherLook(int zone, int selfNation, int otherNation)
    {
        if (IsPathosZone(zone)) return PathosSkill;
        if (zone == DungeonDefence) return DeityOf(otherNation);
        if (zone == DelosBasement || IsDeityZone(zone)) return DeityOf(Opposite(selfNation));
        return 0;
    }

    public static float LookScale(int zone, int selfNation, int skill)
    {
        if (zone == DelosBasement) return selfNation == Nations.Karus ? KarusDeityScale : ElMoradDeityScale;
        return skill switch
        {
            ElMoradDeitySkill => ElMoradDeityScale,
            KarusDeitySkill => KarusDeityScale,
            _ => 0f,
        };
    }

    public static int SelfWeapon(int zone, int selfNation) =>
        zone == DungeonDefence || IsDeityZone(zone) ? DeityWeaponOf(selfNation) : 0;

    public static int OtherWeapon(int zone, int selfNation, int otherNation)
    {
        if (zone == DungeonDefence) return DeityWeaponOf(otherNation);
        if (IsDeityZone(zone)) return DeityWeaponOf(Opposite(selfNation));
        return 0;
    }

    private static int DeityWeaponOf(int nation) => nation == Nations.ElMorad ? ElMoradDeityWeapon : KarusDeityWeapon;

    public static int SelfNameText(int zone, int selfNation) =>
        zone == DungeonDefence || IsDeityZone(zone) ? DeityNameOf(selfNation) : 0;

    public static int OtherNameText(int zone, int selfNation, bool selfIsGm, bool selfInParty, bool otherInMyParty)
    {
        if (IsDeityZone(zone)) return DeityNameOf(Opposite(selfNation));
        if (IsPathosZone(zone) && !selfIsGm && selfInParty && !otherInMyParty) return ChatSender.PathoistText;
        return 0;
    }

    private static int DeityNameOf(int nation) => nation == Nations.ElMorad ? ChatSender.ElMoradDeityText : ChatSender.KarusDeityText;

    public static int DevilModel(int race) => race switch
    {
        KarusKurian => KarusDevilModel,
        ElMoradKurian => ElMoradDevilModel,
        _ => 0,
    };
}
