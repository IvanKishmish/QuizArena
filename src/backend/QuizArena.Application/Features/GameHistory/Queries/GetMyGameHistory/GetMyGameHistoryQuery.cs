using Mediator;
using ErrorOr;
using QuizArena.Application.Common;

namespace QuizArena.Application.Features.GameHistory.Queries.GetMyGameHistory;

public sealed record GameHistorySummary(
    Guid QuizSetId,
    string QuizSetTitle,
    int FinalScore,
    int Placement,
    DateTimeOffset PlayedAt);

public sealed record GetMyGameHistoryQuery(int PageNumber = 1, int PageSize = 20)
    : IQuery<ErrorOr<PagedResponse<GameHistorySummary>>>, IPagedQuery;
