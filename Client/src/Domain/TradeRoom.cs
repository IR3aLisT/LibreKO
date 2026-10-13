namespace LibreKO.Domain;

public readonly record struct TradeRoom(int FreeSlots, int Incoming, long WeightAfter, long MaxWeight)
{
    public bool Fits => Incoming <= FreeSlots && (MaxWeight <= 0 || WeightAfter <= MaxWeight);
}
