using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuizArena.Domain.Entities;

namespace QuizArena.Persistence.EntityTypeConfigurations;

public sealed class GameHistoryEntryConfiguration : IEntityTypeConfiguration<GameHistoryEntry>
{
    public void Configure(EntityTypeBuilder<GameHistoryEntry> builder)
    {
        builder.ToTable("GameHistory");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.QuizSetId)
            .IsRequired();

        builder.Property(x => x.ParticipantUserId);

        builder.Property(x => x.DisplayName)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(x => x.FinalScore)
            .IsRequired();

        builder.Property(x => x.Placement)
            .IsRequired();

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.CreatedBy).IsRequired();
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.UpdatedBy);

        builder.HasIndex(x => x.QuizSetId);
        builder.HasIndex(x => x.ParticipantUserId);
    }
}