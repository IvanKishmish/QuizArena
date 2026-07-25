using ErrorOr;
using Mediator;

namespace QuizArena.Application.Features.Admin.Commands.DeleteAnyQuizSet;

public sealed record DeleteAnyQuizSetCommand(Guid QuizSetId) : ICommand<ErrorOr<Deleted>>;