using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibreKO.Common.Domain.Entities.GameData;

public class ItemGradeBonusData
{
    public const int ExtensionSpan = 1000;

    public int Id { get; set; }
    public int ExtFile { get; set; }
    public int ExtensionId { get; set; }
    public int Bonus { get; set; }

    public static int KeyOf(int extFile, int extensionId) => extFile * ExtensionSpan + extensionId;

    internal class EntityConfiguration : IEntityTypeConfiguration<ItemGradeBonusData>
    {
        public void Configure(EntityTypeBuilder<ItemGradeBonusData> builder)
        {
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).ValueGeneratedNever();
        }
    }
}
