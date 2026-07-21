using ErrorOr;
using Mediator;

namespace QuizArena.Application.Features.Questions.Commands.DeleteQuestion;

public sealed record DeleteQuestionCommand(
    Guid QuizSetId,
    Guid QuestionId)
    : ICommand<ErrorOr<Deleted>>;