using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibreKO.Common.Domain.Entities.GameData;

public class MapObjectData
{
    public int Id { get; set; }
    public short ZoneId { get; set; }
    public short Index { get; set; }
    public short Type { get; set; }
    public short ControlNpcId { get; set; }
    public short Status { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public int Belong { get; set; }
    public byte Life { get; set; }

    internal class EntityConfiguration : IEntityTypeConfiguration<MapObjectData>
    {
        public void Configure(EntityTypeBuilder<MapObjectData> builder)
        {
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).ValueGeneratedNever();
        }
    }
}
