using ErrorOr;
using Mediator;

namespace QuizArena.Application.Features.Admin.Commands.BanUser;

public sealed record BanUserCommand(Guid UserId) : ICommand<ErrorOr<Updated>>;