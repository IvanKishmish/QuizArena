using Mediator;
using ErrorOr;

namespace QuizArena.Application.Features.QuizSets.Commands.UpdateQuizSet;

public sealed record UpdateQuizSetDetailsCommand(Guid QuizSetId, string Title, string Description)
: ICommand<ErrorOr<Updated>>;