using Mediator;
using ErrorOr;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using QuizArena.Application.Common;
using QuizArena.Application.Common.Interfaces;

namespace QuizArena.Application.Features.GameHistory.Queries.GetMyGameHistory;

public sealed class GetMyGameHistoryQueryHandler(
    IAppDbContext context, ICurrentUserService currentUser, IValidator<GetMyGameHistoryQuery> validator)
    : IQueryHandler<GetMyGameHistoryQuery, ErrorOr<PagedResponse<GameHistorySummary>>>
{
    public async ValueTask<ErrorOr<PagedResponse<GameHistorySummary>>> Handle(
        GetMyGameHistoryQuery query, CancellationToken ct = default)
    {
        var validationResult = await validator.ValidateAsync(query, ct);

        if (!validationResult.IsValid)
            return validationResult.Errors
                .Select(e => Error.Validation(e.PropertyName, e.ErrorMessage))
                .ToList();

        if (currentUser.UserId is null)
            return Error.Unauthorized("Auth.NotAuthenticated", "User is not authenticated.");

        var baseQuery = context.GameHistory
            .AsNoTracking()
            .Where(h => h.ParticipantUserId == currentUser.UserId);

        var totalCount = await baseQuery.CountAsync(ct);

        var entries = await baseQuery
            .OrderByDescending(h => h.CreatedAt)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        var quizSetIds = entries.Select(e => e.QuizSetId).Distinct().ToList();

        var titlesByQuizSetId = await context.QuizSets
            .AsNoTracking()
            .Where(q => quizSetIds.Contains(q.Id))
            .Select(q => new { q.Id, q.Title })
            .ToDictionaryAsync(q => q.Id, q => q.Title, ct);

        var items = entries
            .Select(e => new GameHistorySummary(
                e.QuizSetId,
                titlesByQuizSetId.GetValueOrDefault(e.QuizSetId, "Deleted quiz"),
                e.FinalScore,
                e.Placement,
                e.CreatedAt))
            .ToList();

        return new PagedResponse<GameHistorySummary>(items, query.PageNumber, query.PageSize, totalCount);
    }
}
