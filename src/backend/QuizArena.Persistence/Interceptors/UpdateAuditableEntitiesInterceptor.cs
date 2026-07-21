using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Domain.Common;

namespace QuizArena.Persistence.Interceptors;

public sealed class UpdateAuditableEntitiesInterceptor(ICurrentUserService currentUserService) 
    : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        DbContext? context = eventData.Context;
        if (context is null)
            return base.SavingChangesAsync(eventData, result, cancellationToken);

        var entries = context.ChangeTracker.Entries<Entity>();
        var utcNow = DateTimeOffset.UtcNow;
        var userId = currentUserService.UserId ?? Guid.Empty;

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Property(x => x.CreatedAt).CurrentValue = utcNow;
                entry.Property(x => x.CreatedBy).CurrentValue = userId;
            }
            if (entry.State == EntityState.Modified)
            {
                entry.Property(x => x.UpdatedAt).CurrentValue = utcNow;
                entry.Property(x => x.UpdatedBy).CurrentValue = userId;
            }
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}