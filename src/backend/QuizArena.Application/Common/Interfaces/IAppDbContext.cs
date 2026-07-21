using Microsoft.EntityFrameworkCore;
using QuizArena.Domain.Entities;

namespace QuizArena.Application.Common.Interfaces;

public interface IAppDbContext : IDisposable, IAsyncDisposable
{
    DbSet<QuizSet> QuizSets { get; }
    DbSet<Player> Players { get; }
    DbSet<GameHistoryEntry> GameHistory { get; }
    
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}