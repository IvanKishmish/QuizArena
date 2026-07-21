using ErrorOr;
using Mediator;

namespace QuizArena.Application.Features.QuizSets.Commands.UnpublishQuizSet;

public sealed record UnpublishQuizSetCommand(Guid QuizSetId)
: ICommand<ErrorOr<Updated>>;