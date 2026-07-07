using Microsoft.EntityFrameworkCore;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Domain.Entities;

namespace QuizArena.Persistence.Context;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<QuizSet> QuizSets => Set<QuizSet>();
    public DbSet<Player> Players => Set<Player>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}