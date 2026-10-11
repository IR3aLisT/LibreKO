using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol.Writers;

namespace LibreKO.Game.Protocol;

public static class KnightsBroadcastBuilders
{
    public const byte ClanPremiumQuery = 1;
    public const byte ClanPremiumStatusChange = 2;
    public const byte ClanPremiumInactive = 0;
    public const byte ClanPremiumActive = 1;

    public static Packet BuildClanPremium(byte subOpcode, Common.Domain.Entities.KnightsEntity? clan) =>
        ClanBroadcastPacketWriter.ClanPremium(subOpcode,
            clan?.HasPremium == true ? ClanPremiumActive : ClanPremiumInactive, PremiumSecondsLeft(clan));

    public static uint PremiumSecondsLeft(Common.Domain.Entities.KnightsEntity? clan)
    {
        if (clan?.PremiumExpiry is not { } end) return 0;
        var left = end - DateTime.UtcNow;
        return left <= TimeSpan.Zero ? 0u : (uint)Math.Min(uint.MaxValue, left.TotalSeconds);
    }

    public static Packet BuildClanBattleNotification() =>
        ClanBroadcastPacketWriter.ClanBattleNotification();
}
