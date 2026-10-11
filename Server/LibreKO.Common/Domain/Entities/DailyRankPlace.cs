using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibreKO.Common.Domain.Entities;

public class DailyRankPlace
{
    public byte Board { get; set; }
    public int SubjectId { get; set; }
    public int Place { get; set; }
    public int PreviousPlace { get; set; }
    public DateOnly RankedOn { get; set; }

    internal class EntityConfiguration : IEntityTypeConfiguration<DailyRankPlace>
    {
        public void Configure(EntityTypeBuilder<DailyRankPlace> builder)
        {
            builder.HasKey(p => new { p.Board, p.SubjectId });
        }
    }
}
