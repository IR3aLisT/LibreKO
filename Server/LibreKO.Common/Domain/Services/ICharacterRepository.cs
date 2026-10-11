using LibreKO.Common.Domain.Entities;
using LibreKO.Common.Enums;

namespace LibreKO.Common.Domain.Services;

public interface ICharacterRepository
{
    Task<Character?> GetById(int id);
    Task<IEnumerable<Character>> GetCharactersByAccount(int accountId);
    Task<IReadOnlyList<Character>> GetByIds(IReadOnlyCollection<int> ids);
    Task CreateAsync(Character character);
    Task UpdateAsync(Character character);

    Task<int> UpdateQuestDataAsync(int characterId, byte[] questData);

    Task<bool> IsNameTaken(string name);
    Task<Character?> GetByName(string name);

    Task<IReadOnlyList<int>> GetPlayerIdsByLoyalty(AccountNation nation, int count, bool monthly);

    Task<int> ResetDailyLoyaltyAll();
}
