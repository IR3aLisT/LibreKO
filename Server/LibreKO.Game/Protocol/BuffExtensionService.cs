using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Domain.Services;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;
using Microsoft.Extensions.Logging;

namespace LibreKO.Game.Protocol;

public interface IBuffExtensionService
{
    Task<bool> ExtendAsync(UserSession session, int skillId);
}

public sealed class BuffExtensionService(
    IGameDataService gameDataService,
    IMagicItemUsageService magicItemUsageService,
    ILogger<BuffExtensionService> logger) : IBuffExtensionService
{
    public const byte ExtendDurationMoral = 240;
    public const byte DurationItemKind = 255;
    public const int ItemSkillFloor = 500000;

    public async Task<bool> ExtendAsync(UserSession session, int skillId)
    {
        if (skillId > ItemSkillFloor
            || !session.ActiveBuffs.TryGetValue(skillId, out var buff)
            || buff.IsExpired
            || buff.DurationExtended)
            return false;
        var magic = gameDataService.GetMagic(skillId);
        if (magic == null
            || !MagicTypeLookup.TryResolve(gameDataService.MagicType4Table, magic, skillId, out var type4)
            || !MagicBuffClassifier.IsBuff(type4))
            return false;
        if (DurationItemOf(session) is not { } itemId || !await magicItemUsageService.TryConsumeItemAsync(session, itemId))
            return false;

        buff.ExpireTicks += TimeSpan.FromSeconds(buff.Duration).Ticks;
        buff.DurationExtended = true;
        await session.Client.SendPacket(MagicProcessPacketWriter.CreateExtendDuration(skillId));
        logger.LogInformation("{Name} doubled buff {SkillId} with Continuance Item {ItemId}", session.Name, skillId, itemId);
        return true;
    }

    private int? DurationItemOf(UserSession session)
    {
        for (var index = InventoryConstants.InventoryStart; index < session.Inventory.Length; index++)
        {
            int itemId = session.Inventory[index].ItemId;
            if (itemId == 0 || gameDataService.GetItem(itemId) is not { } item) continue;
            if (item.Kind == DurationItemKind && item.Effect1 != 0
                && gameDataService.GetMagic(item.Effect1)?.Moral == ExtendDurationMoral)
                return itemId;
        }
        return null;
    }
}
