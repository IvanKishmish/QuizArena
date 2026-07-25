using Microsoft.EntityFrameworkCore;
using QuizArena.Application.Common.Interfaces.IdentityQueryService;

namespace QuizArena.Persistence.Identity;

public sealed class IdentityUserQueryService(ApplicationIdentityDbContext identityDbContext) : IIdentityUserQueryService
{
    public async Task<(IReadOnlyList<IdentityUserSummary> Users, int TotalCount)>
        GetPagedUsersAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var query = identityDbContext.Users.AsNoTracking();
        
        var totalCount = await query.CountAsync(ct);

        var users = await query
            .OrderByDescending(u => u.RegisteredAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new IdentityUserSummary(
                u.Id,
                u.Email!,
                u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow,
                u.RegisteredAt))
            .ToListAsync(ct);
        
        return (users, totalCount);
    }
}