using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibreKO.Common.Domain.Entities.GameData;

public class ItemSmashClassData
{
    public int Id { get; set; }
    public int GradeClass { get; set; }
    public int Band { get; set; }
    public int Rolls { get; set; }

    internal class EntityConfiguration : IEntityTypeConfiguration<ItemSmashClassData>
    {
        public void Configure(EntityTypeBuilder<ItemSmashClassData> builder)
        {
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).ValueGeneratedNever();
        }
    }
}
