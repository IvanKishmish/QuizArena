using ErrorOr;
using Mediator;
using Microsoft.EntityFrameworkCore;
using QuizArena.Application.Common.Interfaces;
using QuizArena.Application.Features.QuizSets.Queries.Common;
using QuizArena.Domain.Enums;

namespace QuizArena.Application.Features.QuizSets.Queries.GetPublicQuizSets;

public sealed class GetPublicQuizSetsQueryHandler
(IAppDbContext dbContext)
: IQueryHandler<GetPublicQuizSetsQuery, ErrorOr<IReadOnlyList<QuizSetSummary>>>
{
    public async ValueTask<ErrorOr<IReadOnlyList<QuizSetSummary>>> Handle(GetPublicQuizSetsQuery request,
        CancellationToken ct = default)
    => await dbContext.QuizSets
        .AsNoTracking()
        .Where(qs => qs.Visibility == Visibility.Public)
        .Select(qs => new QuizSetSummary(qs.Id, qs.Title, qs.Description, qs.Visibility))
        .ToListAsync(ct);
}