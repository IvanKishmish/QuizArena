using ErrorOr;
using Mediator;

namespace QuizArena.Application.Features.Auth.Login;

public sealed record LoginCommand(string Email, string Password) : ICommand<ErrorOr<string>>;