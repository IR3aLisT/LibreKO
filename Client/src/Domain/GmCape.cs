namespace LibreKO.Domain;

public static class GmCape
{
    public const int Id = 99;
    public const int None = -1;

    public static int Worn(int capeId, bool gmShown) =>
        gmShown ? Id : capeId == Id ? None : capeId;
}
