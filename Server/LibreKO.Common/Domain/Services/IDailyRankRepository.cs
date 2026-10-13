using LibreKO.Common.Domain.Entities;

namespace LibreKO.Common.Domain.Services;

public readonly record struct DrakiClimber(int CharacterId, string Name, short Class, byte Stage, byte SubStage);

public interface IDailyRankRepository
{
    Task AddMerchantGoldAsync(int characterId, long gold);
    Task AddBlessedUpgradeAsync(int characterId);
    Task ResetCountersAsync();
    Task<IReadOnlyList<int>> GetMerchantOrderAsync();
    Task<IReadOnlyList<int>> GetBlessedUpgradeOrderAsync();
    Task<IReadOnlyList<int>> GetMonthlyLoyaltyOrderAsync();
    Task<IReadOnlyList<DrakiClimber>> GetDrakiClimbersAsync();
    Task<IReadOnlyDictionary<int, string>> GetNamesAsync(IReadOnlyCollection<int> characterIds);
    Task<IReadOnlyList<DailyRankPlace>> GetPlacesAsync();
    Task ReplacePlacesAsync(IReadOnlyList<DailyRankPlace> places);
}
