using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibreKO.Common.Domain.Entities;

public class RankerStatueNote
{
    public const int MessageMaxLength = 200;

    public int CharacterId { get; set; }
    public string Message { get; set; } = string.Empty;
    public int LookChangedAt { get; set; }

    internal class EntityConfiguration : IEntityTypeConfiguration<RankerStatueNote>
    {
        public void Configure(EntityTypeBuilder<RankerStatueNote> builder)
        {
            builder.HasKey(p => p.CharacterId);
            builder.Property(p => p.CharacterId).ValueGeneratedNever();
            builder.Property(p => p.Message).HasMaxLength(MessageMaxLength);
        }
    }
}
