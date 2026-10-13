namespace LibreKO.Game.Protocol;

public static class VipVault
{
    public const byte Storages = 4;
    public const byte OwnStorage = 0;
    public const int PinLength = 4;

    public const int VaultKey = 800_442_000;
    public const int InnVaultKey = 931_695_000;
    public const int SafeKeyOneDay = 810_442_000;
    public const int SafeKeySevenDays = 998_019_000;

    private const int VaultKeyDays = 30;
    private const int SevenDays = 7;
    private const int OneDay = 1;

    public static int KeyDays(int itemId) => itemId switch
    {
        VaultKey or InnVaultKey => VaultKeyDays,
        SafeKeySevenDays => SevenDays,
        SafeKeyOneDay => OneDay,
        _ => 0,
    };

    public static uint ExpiresAt(DateTime expiry) =>
        expiry <= DateTime.UnixEpoch
            ? 0u
            : (uint)Math.Min(uint.MaxValue, (expiry - DateTime.UnixEpoch).TotalSeconds);

    public static bool ExpiredUnannounced(DateTime expiry, bool announced, long nowUnixSeconds)
    {
        var expiresAt = ExpiresAt(expiry);
        return !announced && expiresAt > 0 && expiresAt <= nowUnixSeconds;
    }
}
