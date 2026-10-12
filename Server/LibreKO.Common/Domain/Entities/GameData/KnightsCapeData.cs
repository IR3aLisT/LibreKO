using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibreKO.Common.Domain.Entities.GameData;

public class KnightsCapeData
{
    public short CapeIndex { get; set; }
    public string Name { get; set; } = string.Empty;
    public int BuyPrice { get; set; }
    public int Duration { get; set; }
    public byte Grade { get; set; }
    public int BuyLoyalty { get; set; }
    public byte Ranking { get; set; }
    public byte Type { get; set; }
    public byte Ticket { get; set; }

    internal class EntityConfiguration : IEntityTypeConfiguration<KnightsCapeData>
    {
        public void Configure(EntityTypeBuilder<KnightsCapeData> builder)
        {
            builder.HasKey(p => p.CapeIndex);
            builder.Property(p => p.CapeIndex).ValueGeneratedNever();
        }
    }
}
