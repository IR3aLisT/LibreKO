using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibreKO.Common.Domain.Entities.GameData;

public class ItemSmashRewardData
{
    public const int BandSpan = 1_000_000;

    public int Id { get; set; }
    public int ItemId { get; set; }
    public int Count { get; set; }
    public int Rate { get; set; }

    public int Band => Id / BandSpan;

    internal class EntityConfiguration : IEntityTypeConfiguration<ItemSmashRewardData>
    {
        public void Configure(EntityTypeBuilder<ItemSmashRewardData> builder)
        {
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).ValueGeneratedNever();
        }
    }
}
