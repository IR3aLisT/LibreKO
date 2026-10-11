using System;

namespace LibreKO.Network;

public partial class Net
{
    public const byte ClanPremiumSubQuery = 1;
    public const byte ClanPremiumSubChange = 2;

    public event Action<bool>? ClanPremiumEvent;

    public bool ClanPremiumActive { get; private set; }
    public bool ClanPremiumKnown { get; private set; }
    public long ClanPremiumEndsAt { get; private set; }

    private void HandleClanPremium(Packet p)
    {
        if (ClanPremiumReply.Read(p) is not { } reply) return;
        ClanPremiumActive = reply.Active;
        ClanPremiumKnown = true;
        ClanPremiumEndsAt = reply.Active && reply.SecondsLeft > 0
            ? DateTimeOffset.UtcNow.ToUnixTimeSeconds() + reply.SecondsLeft
            : 0;
        ClanPremiumEvent?.Invoke(ClanPremiumActive);
    }

    public void SendClanPremiumQuery()
    {
        var p = new Packet(GameOpcodes.GS_CLAN_PREMIUM);
        p.WriteByte(ClanPremiumSubQuery);
        _conn.Send(p);
    }
}
