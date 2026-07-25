using ErrorOr;
using Mediator;

namespace QuizArena.Application.Features.Admin.Commands.UnbanUser;

public sealed record UnbanUserCommand(Guid UserId) : ICommand<ErrorOr<Updated>>;