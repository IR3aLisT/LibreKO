using LibreKO.Common.Domain.Entities;

namespace LibreKO.Common.Domain.Services;

public interface IRankerStatueNoteRepository
{
    Task<RankerStatueNote?> GetByCharacterId(int characterId);
    Task SaveAsync(RankerStatueNote note);
}
