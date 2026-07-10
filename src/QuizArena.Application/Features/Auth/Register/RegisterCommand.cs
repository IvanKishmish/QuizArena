using ErrorOr;
using Mediator;

namespace QuizArena.Application.Features.Auth.Register;

public sealed record RegisterCommand(string NickName, string Email, string Password)
: ICommand<ErrorOr<Guid>>;