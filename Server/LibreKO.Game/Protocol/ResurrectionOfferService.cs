using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Domain.Services;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;

namespace LibreKO.Game.Protocol;

public interface IResurrectionOfferService
{
    Task OfferAsync(UserSession caster, UserSession target, int skillId);
    Task AcceptAsync(UserSession target, int casterId);
    void Withdraw(UserSession target, int casterId);
    Task<bool> PayInPlaceAsync(UserSession target);
    Task ReviveAsync(UserSession caster, UserSession target, int skillId, MagicType5Data type5Data);
}

public sealed class ResurrectionOfferService(
    SessionManager sessionManager,
    IGameDataService gameDataService,
    IMagicItemUsageService magicItemUsageService,
    IPlayerProgressionService playerProgressionService,
    ICombatNotificationService combatNotificationService) : IResurrectionOfferService
{
    public const byte ChoiceInPlace = 1;
    public const byte ChoiceTown = 2;
    public const int ResurrectionStone = 379006000;
    public const int ResultApplied = 1;
    public const int ResultOffered = 2;

    private const int OfferSeconds = 60;
    private const int StonesPerLevel = 3;
    private const int InPlaceMinLevelExclusive = 5;
    private const int PercentScale = 100;

    public async Task OfferAsync(UserSession caster, UserSession target, int skillId)
    {
        target.PendingResurrection = new ResurrectionOffer(
            caster.CharacterId, skillId, DateTime.UtcNow.AddSeconds(OfferSeconds).Ticks);

        await target.Client.SendPacket(
            RegenePacketWriter.Offer(target.GetPosX, target.GetPosZ, target.GetPosY, caster.CharacterId, skillId));

        await sessionManager.Regions.SendToRegion(
            caster,
            MagicProcessPacketWriter.Create(
                MagicProcessOpcode.Effecting, skillId, (short)caster.CharacterId, (short)target.CharacterId,
                [0, ResultOffered]),
            excludeSender: false);
    }

    public async Task AcceptAsync(UserSession target, int casterId)
    {
        if (TakeOffer(target, casterId) is not { } offer || target.Hp > 0)
            return;

        var magic = gameDataService.GetMagic(offer.SkillId);
        if (magic == null || !MagicTypeLookup.TryResolve(gameDataService.MagicType5Table, magic, offer.SkillId, out var type5Data))
            return;

        if (magic.UseItem != 0
            && type5Data.NeedStone > 0
            && !await magicItemUsageService.TryConsumeItemAsync(target, magic.UseItem, type5Data.NeedStone))
        {
            await target.Client.SendPacket(RegenePacketWriter.MissingItem(
                target.GetPosX, target.GetPosZ, target.GetPosY, magic.UseItem, (ushort)type5Data.NeedStone));
            return;
        }

        var caster = sessionManager.GetByCharacterId(offer.CasterId) ?? target;
        await ReviveAsync(caster, target, offer.SkillId, type5Data);
    }

    public void Withdraw(UserSession target, int casterId) => TakeOffer(target, casterId);

    public async Task<bool> PayInPlaceAsync(UserSession target)
    {
        var stones = (ushort)(StonesPerLevel * target.Level);
        if (target.Level > InPlaceMinLevelExclusive
            && await magicItemUsageService.TryConsumeItemAsync(target, ResurrectionStone, stones))
            return true;

        await target.Client.SendPacket(RegenePacketWriter.MissingItem(
            target.GetPosX, target.GetPosZ, target.GetPosY, ResurrectionStone, stones));
        return false;
    }

    public async Task ReviveAsync(UserSession caster, UserSession target, int skillId, MagicType5Data type5Data)
    {
        target.PendingResurrection = null;
        target.Hp = target.MaxHp;
        target.Mp = 0;

        var recovered = target.DeathExpLoss > 0 && type5Data.ExpRecover > 0
            ? target.DeathExpLoss * type5Data.ExpRecover / PercentScale
            : 0;
        target.DeathExpLoss = 0;
        if (recovered > 0)
            await playerProgressionService.ChangeExperienceAsync(target, recovered);

        await combatNotificationService.SendHpChangeAsync(target);
        await combatNotificationService.SendMspChangeAsync(target);

        await sessionManager.Regions.SendToRegion(
            target,
            MagicProcessPacketWriter.Create(
                MagicProcessOpcode.Effecting, skillId, (short)caster.CharacterId, (short)target.CharacterId,
                [0, ResultApplied]),
            excludeSender: false);
    }

    private static ResurrectionOffer? TakeOffer(UserSession target, int casterId)
    {
        if (target.PendingResurrection is not { } offer || offer.CasterId != casterId)
            return null;
        target.PendingResurrection = null;
        return offer.ExpiresTicks >= DateTime.UtcNow.Ticks ? offer : null;
    }
}
