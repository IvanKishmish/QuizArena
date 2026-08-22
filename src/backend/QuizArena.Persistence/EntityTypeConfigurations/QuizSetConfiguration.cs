using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuizArena.Domain.Entities;

namespace QuizArena.Persistence.EntityTypeConfigurations;

public sealed class QuizSetConfiguration : IEntityTypeConfiguration<QuizSet>
{
    public void Configure(EntityTypeBuilder<QuizSet> builder)
    {
        builder.ToTable("QuizSets");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(x => x.Visibility)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.OwnerId)
            .IsRequired();

        // Questions live in MongoDB as their own aggregate (QuestionDocument, keyed by QuizSetId) — QuizSet
        // no longer carries a Questions collection at all, so there's nothing here to Ignore() anymore.

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.CreatedBy).IsRequired();
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.UpdatedBy);

        builder.HasIndex(x => x.OwnerId);
    }
}