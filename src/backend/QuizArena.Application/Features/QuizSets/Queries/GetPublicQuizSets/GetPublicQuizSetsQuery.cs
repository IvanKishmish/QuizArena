using ErrorOr;
using Mediator;
using QuizArena.Application.Common;
using QuizArena.Application.Features.QuizSets.Queries.Common;

namespace QuizArena.Application.Features.QuizSets.Queries.GetPublicQuizSets;

public sealed record GetPublicQuizSetsQuery(int PageNumber = 1, int PageSize = 20)
: IQuery<ErrorOr<PagedResponse<QuizSetSummary>>>;