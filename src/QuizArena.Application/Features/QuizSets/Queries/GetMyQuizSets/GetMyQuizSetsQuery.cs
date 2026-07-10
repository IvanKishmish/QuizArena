using Mediator;
using ErrorOr;
using QuizArena.Application.Features.QuizSets.Queries.Common;
using QuizArena.Application.Features.QuizSets.Queries.GetPublicQuizSets;

namespace QuizArena.Application.Features.QuizSets.Queries.GetMyQuizSets;

public sealed record GetMyQuizSetsQuery : IQuery<ErrorOr<IReadOnlyList<QuizSetSummary>>>;