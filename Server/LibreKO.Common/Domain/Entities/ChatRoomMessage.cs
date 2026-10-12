using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibreKO.Common.Domain.Entities;

public enum ChatRoomMessageKind : byte
{
    Text = 0,
    VoiceNote = 1,
}

public class ChatRoomMessage
{
    public const int RoomKeyMaxLength = 40;
    public const int SenderNameMaxLength = 50;
    public const int TextMaxLength = 256;
    public const int StoragePathMaxLength = 128;
    public const int Sha256HexLength = 64;
    public const int WaveformMaxSamples = 64;

    public long Id { get; set; }
    public string RoomKey { get; set; } = string.Empty;
    public ChatRoomMessageKind Kind { get; set; }
    public int SenderCharacterId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? Text { get; set; }
    public int NoteDurationMs { get; set; }
    public int NoteSizeBytes { get; set; }
    public string? NoteSha256 { get; set; }
    public string? NoteStoragePath { get; set; }
    public byte[]? NoteWaveform { get; set; }

    internal class EntityConfiguration : IEntityTypeConfiguration<ChatRoomMessage>
    {
        public void Configure(EntityTypeBuilder<ChatRoomMessage> builder)
        {
            builder.HasKey(p => p.Id);
            builder.Property(p => p.RoomKey).HasMaxLength(RoomKeyMaxLength);
            builder.Property(p => p.SenderName).HasMaxLength(SenderNameMaxLength);
            builder.Property(p => p.Text).HasMaxLength(TextMaxLength);
            builder.Property(p => p.NoteSha256).HasMaxLength(Sha256HexLength);
            builder.Property(p => p.NoteStoragePath).HasMaxLength(StoragePathMaxLength);
            builder.Property(p => p.NoteWaveform).HasMaxLength(WaveformMaxSamples);
            builder.HasIndex(p => new { p.RoomKey, p.Id });
            builder.HasIndex(p => p.CreatedAt);
        }
    }
}
