using ErrorOr;
using Mediator;

namespace QuizArena.Application.Features.QuizSets.Commands.CreateQuizSet;

public sealed record CreateQuizSetCommand(string Title, string Description)
: ICommand<ErrorOr<Guid>>;