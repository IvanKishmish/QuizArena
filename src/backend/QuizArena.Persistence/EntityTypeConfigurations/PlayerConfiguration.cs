using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuizArena.Domain.Entities;

namespace QuizArena.Persistence.EntityTypeConfigurations;

public sealed class PlayerConfiguration : IEntityTypeConfiguration<Player>
{
    public void Configure(EntityTypeBuilder<Player> builder)
    {
        builder.ToTable("Players");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.NickName)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.TotalGamesPlayed)
            .IsRequired();

        builder.Property(x => x.TotalScore)
            .IsRequired();

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.CreatedBy).IsRequired();
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.UpdatedBy);

        builder.HasIndex(x => x.NickName);
    }
}