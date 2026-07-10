using ErrorOr;
using Mediator;

namespace QuizArena.Application.Features.Questions.Queries.GetQuestionsByQuizSet;

public sealed record GetQuestionsByQuizSetQuery(Guid QuizSetId)
: IQuery<ErrorOr<IReadOnlyList<QuestionResponse>>>;