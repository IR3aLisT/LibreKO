using LibreKO.Common.Domain.Entities;
using LibreKO.Common.Domain.Services;
using LibreKO.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace LibreKO.Common.Infrastructure.Persistence;

public class DailyRankRepository(AppDbContext context) : IDailyRankRepository
{
    public async Task AddMerchantGoldAsync(int characterId, long gold)
    {
        var counter = await CounterAsync(characterId);
        counter.MerchantGold += gold;
        await context.SaveChangesAsync();
    }

    public async Task AddBlessedUpgradeAsync(int characterId)
    {
        var counter = await CounterAsync(characterId);
        counter.BlessedUpgrades++;
        await context.SaveChangesAsync();
    }

    private async Task<DailyRankCounter> CounterAsync(int characterId)
    {
        var counter = await context.DailyRankCounters.SingleOrDefaultAsync(c => c.CharacterId == characterId);
        if (counter != null)
            return counter;
        counter = new DailyRankCounter { CharacterId = characterId };
        context.DailyRankCounters.Add(counter);
        return counter;
    }

    public async Task ResetCountersAsync()
    {
        context.DailyRankCounters.RemoveRange(await context.DailyRankCounters.ToListAsync());
        await context.SaveChangesAsync();
    }

    private IQueryable<Character> Players =>
        from ch in context.Characters
        join acc in context.Accounts on ch.AccountId equals acc.Id
        where acc.Authority == AccountAuthority.Normal && ch.DeletionTime == null
        select ch;

    public async Task<IReadOnlyList<int>> GetMerchantOrderAsync() =>
        await (from counter in context.DailyRankCounters
               join ch in Players on counter.CharacterId equals ch.Id
               where counter.MerchantGold > 0
               orderby counter.MerchantGold descending, ch.Id
               select ch.Id).ToListAsync();

    public async Task<IReadOnlyList<int>> GetBlessedUpgradeOrderAsync() =>
        await (from counter in context.DailyRankCounters
               join ch in Players on counter.CharacterId equals ch.Id
               where counter.BlessedUpgrades > 0
               orderby counter.BlessedUpgrades descending, ch.Id
               select ch.Id).ToListAsync();

    public async Task<IReadOnlyList<int>> GetMonthlyLoyaltyOrderAsync() =>
        await Players.Where(ch => ch.LoyaltyMonthly > 0)
            .OrderByDescending(ch => ch.LoyaltyMonthly).ThenByDescending(ch => ch.Loyalty).ThenBy(ch => ch.Id)
            .Select(ch => ch.Id).ToListAsync();

    public async Task<IReadOnlyList<DrakiClimber>> GetDrakiClimbersAsync()
    {
        var rows = await Players.Where(ch => ch.DrakiStage > 0)
            .OrderByDescending(ch => ch.DrakiStage).ThenByDescending(ch => ch.DrakiSubStage).ThenBy(ch => ch.Id)
            .Select(ch => new { ch.Id, ch.Name, ch.Class, ch.DrakiStage, ch.DrakiSubStage })
            .ToListAsync();
        return rows.Select(r => new DrakiClimber(r.Id, r.Name, r.Class, r.DrakiStage, r.DrakiSubStage)).ToList();
    }

    public async Task<IReadOnlyDictionary<int, string>> GetNamesAsync(IReadOnlyCollection<int> characterIds) =>
        await context.Characters.Where(ch => characterIds.Contains(ch.Id))
            .ToDictionaryAsync(ch => ch.Id, ch => ch.Name);

    public async Task<IReadOnlyList<DailyRankPlace>> GetPlacesAsync() =>
        await context.DailyRankPlaces.AsNoTracking().ToListAsync();

    public async Task ReplacePlacesAsync(IReadOnlyList<DailyRankPlace> places)
    {
        context.DailyRankPlaces.RemoveRange(await context.DailyRankPlaces.ToListAsync());
        context.DailyRankPlaces.AddRange(places);
        await context.SaveChangesAsync();
    }
}
