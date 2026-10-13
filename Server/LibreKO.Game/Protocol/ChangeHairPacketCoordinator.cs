using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;

namespace LibreKO.Game.Protocol;

public interface IChangeHairPacketCoordinator
{
    Task HandleAsync(IClient client, Packet packet);
}

public sealed class ChangeHairPacketCoordinator(
    SessionManager sessionManager,
    IMagicItemUsageService items,
    ICharacterStatePersister persister,
    IWorldVisibilityService visibility) : IChangeHairPacketCoordinator
{
    public const byte InGameSub = 1;
    public const int FaceChangeCoupon = 810109000;
    public const int MakeOverCoupon = 810340000;
    private const int RequestBytes = 1 + 1 + 1 + sizeof(int);

    public async Task HandleAsync(IClient client, Packet packet)
    {
        var session = sessionManager.GetByClientId(client.Id);
        if (session == null || packet.RemainingBytes < RequestBytes)
            return;
        if (packet.ReadByte() != InGameSub)
            return;

        var bagPosition = packet.ReadByte();
        var face = packet.ReadByte();
        var hair = packet.ReadInt();
        var coupon = CouponAt(session, bagPosition);
        if (coupon == 0 || session.Hp <= 0 || !await items.TryConsumeItemAsync(session, coupon))
        {
            await session.Client.SendPacket(PreGamePacketWriter.ChangeHairResult(PreGamePacketWriter.ChangeHairFailed));
            return;
        }

        session.Face = face;
        session.Hair = hair;
        await persister.SaveAsync(session);
        await session.Client.SendPacket(PreGamePacketWriter.ChangeHairApplied());
        await visibility.ResendUserRecordAsync(session);
    }

    public static int CouponAt(UserSession session, byte bagPosition)
    {
        if (bagPosition >= InventoryConstants.HaveMax)
            return 0;
        var slot = session.Inventory[InventoryConstants.SlotMax + bagPosition];
        return slot.Count > 0 && slot.ItemId is FaceChangeCoupon or MakeOverCoupon ? slot.ItemId : 0;
    }
}
