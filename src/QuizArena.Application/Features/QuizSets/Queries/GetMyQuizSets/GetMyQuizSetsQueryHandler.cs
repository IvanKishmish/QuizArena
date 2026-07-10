using Mediator;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Application.Features.QuizSets.Queries.Common;
using ErrorOr;
using Microsoft.EntityFrameworkCore;

namespace QuizArena.Application.Features.QuizSets.Queries.GetMyQuizSets;

public sealed class GetMyQuizSetsQueryHandler(
    IAppDbContext dbContext,
    ICurrentUserService currentUserService)
: IQueryHandler<GetMyQuizSetsQuery, ErrorOr<IReadOnlyList<QuizSetSummary>>>

{
    public async ValueTask<ErrorOr<IReadOnlyList<QuizSetSummary>>> Handle
        (GetMyQuizSetsQuery request, CancellationToken ct = default)
    {
        if (currentUserService.UserId is null)
            return Error.Unauthorized("Auth.NotAuthenticated", "User is not authenticated.");
        
        return await dbContext.QuizSets
            .AsNoTracking()
            .Where(qs => qs.OwnerId == currentUserService.UserId)
            .Select(qs => new QuizSetSummary(qs.Id, qs.Title, qs.Description, qs.Visibility))
            .ToListAsync(ct);
    }
}