using ErrorOr;
using Mediator;
using QuizArena.Application.Features.QuizSets.Queries.Common;

namespace QuizArena.Application.Features.QuizSets.Queries.GetPublicQuizSets;

public sealed record GetPublicQuizSetsQuery
: IQuery<ErrorOr<IReadOnlyList<QuizSetSummary>>>;