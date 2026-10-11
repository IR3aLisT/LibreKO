namespace LibreKO.Domain;

public enum ItemUse { Trade, NpcSell, Warehouse, VipVault, ClanBank }

public static class ItemUseRules
{
    public const int Allowed = 0;
    public const int CannotTradeText = 7701;
    public const int SealedNoTradeText = 15102;
    public const int BoundNoTradeText = 19016;
    public const int NonTradableText = 18200;
    public const int NonStorableText = WarehouseRules.NonStorableText;

    private const int RaceQuest = ItemData.QuestItemRace;
    private const int RaceNoTradeSellDestroy = 50;
    private const int RaceNoTradeDestroy = 70;
    private const int RaceNoTradeDestroyAlt = 71;
    private const int RaceNoTradeSellStore = 73;
    private const int RaceOneTimeTrade = 74;
    private const int RaceNoTradeSellDestroyAlt = 76;
    private const int RaceNoSale = 12;
    private const byte TradeLockedStateFirst = 16;
    private const byte TradeLockedStateLast = 18;

    public static int Refusal(ItemUse use, int itemId, int race, byte state, bool timed = false) => use switch
    {
        ItemUse.Trade => TradeRefusal(itemId, race, state, timed),
        ItemUse.NpcSell => SellRefusal(itemId, race, state, timed),
        ItemUse.ClanBank => !WarehouseRules.Storable(itemId, race) || HeldBack(state) || timed
            ? NonStorableText
            : Allowed,
        _ => WarehouseRules.Storable(itemId, race) ? Allowed : NonStorableText,
    };

    private static bool HeldBack(byte state) => state is (byte)ItemFlag.Rented or (byte)ItemFlag.CharacterSeal
        or (byte)ItemFlag.Duplicate or (byte)ItemFlag.Sealed or (byte)ItemFlag.Bound;

    private static int TradeRefusal(int itemId, int race, byte state, bool timed)
    {
        if (ItemData.IsNoTradeId(itemId)
            || race is RaceQuest or RaceNoTradeDestroy or RaceNoTradeDestroyAlt or RaceNoTradeSellStore
            || state is >= TradeLockedStateFirst and <= TradeLockedStateLast)
            return CannotTradeText;
        return state switch
        {
            (byte)ItemFlag.Sealed => SealedNoTradeText,
            (byte)ItemFlag.Bound => BoundNoTradeText,
            _ => HeldBack(state) || timed ? CannotTradeText : Allowed,
        };
    }

    private static int SellRefusal(int itemId, int race, byte state, bool timed) =>
        ItemData.IsNoTradeId(itemId) || HeldBack(state) || timed
        || race is RaceNoSale or RaceQuest or RaceNoTradeSellDestroy or RaceNoTradeDestroy or RaceNoTradeDestroyAlt
            or RaceNoTradeSellStore or RaceOneTimeTrade or RaceNoTradeSellDestroyAlt
            ? NonTradableText
            : Allowed;
}
