using LibreKO.Common.Domain.Entities;
using LibreKO.Common.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace LibreKO.Common.Infrastructure.Persistence;

public class RankerStatueNoteRepository(AppDbContext context) : IRankerStatueNoteRepository
{
    public Task<RankerStatueNote?> GetByCharacterId(int characterId) =>
        context.RankerStatueNotes.SingleOrDefaultAsync(note => note.CharacterId == characterId);

    public async Task SaveAsync(RankerStatueNote note)
    {
        if (await context.RankerStatueNotes.AnyAsync(entry => entry.CharacterId == note.CharacterId))
            context.RankerStatueNotes.Update(note);
        else
            context.RankerStatueNotes.Add(note);
        await context.SaveChangesAsync();
    }
}
