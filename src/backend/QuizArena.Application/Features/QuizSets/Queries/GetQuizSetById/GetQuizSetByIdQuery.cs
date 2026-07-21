using Mediator;
using ErrorOr;

namespace QuizArena.Application.Features.QuizSets.Queries.GetQuizSetById;

public sealed record GetQuizSetByIdQuery(Guid QuizSetId) : IQuery<ErrorOr<QuizSetResponse>>;