using ErrorOr;
using Mediator;
using Microsoft.EntityFrameworkCore;
using QuizArena.Application.Common;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Application.Features.QuizSets.Queries.Common;
using QuizArena.Domain.Enums;

namespace QuizArena.Application.Features.QuizSets.Queries.GetPublicQuizSets;

public sealed class GetPublicQuizSetsQueryHandler
(IAppDbContext dbContext)
: IQueryHandler<GetPublicQuizSetsQuery, ErrorOr<PagedResponse<QuizSetSummary>>>
{
    public async ValueTask<ErrorOr<PagedResponse<QuizSetSummary>>> Handle(GetPublicQuizSetsQuery request,
        CancellationToken ct = default)
    {
        var query = dbContext.QuizSets
            .AsNoTracking()
            .Where(qs => qs.Visibility == Visibility.Public);
        
        var totalCount = await query.CountAsync(ct);
        
        var items = await query
            .OrderByDescending(qs => qs.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(qs => new QuizSetSummary(qs.Id, qs.Title, qs.Description, qs.Visibility))
            .ToListAsync(ct);
        
        return new PagedResponse<QuizSetSummary>(items, request.PageNumber, request.PageSize, totalCount);
    }
}