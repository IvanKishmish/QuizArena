namespace QuizArena.Application.Common.Interfaces.IdentityQueryService;

public interface IIdentityUserQueryService
{
    Task<(IReadOnlyList<IdentityUserSummary> Users, int TotalCount)> GetPagedUsersAsync(int pageNumber,
        int pageSize, CancellationToken ct = default);
}