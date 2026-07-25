using Mediator;
using ErrorOr;
using QuizArena.Application.Common;
using QuizArena.Domain.Enums;

namespace QuizArena.Application.Features.Admin.Queries.GetAllQuizSetsForModeration;

public sealed record AdminQuizSetSummary(Guid Id, string Title, Guid OwnerId, Visibility Visibility, DateTimeOffset CreatedAt);

public sealed record GetAllQuizSetsForModerationQuery(int PageNumber = 1, int PageSize = 20)
    : IQuery<ErrorOr<PagedResponse<AdminQuizSetSummary>>>;