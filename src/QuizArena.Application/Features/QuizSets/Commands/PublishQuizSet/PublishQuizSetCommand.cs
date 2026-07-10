using Mediator;
using ErrorOr;

namespace QuizArena.Application.Features.QuizSets.Commands.PublishQuizSet;

public sealed record PublishQuizSetCommand(Guid QuizSetId) 
: ICommand<ErrorOr<Updated>>;