using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibreKO.Common.Domain.Entities;

public class ChatRoomReaction
{
    public const int CharacterNameMaxLength = ChatRoomMessage.SenderNameMaxLength;
    public const int EmojiMaxLength = 32;

    public long Id { get; set; }
    public long MessageId { get; set; }
    public int CharacterId { get; set; }
    public string CharacterName { get; set; } = string.Empty;
    public string Emoji { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    internal class EntityConfiguration : IEntityTypeConfiguration<ChatRoomReaction>
    {
        public void Configure(EntityTypeBuilder<ChatRoomReaction> builder)
        {
            builder.HasKey(p => p.Id);
            builder.Property(p => p.CharacterName).HasMaxLength(CharacterNameMaxLength);
            builder.Property(p => p.Emoji).HasMaxLength(EmojiMaxLength);
            builder.HasIndex(p => new { p.MessageId, p.CharacterId }).IsUnique();
            builder.HasOne<ChatRoomMessage>().WithMany().HasForeignKey(p => p.MessageId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
