using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;

namespace LibreKO.Game.Protocol;

public interface IUserOptionPacketCoordinator
{
    Task HandleAsync(IClient client, Packet packet);
}

public class UserOptionPacketCoordinator(SessionManager sessionManager) : IUserOptionPacketCoordinator
{
    public async Task HandleAsync(IClient client, Packet packet)
    {
        var session = sessionManager.GetByClientId(client.Id);
        if (session == null || packet.RemainingBytes < 2)
            return;
        if (packet.ReadByte() != UserOptionPacketWriter.SubRankAura)
            return;

        bool hidden = packet.ReadByte() == 0;
        if (session.RankAuraHidden == hidden)
            return;
        session.RankAuraHidden = hidden;
        await sessionManager.Regions.SendToRegion(session, UserOptionPacketWriter.RankAura(session.CharacterId, !hidden));
    }
}
