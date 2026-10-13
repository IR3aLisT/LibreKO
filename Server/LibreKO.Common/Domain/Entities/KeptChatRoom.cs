using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibreKO.Common.Domain.Entities;

public class KeptChatRoom
{
    public const int NameMaxLength = 64;
    public const int PasswordMaxLength = 16;

    public string RoomKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int MaxMembers { get; set; }
    public int OwnerId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastUsedAt { get; set; }

    internal class EntityConfiguration : IEntityTypeConfiguration<KeptChatRoom>
    {
        public void Configure(EntityTypeBuilder<KeptChatRoom> builder)
        {
            builder.HasKey(p => p.RoomKey);
            builder.Property(p => p.RoomKey).HasMaxLength(ChatRoomMessage.RoomKeyMaxLength);
            builder.Property(p => p.Name).HasMaxLength(NameMaxLength);
            builder.Property(p => p.Password).HasMaxLength(PasswordMaxLength);
            builder.HasIndex(p => p.OwnerId);
        }
    }
}
