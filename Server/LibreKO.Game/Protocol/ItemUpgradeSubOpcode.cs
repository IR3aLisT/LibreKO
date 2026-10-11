namespace LibreKO.Game.Protocol;

public enum ItemUpgradeSubOpcode : byte
{
    AnvilOpen = 1,
    Upgrade = 2,
    UpgradeAccessories = 3,
    BifrostRequest = 4,
    BifrostExchange = 5,
    PetHatching = 6,
    UpgradeRebirth = 7,
    ItemSeal = 8,
    CharacterSeal = 9,
    PetTransform = 10,
    Combine = 11,
    Smash = 13,
}
