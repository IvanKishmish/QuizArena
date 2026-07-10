using Mediator;
using ErrorOr;

namespace QuizArena.Application.Features.QuizSets.Commands.DeleteQuizSet;

public sealed record DeleteQuizSetCommand(Guid QuizSetId)
: ICommand<ErrorOr<Deleted>>;