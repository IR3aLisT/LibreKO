using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol.Writers;

namespace LibreKO.Game.World;

public static class DevilForms
{
    public static bool Active(UserSession session) =>
        session.ActiveBuffs.Values.Any(buff => buff.BuffType == BuffType.DevilTransform && !buff.IsExpired);

    public static Packet Announcement(UserSession session) =>
        MovementPacketWriter.StateChange(session.CharacterId, (byte)StateChangeType.DevilForm, session.DevilForm ? 1 : 0);

    public static Packet? Sync(UserSession session)
    {
        var active = Active(session);
        if (active == session.DevilForm)
            return null;
        session.DevilForm = active;
        return Announcement(session);
    }
}
