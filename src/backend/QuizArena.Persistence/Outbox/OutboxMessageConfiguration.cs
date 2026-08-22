using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace QuizArena.Persistence.Outbox;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.Type)
            .IsRequired()
            .HasMaxLength(400);

        builder.Property(x => x.Payload)
            .IsRequired();

        builder.Property(x => x.LastError)
            .HasMaxLength(2000);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.NextAttemptAt).IsRequired();

        // The exact query OutboxProcessorService polls with: unprocessed rows, due now, ordered oldest first.
        builder.HasIndex(x => new { x.ProcessedAt, x.NextAttemptAt });
    }
}
