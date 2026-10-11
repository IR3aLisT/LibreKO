using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibreKO.Common.Domain.Entities;

public class DailyRankCounter
{
    public int CharacterId { get; set; }
    public long MerchantGold { get; set; }
    public int BlessedUpgrades { get; set; }

    internal class EntityConfiguration : IEntityTypeConfiguration<DailyRankCounter>
    {
        public void Configure(EntityTypeBuilder<DailyRankCounter> builder)
        {
            builder.HasKey(p => p.CharacterId);
            builder.Property(p => p.CharacterId).ValueGeneratedNever();
        }
    }
}
